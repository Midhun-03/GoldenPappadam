using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Tests;

public class StockServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private StockService _stock = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Damage_is_stored_as_a_negative_movement()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 20m);

        var result = await _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Damage, 3m, null, "Crushed in transit"), default);

        Assert.Equal(17m, result.QuantityOnHand);
    }

    [Fact]
    public async Task Damage_without_a_note_is_rejected()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Damage, 3m, null, null), default));
    }

    [Fact]
    public async Task Opening_stock_can_only_be_recorded_once()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        await _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Opening, 50m, null, null), default);

        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Opening, 10m, null, null), default));
    }

    [Fact]
    public async Task Sale_movements_cannot_be_entered_by_hand()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Sale, 5m, null, null), default));
    }

    [Fact]
    public async Task Adjustment_records_the_difference_from_the_counted_quantity()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 20m);

        var result = await _stock.AdjustToCountAsync(
            new AdjustStockRequest(loose.Id, 18.5m, null, "Monthly count"), default);

        Assert.Equal(18.5m, result.QuantityOnHand);

        var movements = await _stock.GetMovementsAsync(loose.Id, null, null, KnownStockLocations.MainWarehouseId, default);
        Assert.Equal(-1.5m, movements.Last().Quantity);
        Assert.Equal(StockMovementType.Adjustment, movements.Last().MovementType);
    }

    [Fact]
    public async Task Movement_history_carries_a_running_balance()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 20m);
        await _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Production, 30m, null, null), default);
        await _stock.AddEntryAsync(
            new CreateStockEntryRequest(loose.Id, StockMovementType.Damage, 5m, null, "Broken"), default);

        var movements = await _stock.GetMovementsAsync(loose.Id, null, null, KnownStockLocations.MainWarehouseId, default);

        Assert.Equal([20m, 50m, 45m], movements.Select(m => m.RunningBalance));
    }

    [Fact]
    public async Task Low_stock_is_flagged_against_the_threshold()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        loose.LowStockThreshold = 10m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(loose.Id, 8m);

        var lowStock = await _stock.GetOnHandAsync(null, lowStockOnly: true, includeInactive: false, KnownStockLocations.MainWarehouseId, default);

        Assert.Equal(loose.Id, Assert.Single(lowStock).ProductId);

        var all = await _stock.GetOnHandAsync(null, lowStockOnly: false, includeInactive: false, KnownStockLocations.MainWarehouseId, default);
        Assert.Equal(0m, all.Single(r => r.ProductId == packet.Id).QuantityOnHand);
    }
}
