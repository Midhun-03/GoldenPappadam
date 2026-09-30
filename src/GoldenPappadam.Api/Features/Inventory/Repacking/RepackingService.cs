using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Repacking;

/// <summary>
/// Repacks unsold packets - into the same size or another - when the office decides they have sat
/// too long. Nothing is lost in repacking, so the conversion is exact: both products are traced
/// back to the loose pappadam they are packed from, and counted in pieces when that variety has its
/// pieces per kg (else in its own unit). Five 20-piece packets are 100 pieces, which is ten 10-piece
/// packets; pieces that do not fill one more packet go back to the loose stock, as kg. Counting in
/// pieces keeps the division exact - in kg, 20 ÷ 170 repeats and 10.000 packets could round to 9.
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
        Guid? SourceProductId, decimal? SourceQuantityPerPack, int? PiecesPerPack, decimal? PiecesPerKg);

    /// <summary>
    /// A packed product traced to its loose pappadam. <see cref="Size"/> is what one packet holds, in
    /// pieces when the loose has a pieces-per-kg figure (<see cref="PiecesPerKg"/> set), otherwise in the
    /// loose product's own unit.
    /// </summary>
    private sealed record Traced(ProductInfo Root, decimal Size, decimal? PiecesPerKg)
    {
        public string CountUnit => PiecesPerKg is null ? Root.UnitCode : "pieces";

        /// <summary>A count of <see cref="CountUnit"/> as a quantity of the loose product, to the gram.</summary>
        public decimal AsLoose(decimal count) =>
            PackConversion.Round(PiecesPerKg is { } perKg ? count / perKg : count);
    }

    /// <summary>Checks everything and works the numbers out before anything is written.</summary>
    private async Task<RepackPlanDto> PlanAsync(RepackRequest request, CancellationToken ct)
    {
        var products = await db.Products
            .Select(p => new ProductInfo(p.Id, p.Name, p.Kind, p.IsActive, p.UnitOfMeasure!.Code,
                p.SourceProductId, p.SourceQuantityPerPack, p.PiecesPerPack, p.PiecesPerKg))
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

        var fromTraced = Trace(from, products);
        var toTraced = Trace(to, products);
        var (fromRoot, fromSize) = (fromTraced.Root, fromTraced.Size);
        var (toRoot, toSize) = (toTraced.Root, toTraced.Size);

        if (fromRoot.Id != toRoot.Id)
        {
            throw new DomainException(
                $"'{from.Name}' is packed from '{fromRoot.Name}' and '{to.Name}' from '{toRoot.Name}', so one " +
                "cannot be repacked into the other.");
        }

        var contents = request.FromQuantity * fromSize;
        var packets = decimal.Floor(contents / toSize);
        var leftover = fromTraced.AsLoose(contents - packets * toSize);
        var unit = fromTraced.CountUnit;

        if (packets <= 0m)
        {
            throw new DomainException(
                $"{Quantity(request.FromQuantity)} {from.UnitCode} of '{from.Name}' hold {Quantity(contents)} " +
                $"{unit}, less than one '{to.Name}' ({Quantity(toSize)} {unit}). Open more packets.");
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
    /// holds: a 20-piece packet holds 20 pieces; a box of 12 such packets holds 240; a 250 g packet of a
    /// 200-per-kg pappadam holds 50. Without a pieces-per-kg figure on the loose, the size is in the
    /// loose product's unit instead (0.250 kg).
    /// </summary>
    private static Traced Trace(ProductInfo product, Dictionary<Guid, ProductInfo> all)
    {
        // First find the loose product, so we know whether to count in pieces.
        var chain = new List<(ProductInfo Pack, ProductInfo Source)>();
        var current = product;

        for (var depth = 0; current.Kind == ProductKind.Packed; depth++)
        {
            if (depth >= MaxChainDepth ||
                current.SourceProductId is not { } sourceId ||
                !all.TryGetValue(sourceId, out var source))
            {
                throw new DomainException($"'{product.Name}' has no complete packed-from chain to a loose product.");
            }

            chain.Add((current, source));
            current = source;
        }

        var root = current;
        var perKg = root.PiecesPerKg is > 0m ? root.PiecesPerKg : null;
        var size = 1m;

        foreach (var (pack, source) in chain)
        {
            if (source.Kind == ProductKind.Loose && pack.PiecesPerPack is { } pieces && perKg is not null)
            {
                size *= pieces;
            }
            else if (source.Kind == ProductKind.Loose && perKg is { } piecesPerKg)
            {
                // A weight-based packet, counted in the pieces it holds.
                size *= PackConversion.SourcePerPack(Pack(pack, source)) * piecesPerKg;
            }
            else
            {
                size *= PackConversion.SourcePerPack(Pack(pack, source));
            }
        }

        return new Traced(root, size, perKg);
    }

    private static PackConversion.Pack Pack(ProductInfo pack, ProductInfo source) =>
        new(pack.Name, pack.PiecesPerPack, pack.SourceQuantityPerPack, source.Name, source.PiecesPerKg);

    private static string Quantity(decimal value) => PdfStyle.Quantity(value);
}
