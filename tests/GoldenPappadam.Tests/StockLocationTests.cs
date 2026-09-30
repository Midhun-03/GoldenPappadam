using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Packing;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Tests;

/// <summary>
/// Stock now has a place. The figures that matter are per location: what the warehouse can pack
/// from, and what is actually on the van. The sum across locations is what the business owns.
/// </summary>
public class StockLocationTests : IAsyncLifetime
{
    private static readonly Guid Warehouse = KnownStockLocations.MainWarehouseId;
    private static readonly Guid Van = KnownStockLocations.FirstVanId;

    private TestDatabase _database = null!;
    private StockService _stock = null!;
    private InvoiceService _invoices = null!;
    private PackingService _packing = null!;
    private Product _loose = null!;
    private Product _packet = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _invoices = new InvoiceService(_database.Db, _stock, new CustomerPriceService(_database.Db, _database.CurrentUser));
        _packing = new PackingService(_database.Db, _stock);

        var (loose, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 45m;
        await _database.Db.SaveChangesAsync();

        _loose = loose;
        _packet = packet;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task The_warehouse_and_the_van_hold_their_own_counts()
    {
        await _database.AddStockAsync(_packet.Id, 100m, Warehouse);
        await _database.AddStockAsync(_packet.Id, 30m, Van);

        Assert.Equal(100m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
        Assert.Equal(30m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));

        // Null means everywhere: what the business owns, wherever it happens to be sitting.
        Assert.Equal(130m, await _stock.GetQuantityOnHandAsync(_packet.Id, null, default));
    }

    [Fact]
    public async Task A_sale_from_the_van_comes_off_the_van()
    {
        await _database.AddStockAsync(_packet.Id, 100m, Warehouse);
        await _database.AddStockAsync(_packet.Id, 30m, Van);
        var shop = await _database.SeedCustomerAsync("Route Shop");

        await _invoices.CreateAsync(
            new CreateInvoiceRequest(shop.Id, null, 0m, null,
                [new InvoiceLineRequest(_packet.Id, 12m, null)], Van),
            default);

        Assert.Equal(18m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
        Assert.Equal(100m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
    }

    [Fact]
    public async Task A_bill_that_says_nothing_still_comes_off_the_warehouse()
    {
        await _database.AddStockAsync(_packet.Id, 100m, Warehouse);
        var shop = await _database.SeedCustomerAsync("Counter Customer");

        await _invoices.CreateAsync(
            new CreateInvoiceRequest(shop.Id, null, 0m, null,
                [new InvoiceLineRequest(_packet.Id, 5m, null)]),
            default);

        Assert.Equal(95m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
    }

    [Fact]
    public async Task Cancelling_a_van_sale_puts_the_stock_back_on_the_van()
    {
        await _database.AddStockAsync(_packet.Id, 30m, Van);
        var shop = await _database.SeedCustomerAsync("Route Shop");

        var bill = await _invoices.CreateAsync(
            new CreateInvoiceRequest(shop.Id, null, 0m, null,
                [new InvoiceLineRequest(_packet.Id, 12m, null)], Van),
            default);

        await _invoices.CancelAsync(bill.Invoice.Id, "Shop refused the delivery", default);

        Assert.Equal(30m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
        Assert.Equal(0m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
    }

    [Fact]
    public async Task Packing_happens_at_the_warehouse_whatever_is_on_the_van()
    {
        await _database.AddStockAsync(_loose.Id, 50m, Warehouse);
        await _database.AddStockAsync(_packet.Id, 30m, Van);

        await _packing.CreateAsync(new CreatePackingRequest(_packet.Id, 40m, null, null), default);

        Assert.Equal(40m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
        Assert.Equal(30m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
        Assert.Equal(40m, await _stock.GetQuantityOnHandAsync(_loose.Id, Warehouse, default));
    }

    [Fact]
    public async Task The_van_can_be_counted_and_corrected_like_any_shelf()
    {
        await _database.AddStockAsync(_packet.Id, 30m, Van);

        var result = await _stock.AdjustToCountAsync(
            new AdjustStockRequest(_packet.Id, 28m, null, "Counted at the end of the route", Van), default);

        Assert.Equal(Van, result.LocationId);
        Assert.Equal(28m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
    }

    [Fact]
    public async Task Production_is_refused_on_a_van()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(_loose.Id, StockMovementType.Production, 10m, null, null, Van), default));

        Assert.Contains("warehouse", error.Message);
    }

    [Fact]
    public async Task Opening_stock_can_be_recorded_per_location()
    {
        await _stock.AddEntryAsync(
            new CreateStockEntryRequest(_packet.Id, StockMovementType.Opening, 100m, null, null, Warehouse), default);

        // The van has its own history, so its opening entry is not blocked by the warehouse's.
        await _stock.AddEntryAsync(
            new CreateStockEntryRequest(_packet.Id, StockMovementType.Opening, 30m, null, null, Van), default);

        Assert.Equal(130m, await _stock.GetQuantityOnHandAsync(_packet.Id, null, default));

        // But only once in each place.
        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(_packet.Id, StockMovementType.Opening, 5m, null, null, Van), default));
    }

    [Fact]
    public async Task Where_a_product_is_can_be_seen_at_a_glance()
    {
        await _database.AddStockAsync(_packet.Id, 100m, Warehouse);
        await _database.AddStockAsync(_packet.Id, 30m, Van);

        var byLocation = await _stock.GetByLocationAsync(_packet.Id, default);

        Assert.Equal(100m, byLocation.Single(l => l.Code == "MAIN").QuantityOnHand);
        Assert.Equal(30m, byLocation.Single(l => l.Code == "VAN-1").QuantityOnHand);
    }

    [Fact]
    public async Task A_location_that_does_not_exist_is_refused()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(_packet.Id, StockMovementType.Production, 1m, null, null, Guid.NewGuid()),
            default));
    }

    [Fact]
    public async Task The_movement_history_says_where_each_movement_happened()
    {
        await _database.AddStockAsync(_packet.Id, 100m, Warehouse);
        await _database.AddStockAsync(_packet.Id, 30m, Van);

        var everywhere = await _stock.GetMovementsAsync(_packet.Id, null, null, null, default);
        var vanOnly = await _stock.GetMovementsAsync(_packet.Id, null, null, Van, default);

        Assert.Equal(2, everywhere.Count);
        Assert.Contains(everywhere, m => m.LocationCode == "MAIN");
        Assert.Contains(everywhere, m => m.LocationCode == "VAN-1");

        // The running balance of a single location is that location's own story.
        Assert.Equal(30m, Assert.Single(vanOnly).RunningBalance);
    }
}
