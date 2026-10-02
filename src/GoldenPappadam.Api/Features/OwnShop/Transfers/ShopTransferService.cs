using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.OwnShop;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.OwnShop.Transfers;

/// <summary>
/// Sends loose pappadam from the factory to the own shop (CLAUDE.md §4 "Own shop"). The factory weighs
/// it out in kg; the shop sells it by the piece; so the transfer converts as it moves, with the variety's
/// own pieces per kg: 30 kg at 200 per kg arrives as 6,000 pieces. Rounded to the nearest whole piece
/// (owner, 2026-09-30).
///
/// The transfer and its two movements - kg out of the warehouse, pieces into the shop - are one
/// transaction. Refused when the warehouse does not hold the kg, like packing, and under the same lock
/// packing takes, so the two can never both use the last kilogram.
/// </summary>
public class ShopTransferService(AppDbContext db, StockService stock)
{
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public Task<ShopTransferPlanDto> PreviewAsync(ShopTransferPreviewRequest request, CancellationToken ct) =>
        PlanAsync(request.SourceProductId, request.QuantityKg, ct);

    public async Task<ShopTransferDto> CreateAsync(CreateShopTransferRequest request, CancellationToken ct)
    {
        // A second press of Save sends the same id: answer with the transfer the first one made.
        if (request.ClientRequestId is { } clientRequestId && await ExistingAsync(clientRequestId, ct) is { } existing)
        {
            return existing;
        }

        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Two transfers - or a transfer and a packing - of the same pappadam wait for each other here,
        // so both cannot pass the stock check below.
        await stock.LockProductsAsync([request.SourceProductId], ct);

        var plan = await PlanAsync(request.SourceProductId, request.QuantityKg, ct);

        if (plan.Shortfall is { } shortfall)
        {
            throw new DomainException(shortfall);
        }

        var transfer = new ShopTransfer
        {
            SourceProductId = plan.SourceProductId,
            PiecesProductId = plan.PiecesProductId,
            FromLocationId = KnownStockLocations.MainWarehouseId,
            ToLocationId = KnownStockLocations.OwnShopId,
            QuantityKg = plan.QuantityKg,
            PiecesPerKg = plan.PiecesPerKg,
            PiecesReceived = plan.Pieces,
            SourceOnHandBefore = plan.FactoryOnHandBefore,
            ShopOnHandBefore = plan.ShopOnHandBefore,
            OccurredAt = occurredAt,
            ClientRequestId = request.ClientRequestId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        db.ShopTransfers.Add(transfer);

        StockMovement Movement(Guid productId, Guid locationId, decimal quantity) => new()
        {
            ProductId = productId,
            LocationId = locationId,
            MovementType = StockMovementType.ShopTransfer,
            Quantity = quantity,
            OccurredAt = occurredAt,
            ReferenceType = StockReferenceType.ShopTransfer,
            ReferenceId = transfer.Id
        };

        db.StockMovements.Add(Movement(plan.SourceProductId, transfer.FromLocationId, -plan.QuantityKg));
        db.StockMovements.Add(Movement(plan.PiecesProductId, transfer.ToLocationId, plan.Pieces));

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (request.ClientRequestId is { } id && IsDuplicate(exception))
        {
            // The same Save raced itself; the one that got in first is the transfer.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ExistingAsync(id, ct) ?? throw new DomainException("The transfer could not be saved. Try again.");
        }

        return await GetAsync(transfer.Id, ct);
    }

    /// <summary><paramref name="from"/> and <paramref name="to"/> are IST business days.</summary>
    public async Task<IReadOnlyList<ShopTransferDto>> GetHistoryAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var fromUtc = from is { } fromDay ? IndiaTime.DayRangeUtc(fromDay).Start : (DateTime?)null;
        var toUtc = to is { } toDay ? IndiaTime.DayRangeUtc(toDay).End : (DateTime?)null;

        return await Project(db.ShopTransfers
                .Where(t => fromUtc == null || t.OccurredAt >= fromUtc)
                .Where(t => toUtc == null || t.OccurredAt < toUtc)
                .OrderByDescending(t => t.OccurredAt)
                .ThenByDescending(t => t.CreatedAt))
            .ToListAsync(ct);
    }

    private async Task<ShopTransferDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.ShopTransfers.Where(t => t.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Transfer");

    private async Task<ShopTransferDto?> ExistingAsync(Guid clientRequestId, CancellationToken ct) =>
        await Project(db.ShopTransfers.Where(t => t.ClientRequestId == clientRequestId)).FirstOrDefaultAsync(ct);

    private IQueryable<ShopTransferDto> Project(IQueryable<ShopTransfer> transfers) =>
        transfers.Select(t => new ShopTransferDto(
            t.Id,
            t.OccurredAt,
            t.SourceProductId,
            t.SourceProduct!.Name,
            t.PiecesProductId,
            t.PiecesProduct!.Name,
            t.FromLocation!.Name,
            t.ToLocation!.Name,
            t.QuantityKg,
            t.PiecesPerKg,
            t.PiecesReceived,
            t.SourceOnHandBefore,
            t.SourceOnHandBefore - t.QuantityKg,
            t.ShopOnHandBefore,
            t.ShopOnHandBefore + t.PiecesReceived,
            db.Users.Where(u => u.Id == t.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            t.Notes));

    /// <summary>Checks the variety and works the numbers out; writes nothing.</summary>
    private async Task<ShopTransferPlanDto> PlanAsync(Guid sourceProductId, decimal quantityKg, CancellationToken ct)
    {
        if (quantityKg <= 0m)
        {
            throw new DomainException("Enter how many kg are being sent, more than zero.");
        }

        var source = await db.Products.AsNoTracking()
                         .Where(p => p.Id == sourceProductId)
                         .Select(p => new { p.Id, p.Name, p.Kind, p.UnitOfMeasureId, p.IsActive, p.PiecesPerKg })
                         .FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Product");

        if (source.Kind != ProductKind.Loose || source.UnitOfMeasureId != KnownUnits.KilogramId)
        {
            throw new DomainException($"'{source.Name}' is not loose pappadam counted in kg, so it is not sent to the shop.");
        }

        if (!source.IsActive)
        {
            throw new DomainException($"Product '{source.Name}' is not active.");
        }

        if (source.PiecesPerKg is not { } perKg || perKg <= 0m)
        {
            throw new DomainException(
                $"'{source.Name}' has no pieces per kg, so its kg cannot be turned into pieces. Set it on the product first.");
        }

        var pieces = await db.Products.AsNoTracking()
                         .Where(p => p.Kind == ProductKind.Pieces && p.IsActive && p.SourceProductId == source.Id)
                         .Select(p => new { p.Id, p.Name })
                         .FirstOrDefaultAsync(ct)
                     ?? throw new DomainException(
                         $"The own shop has no pieces product for '{source.Name}' yet. Add one on the Products screen " +
                         "(type \"Shop pieces\"), with its rate per piece.");

        var piecesReceived = decimal.Round(quantityKg * perKg, 0, MidpointRounding.AwayFromZero);

        if (piecesReceived <= 0m)
        {
            throw new DomainException("That is too little to make a whole piece. Check the kg.");
        }

        var factoryBefore = await stock.GetQuantityOnHandAsync(source.Id, KnownStockLocations.MainWarehouseId, ct);
        var shopBefore = await stock.GetQuantityOnHandAsync(pieces.Id, KnownStockLocations.OwnShopId, ct);

        return new ShopTransferPlanDto(
            source.Id,
            source.Name,
            pieces.Id,
            pieces.Name,
            quantityKg,
            perKg,
            piecesReceived,
            factoryBefore,
            factoryBefore - quantityKg,
            shopBefore,
            shopBefore + piecesReceived,
            factoryBefore >= quantityKg
                ? null
                : $"Sending {Quantity(quantityKg)} kg of {source.Name} needs {Quantity(quantityKg)} kg; the warehouse has " +
                  $"{(factoryBefore > 0m ? $"{Quantity(factoryBefore)} kg" : "none")}. Send less, or record the missing stock first.");
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number);

    private static string Quantity(decimal value) => PdfStyle.Quantity(value);
}
