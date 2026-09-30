using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Packing;

/// <summary>
/// Turns loose stock into packets (or packets into boxes): a conversion of stock that already exists,
/// never new production (CLAUDE.md §4 "Packing conversion"). One packing entry and its two movements -
/// source down, packs up - are written in a single transaction, so neither can exist without the other.
///
/// What the source loses is calculated, never typed: packets × pieces ÷ the loose variety's pieces per
/// kg for a count-based packet, packets × weight for a weight-based one, packets × count for a box.
/// Packing is refused when the warehouse does not hold enough - the one stock operation that blocks
/// rather than warns - and each entry keeps the conversion it used.
/// </summary>
public class PackingService(AppDbContext db, StockService stock)
{
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    /// <summary>What a packing would do, for the screen to show before anything is saved.</summary>
    public Task<PackingPlanDto> PreviewAsync(PackingPreviewRequest request, CancellationToken ct) =>
        PlanAsync(request.PackedProductId, request.PacksProduced, ct);

    public async Task<PackingResponse> CreateAsync(CreatePackingRequest request, CancellationToken ct)
    {
        // A second press of Save sends the same id: answer with the packing the first one made.
        if (request.ClientRequestId is { } clientRequestId &&
            await ExistingAsync(clientRequestId, ct) is { } existing)
        {
            return existing;
        }

        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var packed = await db.Products.AsNoTracking()
                         .Where(p => p.Id == request.PackedProductId)
                         .Select(p => new { p.SourceProductId })
                         .FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Packed product");

        // Two packings of the same pappadam at once must not both pass the stock check below: the
        // second waits here until the first has written its movements.
        if (packed.SourceProductId is { } sourceId)
        {
            await db.Database
                .SqlQuery<Guid>($"SELECT Id AS Value FROM inventory.Products WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sourceId}")
                .ToListAsync(ct);
        }

        var plan = await PlanAsync(request.PackedProductId, request.PacksProduced, ct);

        if (plan.Shortfall is { } shortfall)
        {
            throw new DomainException(shortfall);
        }

        var entry = new PackingEntry
        {
            PackedProductId = plan.PackedProductId,
            SourceProductId = plan.SourceProductId,
            PacksProduced = plan.PacksProduced,
            SourceQuantityUsed = plan.SourceUsed,
            PiecesPerPack = plan.PiecesPerPack,
            PiecesPerKg = plan.PiecesPerKg,
            SourcePerPack = plan.SourcePerPack,
            SourceOnHandBefore = plan.SourceOnHandBefore,
            ClientRequestId = request.ClientRequestId,
            OccurredAt = occurredAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        db.PackingEntries.Add(entry);

        StockMovement Movement(Guid productId, decimal quantity) => new()
        {
            ProductId = productId,
            // Packing is warehouse work: nobody packs on the van.
            LocationId = KnownStockLocations.MainWarehouseId,
            MovementType = StockMovementType.Packing,
            Quantity = quantity,
            OccurredAt = occurredAt,
            ReferenceType = StockReferenceType.PackingEntry,
            ReferenceId = entry.Id
        };

        db.StockMovements.Add(Movement(plan.SourceProductId, -plan.SourceUsed));
        db.StockMovements.Add(Movement(plan.PackedProductId, plan.PacksProduced));

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (request.ClientRequestId is { } id && IsDuplicate(exception))
        {
            // The same Save raced itself; the one that got in first is the packing.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ExistingAsync(id, ct) ?? throw new DomainException("The packing could not be saved. Try again.");
        }

        return await ResponseAsync(entry.Id, plan, ct);
    }

    public async Task<IReadOnlyList<PackingEntryDto>> GetHistoryAsync(
        DateTime? from,
        DateTime? to,
        Guid? packedProductId,
        CancellationToken ct) =>
        await db.PackingEntries
            .Where(e => from == null || e.OccurredAt >= from)
            .Where(e => to == null || e.OccurredAt <= to)
            .Where(e => packedProductId == null || e.PackedProductId == packedProductId)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => new PackingEntryDto(
                e.Id,
                e.OccurredAt,
                e.PackedProductId,
                e.PackedProduct!.Name,
                e.PacksProduced,
                e.SourceProductId,
                e.SourceProduct!.Name,
                e.SourceQuantityUsed,
                e.Notes,
                e.SourceProduct.UnitOfMeasure!.Code,
                e.PiecesPerPack,
                e.PiecesPerKg,
                e.SourcePerPack,
                e.SourceOnHandBefore,
                e.SourceOnHandBefore - e.SourceQuantityUsed))
            .ToListAsync(ct);

    /// <summary>Checks the product and works the numbers out; writes nothing.</summary>
    private async Task<PackingPlanDto> PlanAsync(Guid packedProductId, decimal packsProduced, CancellationToken ct)
    {
        if (packsProduced <= 0)
        {
            throw new DomainException("Packs produced must be greater than zero.");
        }

        var packed = await db.Products.AsNoTracking()
                         .Where(p => p.Id == packedProductId)
                         .Select(p => new
                         {
                             p.Id, p.Name, p.Kind, p.IsActive, p.PiecesPerPack, p.SourceQuantityPerPack,
                             UnitCode = p.UnitOfMeasure!.Code,
                             p.SourceProductId,
                             SourceName = p.SourceProduct != null ? p.SourceProduct.Name : null,
                             SourceUnitCode = p.SourceProduct != null ? p.SourceProduct.UnitOfMeasure!.Code : null,
                             SourcePiecesPerKg = p.SourceProduct != null ? p.SourceProduct.PiecesPerKg : null
                         })
                         .FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Packed product");

        if (packed.Kind != ProductKind.Packed)
        {
            throw new DomainException($"'{packed.Name}' is a loose product, so it cannot be packed into.");
        }

        if (!packed.IsActive)
        {
            throw new DomainException($"Product '{packed.Name}' is not active.");
        }

        if (packed.SourceProductId is not { } sourceId)
        {
            throw new DomainException($"'{packed.Name}' has no source product configured.");
        }

        if (packed.PiecesPerPack is not null && packsProduced != decimal.Truncate(packsProduced))
        {
            throw new DomainException("A packet of pieces is counted whole: enter a whole number of packets.");
        }

        var pack = new PackConversion.Pack(
            packed.Name, packed.PiecesPerPack, packed.SourceQuantityPerPack, packed.SourceName!, packed.SourcePiecesPerKg);
        var sourcePerPack = PackConversion.SourcePerPack(pack);
        var used = PackConversion.SourceFor(pack, packsProduced);

        if (used <= 0m)
        {
            throw new DomainException("That is too few packs to use any stock. Check the quantity.");
        }

        var before = await stock.GetQuantityOnHandAsync(sourceId, KnownStockLocations.MainWarehouseId, ct);
        var countBased = packed.PiecesPerPack is not null;
        var perKg = countBased ? PackConversion.PiecesPerKgOf(pack) : (decimal?)null;

        return new PackingPlanDto(
            packed.Id,
            packed.Name,
            packsProduced,
            packed.UnitCode,
            sourceId,
            packed.SourceName!,
            packed.SourceUnitCode!,
            packed.PiecesPerPack,
            perKg,
            sourcePerPack,
            used,
            countBased ? packsProduced * packed.PiecesPerPack!.Value : null,
            before,
            before - used,
            before >= used ? null : Shortfall(packed.Name, packsProduced, packed.PiecesPerPack, perKg, used, before,
                packed.SourceName!, packed.SourceUnitCode!));
    }

    /// <summary>
    /// "Packing 150 × 20 piece packet needs 3,000 pieces (15 KG); the warehouse has 2,000 pieces (10 KG) of
    /// Loose pappadam (standard)." Pieces where the packet is counted in them, so the office recognises
    /// the numbers.
    /// </summary>
    private static string Shortfall(string packedName, decimal packs, int? piecesPerPack, decimal? perKg,
        decimal used, decimal onHand, string sourceName, string sourceUnit)
    {
        string Amount(decimal quantity) =>
            perKg is { } k
                ? $"{Quantity(quantity * k)} pieces ({Quantity(quantity)} {sourceUnit})"
                : $"{Quantity(quantity)} {sourceUnit}";

        var needs = piecesPerPack is { } pieces && perKg is not null
            ? $"{Quantity(packs * pieces)} pieces ({Quantity(used)} {sourceUnit})"
            : Amount(used);

        return $"Packing {Quantity(packs)} × {packedName} needs {needs}; the warehouse has " +
               $"{(onHand > 0m ? Amount(onHand) : "none")} of {sourceName}. Pack fewer, or record the " +
               "missing stock first.";
    }

    private async Task<PackingResponse?> ExistingAsync(Guid clientRequestId, CancellationToken ct)
    {
        var entry = await db.PackingEntries.AsNoTracking().FirstOrDefaultAsync(e => e.ClientRequestId == clientRequestId, ct);
        if (entry is null)
        {
            return null;
        }

        var plan = await PlanAsync(entry.PackedProductId, entry.PacksProduced, ct);
        var recorded = plan with
        {
            SourceUsed = entry.SourceQuantityUsed,
            SourceOnHandBefore = entry.SourceOnHandBefore ?? plan.SourceOnHandBefore,
            SourceOnHandAfter = (entry.SourceOnHandBefore ?? plan.SourceOnHandBefore) - entry.SourceQuantityUsed,
            Shortfall = null
        };

        return await ResponseAsync(entry.Id, recorded, ct);
    }

    private async Task<PackingResponse> ResponseAsync(Guid entryId, PackingPlanDto plan, CancellationToken ct)
    {
        var sourceOnHand = await stock.GetQuantityOnHandAsync(plan.SourceProductId, KnownStockLocations.MainWarehouseId, ct);
        var packedOnHand = await stock.GetQuantityOnHandAsync(plan.PackedProductId, KnownStockLocations.MainWarehouseId, ct);

        return new PackingResponse(
            entryId,
            plan.PackedProductId,
            plan.PacksProduced,
            packedOnHand,
            plan.SourceProductId,
            plan.SourceUsed,
            sourceOnHand,
            null,
            plan);
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number);

    private static string Quantity(decimal value) => PdfStyle.Quantity(value);
}
