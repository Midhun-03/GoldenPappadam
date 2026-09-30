using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Tests;

/// <summary>
/// The day on the van: loaded in the morning, sold along the route, emptied at night. The last
/// test is the point of the whole milestone - when the three numbers do not agree, somebody is
/// told rather than the books being quietly tidied up.
/// </summary>
public class VanLoadTests : IAsyncLifetime
{
    private static readonly Guid Warehouse = KnownStockLocations.MainWarehouseId;
    private static readonly Guid Van = KnownStockLocations.FirstVanId;

    private TestDatabase _database = null!;
    private StockService _stock = null!;
    private VanLoadService _vanLoads = null!;
    private InvoiceService _invoices = null!;
    private Product _packet = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _vanLoads = new VanLoadService(_database.Db, _stock);
        _invoices = new InvoiceService(_database.Db, _stock, new CustomerPriceService(_database.Db, _database.CurrentUser));

        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 45m;
        await _database.Db.SaveChangesAsync();

        _packet = packet;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Loading_the_van_takes_stock_off_the_warehouse_and_puts_it_on_the_van()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);

        await LoadAsync(VanLoadDirection.Loading, 150m);

        Assert.Equal(50m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
        Assert.Equal(150m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
    }

    [Fact]
    public async Task The_evening_return_brings_the_rest_back()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);
        await LoadAsync(VanLoadDirection.Loading, 150m);

        await LoadAsync(VanLoadDirection.Return, 150m);

        Assert.Equal(200m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
        Assert.Equal(0m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
    }

    [Fact]
    public async Task A_day_that_adds_up_is_settled()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);
        await LoadAsync(VanLoadDirection.Loading, 150m);
        await SellFromVanAsync(138m);
        await LoadAsync(VanLoadDirection.Return, 12m);

        var day = await _vanLoads.GetReconciliationAsync(Van, IndiaTime.Today(), default);
        var line = Assert.Single(day.Lines);

        Assert.Equal(150m, line.Loaded);
        Assert.Equal(138m, line.Sold);
        Assert.Equal(12m, line.Returned);
        Assert.Equal(0m, line.Unaccounted);
        Assert.True(day.IsSettled);
    }

    [Fact]
    public async Task Two_packets_nobody_can_account_for_are_shown_rather_than_written_off()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);
        await LoadAsync(VanLoadDirection.Loading, 150m);
        await SellFromVanAsync(138m);

        // Only ten came back, so two are missing: an unrecorded sale, or a miscount, or damage.
        // The arithmetic cannot tell which, so it says so and leaves the judgement to a person.
        await LoadAsync(VanLoadDirection.Return, 10m);

        var day = await _vanLoads.GetReconciliationAsync(Van, IndiaTime.Today(), default);
        var line = Assert.Single(day.Lines);

        Assert.Equal(2m, line.Unaccounted);
        Assert.False(day.IsSettled);

        // And the van really is still holding them: the discrepancy is the stock, not a report.
        Assert.Equal(2m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
    }

    [Fact]
    public async Task A_cancelled_van_sale_comes_back_into_the_day()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);
        await LoadAsync(VanLoadDirection.Loading, 150m);

        var bill = await SellFromVanAsync(20m);
        await _invoices.CancelAsync(bill, "Shop refused the delivery", default);

        var day = await _vanLoads.GetReconciliationAsync(Van, IndiaTime.Today(), default);
        var line = Assert.Single(day.Lines);

        Assert.Equal(0m, line.Sold);
        Assert.Equal(150m, line.Unaccounted);
    }

    [Fact]
    public async Task Counting_the_van_settles_the_day()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);
        await LoadAsync(VanLoadDirection.Loading, 150m);
        await SellFromVanAsync(138m);
        await LoadAsync(VanLoadDirection.Return, 10m);

        // The office decides the two were damaged and records it, which is the deliberate
        // correction the automatic version would have made silently.
        await _stock.AddEntryAsync(
            new CreateStockEntryRequest(_packet.Id, StockMovementType.Damage, 2m, null, "Crushed in the van", Van),
            default);

        var day = await _vanLoads.GetReconciliationAsync(Van, IndiaTime.Today(), default);

        Assert.Equal(-2m, Assert.Single(day.Lines).Other);
        Assert.True(day.IsSettled);
    }

    [Fact]
    public async Task What_is_on_the_van_is_what_the_return_hands_back()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);
        await LoadAsync(VanLoadDirection.Loading, 150m);
        await SellFromVanAsync(138m);

        var onVan = await _vanLoads.GetOnVanAsync(Van, default);

        Assert.Equal(12m, Assert.Single(onVan).Quantity);
    }

    [Fact]
    public async Task Loading_a_warehouse_onto_itself_is_refused()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);

        var error = await Assert.ThrowsAsync<DomainException>(() => _vanLoads.CreateAsync(
            new CreateVanLoadRequest(Warehouse, VanLoadDirection.Loading, null, null,
                [new VanLoadLineRequest(_packet.Id, 10m)]),
            default));

        Assert.Contains("not a van", error.Message);
    }

    [Fact]
    public async Task The_same_product_twice_on_one_load_is_refused()
    {
        await _database.AddStockAsync(_packet.Id, 200m, Warehouse);

        await Assert.ThrowsAsync<DomainException>(() => _vanLoads.CreateAsync(
            new CreateVanLoadRequest(Van, VanLoadDirection.Loading, null, null,
                [new VanLoadLineRequest(_packet.Id, 10m), new VanLoadLineRequest(_packet.Id, 5m)]),
            default));
    }

    [Fact]
    public async Task Loading_more_than_the_warehouse_has_warns_but_still_happens()
    {
        await _database.AddStockAsync(_packet.Id, 100m, Warehouse);

        var result = await LoadAsync(VanLoadDirection.Loading, 150m);

        // The packets physically went onto the van, so the record has to say so.
        Assert.Equal(150m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
        Assert.Equal(-50m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
        Assert.Contains(result.Warnings, w => w.Contains("-50"));
    }

    private async Task<CreateVanLoadResponse> LoadAsync(VanLoadDirection direction, decimal quantity) =>
        await _vanLoads.CreateAsync(
            new CreateVanLoadRequest(Van, direction, null, null, [new VanLoadLineRequest(_packet.Id, quantity)]),
            default);

    private async Task<Guid> SellFromVanAsync(decimal quantity)
    {
        var shop = await _database.SeedCustomerAsync($"Shop {Guid.NewGuid():N}");

        var bill = await _invoices.CreateAsync(
            new CreateInvoiceRequest(shop.Id, null, 0m, null,
                [new InvoiceLineRequest(_packet.Id, quantity, null)], Van),
            default);

        return bill.Invoice.Id;
    }
}
