using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Packing;

/// <summary>
/// Turns source stock into packs: one packing entry plus the two stock movements it causes,
/// written in a single transaction so stock can never be half-changed.
/// </summary>
public class PackingService(AppDbContext db, StockService stock)
{
    public async Task<PackingResponse> CreateAsync(CreatePackingRequest request, CancellationToken ct)
    {
        if (request.PacksProduced <= 0)
        {
            throw new DomainException("Packs produced must be greater than zero.");
        }

        var packedProduct = await db.Products.FirstOrDefaultAsync(p => p.Id == request.PackedProductId, ct)
                            ?? throw new NotFoundException("Packed product");

        if (packedProduct.Kind != ProductKind.Packed)
        {
            throw new DomainException($"'{packedProduct.Name}' is a loose product, so it cannot be packed into.");
        }

        if (!packedProduct.IsActive)
        {
            throw new DomainException($"Product '{packedProduct.Name}' is not active.");
        }

        // Guaranteed by the check constraint, but read them into locals to keep the compiler happy.
        if (packedProduct.SourceProductId is not { } sourceProductId ||
            packedProduct.SourceQuantityPerPack is not { } quantityPerPack)
        {
            throw new DomainException($"'{packedProduct.Name}' has no source product configured.");
        }

        var sourceQuantityUsed = request.SourceQuantityUsed
                                 ?? decimal.Round(request.PacksProduced * quantityPerPack, 3, MidpointRounding.AwayFromZero);

        if (sourceQuantityUsed <= 0)
        {
            throw new DomainException("The source quantity used must be greater than zero.");
        }

        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var entry = new PackingEntry
        {
            PackedProductId = packedProduct.Id,
            SourceProductId = sourceProductId,
            PacksProduced = request.PacksProduced,
            SourceQuantityUsed = sourceQuantityUsed,
            OccurredAt = occurredAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        db.PackingEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        db.StockMovements.AddRange(
            new StockMovement
            {
                ProductId = sourceProductId,
                // Packing is warehouse work: nobody packs on the van.
                LocationId = KnownStockLocations.MainWarehouseId,
                MovementType = StockMovementType.Packing,
                Quantity = -sourceQuantityUsed,
                OccurredAt = occurredAt,
                ReferenceType = StockReferenceType.PackingEntry,
                ReferenceId = entry.Id
            },
            new StockMovement
            {
                ProductId = packedProduct.Id,
                LocationId = KnownStockLocations.MainWarehouseId,
                MovementType = StockMovementType.Packing,
                Quantity = request.PacksProduced,
                OccurredAt = occurredAt,
                ReferenceType = StockReferenceType.PackingEntry,
                ReferenceId = entry.Id
            });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var sourceOnHand = await stock.GetQuantityOnHandAsync(sourceProductId, KnownStockLocations.MainWarehouseId, ct);
        var packedOnHand = await stock.GetQuantityOnHandAsync(packedProduct.Id, KnownStockLocations.MainWarehouseId, ct);

        return new PackingResponse(
            entry.Id,
            packedProduct.Id,
            entry.PacksProduced,
            packedOnHand,
            sourceProductId,
            entry.SourceQuantityUsed,
            sourceOnHand,
            StockService.WarningFor(sourceOnHand));
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
                e.Notes))
            .ToListAsync(ct);
}
