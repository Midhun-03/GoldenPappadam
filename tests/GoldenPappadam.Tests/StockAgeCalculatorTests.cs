using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using static GoldenPappadam.Api.Features.Inventory.Stock.StockAgeCalculator;

namespace GoldenPappadam.Tests;

/// <summary>
/// The age rules on their own: first in, first out; a packet keeps its packing date when it travels
/// on the van; repacked packets start fresh. No database - every rule is plain arithmetic.
/// </summary>
public class StockAgeCalculatorTests
{
    private static readonly Guid Packet = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid Loose = Guid.NewGuid();
    private static readonly Guid Warehouse = KnownStockLocations.MainWarehouseId;
    private static readonly Guid Van = KnownStockLocations.FirstVanId;
    private static readonly DateOnly Day1 = new(2026, 9, 1);

    private static int _clock;

    private static Movement M(Guid product, Guid location, StockMovementType type, decimal quantity, int day, Guid? reference = null)
    {
        // Each call is a later moment, except where a test deliberately shares one (see Pair).
        var at = Day1.AddDays(day).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddSeconds(++_clock);
        return new Movement(product, location, type, quantity, Day1.AddDays(day), at, at, reference);
    }

    /// <summary>Both halves of one operation, saved together: same timestamps.</summary>
    private static Movement[] Together(params Movement[] halves) =>
        halves.Select(h => h with { OccurredAt = halves[0].OccurredAt, CreatedAt = halves[0].CreatedAt }).ToArray();

    private static List<Layer> Held(IEnumerable<Movement> movements, Guid product, Guid location) =>
        Compute(movements, new HashSet<Guid> { Loose }).GetValueOrDefault((product, location)) ?? [];

    [Fact]
    public void The_oldest_packets_go_first()
    {
        var held = Held(
        [
            M(Packet, Warehouse, StockMovementType.Packing, 10m, 0),
            M(Packet, Warehouse, StockMovementType.Packing, 10m, 4),
            M(Packet, Warehouse, StockMovementType.Sale, -12m, 6)
        ], Packet, Warehouse);

        var left = Assert.Single(held);
        Assert.Equal(Day1.AddDays(4), left.Day);
        Assert.Equal(8m, left.Quantity);
    }

    [Fact]
    public void A_packet_keeps_its_packing_date_on_the_van_and_back()
    {
        var load = Guid.NewGuid();
        var unload = Guid.NewGuid();

        var movements = new List<Movement> { M(Packet, Warehouse, StockMovementType.Packing, 10m, 0) };
        movements.AddRange(Together(
            M(Packet, Warehouse, StockMovementType.Transfer, -6m, 3, load),
            M(Packet, Van, StockMovementType.Transfer, 6m, 3, load)));
        movements.Add(M(Packet, Warehouse, StockMovementType.Packing, 5m, 5));
        movements.AddRange(Together(
            M(Packet, Van, StockMovementType.Transfer, -2m, 6, unload),
            M(Packet, Warehouse, StockMovementType.Transfer, 2m, 6, unload)));

        var van = Assert.Single(Held(movements, Packet, Van));
        Assert.Equal(Day1, van.Day);
        Assert.Equal(4m, van.Quantity);

        // The two that came back are still the oldest in the warehouse, not "new" stock.
        var warehouse = Held(movements, Packet, Warehouse);
        Assert.Equal([(Day1, 6m), (Day1.AddDays(5), 5m)], warehouse.Select(l => (l.Day, l.Quantity)));
    }

    [Fact]
    public void Even_if_the_arriving_half_is_listed_first_it_still_carries_the_packing_date()
    {
        var load = Guid.NewGuid();
        var halves = Together(
            M(Packet, Van, StockMovementType.Transfer, 3m, 2, load),
            M(Packet, Warehouse, StockMovementType.Transfer, -3m, 2, load));

        var van = Held([M(Packet, Warehouse, StockMovementType.Packing, 3m, 0), .. halves], Packet, Van);

        Assert.Equal(Day1, Assert.Single(van).Day);
    }

    [Fact]
    public void A_cancelled_bill_puts_back_the_very_packets_it_took()
    {
        var bill = Guid.NewGuid();
        var held = Held(
        [
            M(Packet, Warehouse, StockMovementType.Packing, 5m, 0),
            M(Packet, Warehouse, StockMovementType.Packing, 5m, 7),
            M(Packet, Warehouse, StockMovementType.Sale, -4m, 8, bill),
            M(Packet, Warehouse, StockMovementType.SaleReversal, 4m, 9, bill)
        ], Packet, Warehouse);

        Assert.Equal([(Day1, 5m), (Day1.AddDays(7), 5m)], held.Select(l => (l.Day, l.Quantity)));
    }

    [Fact]
    public void Stock_that_went_below_zero_is_made_good_by_the_next_packing()
    {
        var held = Held(
        [
            M(Packet, Warehouse, StockMovementType.Sale, -5m, 0),
            M(Packet, Warehouse, StockMovementType.Packing, 12m, 2)
        ], Packet, Warehouse);

        Assert.Equal(7m, Assert.Single(held).Quantity);
    }

    [Fact]
    public void Repacked_packets_start_fresh_and_loose_left_over_keeps_its_age()
    {
        var entry = Guid.NewGuid();
        var movements = new List<Movement> { M(Packet, Warehouse, StockMovementType.Packing, 5m, 0) };
        movements.AddRange(Together(
            M(Packet, Warehouse, StockMovementType.Repacking, -5m, 8, entry),
            M(Other, Warehouse, StockMovementType.Repacking, 16m, 8, entry),
            M(Loose, Warehouse, StockMovementType.Repacking, 4m, 8, entry)));

        Assert.Empty(Held(movements, Packet, Warehouse));
        Assert.Equal(Day1.AddDays(8), Assert.Single(Held(movements, Other, Warehouse)).Day);
        Assert.Equal(Day1, Assert.Single(Held(movements, Loose, Warehouse)).Day);
    }

    [Fact]
    public void Repacking_into_the_same_size_keeps_the_count_and_resets_the_age()
    {
        var entry = Guid.NewGuid();
        var movements = new List<Movement> { M(Packet, Warehouse, StockMovementType.Packing, 5m, 0) };
        movements.AddRange(Together(
            M(Packet, Warehouse, StockMovementType.Repacking, -5m, 9, entry),
            M(Packet, Warehouse, StockMovementType.Repacking, 5m, 9, entry)));

        var layer = Assert.Single(Held(movements, Packet, Warehouse));
        Assert.Equal((Day1.AddDays(9), 5m), (layer.Day, layer.Quantity));
    }

    [Theory]
    [InlineData(20, 4, 10, 15)]
    [InlineData(30, 7, 15, 23)]
    public void The_age_bands_follow_the_shelf_life(int life, int fresh, int repack, int ageing)
    {
        var bands = AgeBands.For(life);
        Assert.Equal((fresh, repack, ageing, life), (bands.FreshUpTo, bands.RepackUpTo, bands.AgeingUpTo, bands.ShelfLife));
    }
}
