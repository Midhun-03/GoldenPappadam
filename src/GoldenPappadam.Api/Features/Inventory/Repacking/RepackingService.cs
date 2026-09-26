using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Repacking;

/// <summary>
/// Repacks unsold packets - into the same size or another - when the office decides they have sat
/// too long. Nothing is lost in repacking, so the conversion is exact: both products are traced
/// back to the loose pappadam they are packed from, and everything is counted in its unit. Five
/// 20-piece packets are 100 pieces, which is ten 10-piece packets; pieces that do not fill one more
/// packet go back to the loose stock.
///
/// One entry and its movements are written in one transaction, at the warehouse, like packing.
/// </summary>
public class RepackingService(AppDbContext db, StockService stock)
{
    /// <summary>A box of packets of pieces is at most a few levels deep; this guards a bad cycle.</summary>
    private const int MaxChainDepth = 10;

    public Task<RepackPlanDto> PreviewAsync(RepackRequest request, CancellationToken ct) => PlanAsync(request, ct);

    public async Task<RepackResponse> CreateAsync(RepackRequest request, CancellationToken ct)
    {
        var dto = await PlanAsync(request, ct);
        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow;

        var entry = new RepackEntry
        {
            FromProductId = dto.FromProductId,
            FromQuantity = dto.FromQuantity,
            ToProductId = dto.ToProductId,
            ToQuantity = dto.ToQuantity,
            LeftoverProductId = dto.LeftoverQuantity > 0m ? dto.LeftoverProductId : null,
            LeftoverQuantity = dto.LeftoverQuantity,
            OccurredAt = occurredAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.RepackEntries.Add(entry);

        StockMovement Movement(Guid productId, decimal quantity) => new()
        {
            ProductId = productId,
            // Repacking is warehouse work, like packing: the van's stock comes back first.
            LocationId = KnownStockLocations.MainWarehouseId,
            MovementType = StockMovementType.Repacking,
            Quantity = quantity,
            OccurredAt = occurredAt,
            ReferenceType = StockReferenceType.RepackEntry,
            ReferenceId = entry.Id
        };

        db.StockMovements.Add(Movement(dto.FromProductId, -dto.FromQuantity));
        db.StockMovements.Add(Movement(dto.ToProductId, dto.ToQuantity));

        if (entry.LeftoverProductId is { } loose)
        {
            db.StockMovements.Add(Movement(loose, dto.LeftoverQuantity));
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var fromOnHand = await stock.GetQuantityOnHandAsync(dto.FromProductId, KnownStockLocations.MainWarehouseId, ct);

        return new RepackResponse(entry.Id, dto with { FromOnHand = fromOnHand, Warning = StockService.WarningFor(fromOnHand) });
    }

    public async Task<IReadOnlyList<RepackEntryDto>> GetHistoryAsync(CancellationToken ct) =>
        await db.RepackEntries
            .OrderByDescending(e => e.OccurredAt)
            .Take(200)
            .Select(e => new RepackEntryDto(
                e.Id,
                e.OccurredAt,
                e.FromProduct!.Name,
                e.FromQuantity,
                e.ToProduct!.Name,
                e.ToQuantity,
                e.LeftoverProduct != null ? e.LeftoverProduct.Name : null,
                e.LeftoverQuantity,
                e.Notes))
            .ToListAsync(ct);

    private sealed record ProductInfo(Guid Id, string Name, ProductKind Kind, bool IsActive, string UnitCode,
        Guid? SourceProductId, decimal? SourceQuantityPerPack);

    /// <summary>Checks everything and works the numbers out before anything is written.</summary>
    private async Task<RepackPlanDto> PlanAsync(RepackRequest request, CancellationToken ct)
    {
        var products = await db.Products
            .Select(p => new ProductInfo(p.Id, p.Name, p.Kind, p.IsActive, p.UnitOfMeasure!.Code,
                p.SourceProductId, p.SourceQuantityPerPack))
            .ToDictionaryAsync(p => p.Id, ct);

        var from = products.GetValueOrDefault(request.FromProductId) ?? throw new NotFoundException("Product to open");
        var to = products.GetValueOrDefault(request.ToProductId) ?? throw new NotFoundException("Product to pack into");

        if (from.Kind != ProductKind.Packed || to.Kind != ProductKind.Packed)
        {
            throw new DomainException(
                "Repacking opens packets and packs them again, so both products must be packed products. " +
                "Loose stock is packed from the Packing screen.");
        }

        if (!to.IsActive)
        {
            throw new DomainException($"Product '{to.Name}' is not active.");
        }

        if (request.FromQuantity != decimal.Truncate(request.FromQuantity))
        {
            throw new DomainException("Packets are opened whole: enter a whole number of packets.");
        }

        var (fromRoot, fromSize) = Root(from, products);
        var (toRoot, toSize) = Root(to, products);

        if (fromRoot.Id != toRoot.Id)
        {
            throw new DomainException(
                $"'{from.Name}' is packed from '{fromRoot.Name}' and '{to.Name}' from '{toRoot.Name}', so one " +
                "cannot be repacked into the other.");
        }

        var contents = request.FromQuantity * fromSize;
        var packets = decimal.Floor(contents / toSize);
        var leftover = decimal.Round(contents - packets * toSize, 3, MidpointRounding.AwayFromZero);

        if (packets <= 0m)
        {
            throw new DomainException(
                $"{Quantity(request.FromQuantity)} {from.UnitCode} of '{from.Name}' hold {Quantity(contents)} " +
                $"{fromRoot.UnitCode}, less than one '{to.Name}' ({Quantity(toSize)} {fromRoot.UnitCode}). Open more packets.");
        }

        var onHand = await stock.GetQuantityOnHandAsync(from.Id, KnownStockLocations.MainWarehouseId, ct);
        var warning = onHand < request.FromQuantity
            ? $"The warehouse has {Quantity(onHand)} {from.UnitCode} of '{from.Name}'; opening {Quantity(request.FromQuantity)} " +
              "will take its stock below zero. Bring the van's stock back first if that is where they are."
            : null;

        return new RepackPlanDto(
            from.Id, from.Name, request.FromQuantity, from.UnitCode,
            to.Id, to.Name, packets, to.UnitCode,
            fromRoot.Id, fromRoot.Name, leftover, fromRoot.UnitCode,
            onHand, warning);
    }

    /// <summary>
    /// The loose product a packed product is ultimately made from, and how much of it one packet
    /// holds: a 20-piece packet holds 20 pieces; a box of 12 such packets holds 240.
    /// </summary>
    private static (ProductInfo Root, decimal Size) Root(ProductInfo product, Dictionary<Guid, ProductInfo> all)
    {
        var size = 1m;
        var current = product;

        for (var depth = 0; current.Kind == ProductKind.Packed; depth++)
        {
            if (depth >= MaxChainDepth ||
                current.SourceProductId is not { } sourceId ||
                current.SourceQuantityPerPack is not { } perPack ||
                !all.TryGetValue(sourceId, out var source))
            {
                throw new DomainException($"'{product.Name}' has no complete packed-from chain to a loose product.");
            }

            size *= perPack;
            current = source;
        }

        return (current, size);
    }

    private static string Quantity(decimal value) => PdfStyle.Quantity(value);
}
