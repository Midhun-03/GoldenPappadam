using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

public record StockAgeLayerDto(DateOnly PackedOn, int AgeDays, decimal Quantity);

/// <summary>One product in one place, split by age. The bands are relative to its shelf life.</summary>
public record StockAgeRowDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitCode,
    Guid LocationId,
    string LocationCode,
    string LocationName,
    int ShelfLifeDays,
    decimal Fresh,
    decimal RepackWindow,
    decimal Ageing,
    decimal ExpiringSoon,
    decimal Expired,
    decimal Total,
    decimal ExpiringWithinDays,
    DateOnly? OldestPackedOn,
    string FreshLabel,
    string RepackLabel,
    string AgeingLabel,
    string ExpiringLabel,
    IReadOnlyList<StockAgeLayerDto> Layers);

/// <summary>How many products need attention today, for the dashboard.</summary>
public record StockAgeAlertsDto(int ToRepack, int ExpiringSoon, int Expired);

/// <summary>
/// Stock age for every product that has a shelf life, from the ledger (see
/// <see cref="StockAgeCalculator"/>), and writing off what has expired - always a person's decision,
/// never automatic, the same rule as a van shortfall.
/// </summary>
public class StockAgeService(AppDbContext db, StockService stock)
{
    public async Task<IReadOnlyList<StockAgeRowDto>> GetAsync(DateOnly asOf, CancellationToken ct)
    {
        var products = await db.Products
            .Where(p => p.ShelfLifeDays != null)
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.Name,
                p.Kind,
                UnitCode = p.UnitOfMeasure!.Code,
                ShelfLife = p.ShelfLifeDays!.Value
            })
            .ToDictionaryAsync(p => p.Id, ct);

        if (products.Count == 0)
        {
            return [];
        }

        var locations = await db.StockLocations
            .Select(l => new { l.Id, l.Code, l.Name })
            .ToDictionaryAsync(l => l.Id, ct);

        // Repacking can move pieces to a loose product that has no shelf life of its own, so every
        // movement of every aged product and of the loose products they come from is read.
        var looseIds = await db.Products.Where(p => p.Kind == ProductKind.Loose).Select(p => p.Id).ToListAsync(ct);
        var ids = products.Keys.Concat(looseIds).Distinct().ToList();
        var end = IndiaTime.DayRangeUtc(asOf).End;

        var movements = await db.StockMovements
            .Where(m => ids.Contains(m.ProductId) && m.OccurredAt < end)
            .Select(m => new
            {
                m.ProductId, m.LocationId, m.MovementType, m.Quantity, m.OccurredAt, m.CreatedAt, m.ReferenceId
            })
            .ToListAsync(ct);

        var layers = StockAgeCalculator.Compute(
            movements.Select(m => new StockAgeCalculator.Movement(
                m.ProductId, m.LocationId, m.MovementType, m.Quantity,
                IndiaTime.ToIndiaDate(m.OccurredAt), m.OccurredAt, m.CreatedAt, m.ReferenceId)),
            looseIds.ToHashSet());

        var rows = new List<StockAgeRowDto>();

        foreach (var ((productId, locationId), held) in layers)
        {
            if (!products.TryGetValue(productId, out var product) || !locations.TryGetValue(locationId, out var location))
            {
                continue;
            }

            var bands = AgeBands.For(product.ShelfLife);
            var aged = held.Select(l => new StockAgeLayerDto(l.Day, asOf.DayNumber - l.Day.DayNumber, l.Quantity)).ToList();

            decimal In(int from, int to) => aged.Where(l => l.AgeDays >= from && l.AgeDays <= to).Sum(l => l.Quantity);

            rows.Add(new StockAgeRowDto(
                product.Id,
                product.ProductCode,
                product.Name,
                product.UnitCode,
                location.Id,
                location.Code,
                location.Name,
                product.ShelfLife,
                In(int.MinValue, bands.FreshUpTo),
                In(bands.FreshUpTo + 1, bands.RepackUpTo),
                In(bands.RepackUpTo + 1, bands.AgeingUpTo),
                In(bands.AgeingUpTo + 1, bands.ShelfLife),
                In(bands.ShelfLife + 1, int.MaxValue),
                aged.Sum(l => l.Quantity),
                In(bands.ShelfLife - (AgeBands.SoonDays - 1), bands.ShelfLife),
                aged.Min(l => (DateOnly?)l.PackedOn),
                bands.FreshLabel,
                bands.RepackLabel,
                bands.AgeingLabel,
                bands.ExpiringLabel,
                aged));
        }

        return rows
            .OrderBy(r => r.LocationId == KnownStockLocations.MainWarehouseId ? 0 : 1)
            .ThenBy(r => r.LocationCode)
            .ThenBy(r => r.ProductName)
            .ToList();
    }

    public async Task<StockAgeAlertsDto> GetAlertsAsync(CancellationToken ct)
    {
        var rows = await GetAsync(IndiaTime.Today(), ct);

        return new StockAgeAlertsDto(
            rows.Count(r => r.RepackWindow > 0m),
            rows.Count(r => r.ExpiringWithinDays > 0m),
            rows.Count(r => r.Expired > 0m));
    }

    /// <summary>
    /// Records the expired quantity in one place as damage. The oldest layers are the expired ones,
    /// so first-in-first-out takes exactly them and the rest keeps its age.
    /// </summary>
    public async Task<StockEntryResponse> WriteOffExpiredAsync(Guid productId, Guid locationId, CancellationToken ct)
    {
        var row = (await GetAsync(IndiaTime.Today(), ct))
            .FirstOrDefault(r => r.ProductId == productId && r.LocationId == locationId);

        if (row is null || row.Expired <= 0m)
        {
            throw new DomainException("Nothing here has expired, so there is nothing to write off.");
        }

        var newest = row.Layers.Where(l => l.AgeDays > row.ShelfLifeDays).Max(l => l.PackedOn);

        return await stock.AddEntryAsync(
            new CreateStockEntryRequest(
                productId,
                StockMovementType.Damage,
                row.Expired,
                null,
                $"Expired: packed on or before {newest:dd MMM yyyy}, over the {row.ShelfLifeDays}-day shelf life",
                locationId),
            ct);
    }
}
