using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.FieldSales.StockRequests;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Mobile;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The promise the whole offline story rests on: send the same thing twice and exactly one bill
/// exists. Everything else here is about never losing what the salesperson actually did.
/// </summary>
public class MobileSyncTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private MobileSyncService _sync = null!;
    private CustomerPriceService _prices = null!;
    private StockService _stock = null!;
    private Product _packet = null!;
    private Customer _shop = null!;
    private Device _device = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _database.CurrentUser.UserId = Guid.NewGuid();
        _stock = new StockService(_database.Db);
        _prices = new CustomerPriceService(_database.Db);

        _sync = new MobileSyncService(
            _database.Db,
            _database.CurrentUser,
            new InvoiceService(_database.Db, _stock, _prices),
            new PaymentService(_database.Db),
            _prices,
            new VanLoadService(_database.Db, _stock),
            new StockRequestService(_database.Db));

        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 45m;
        await _database.Db.SaveChangesAsync();
        _packet = packet;

        _shop = await _database.SeedCustomerAsync("Route Shop");

        // The van the phone rides in, and stock on it to sell.
        await _database.AddStockAsync(_packet.Id, 150m, KnownStockLocations.FirstVanId);

        var registered = await _sync.RegisterAsync(new RegisterDeviceRequest("Nokia", "Android"), default);
        _device = await _database.Db.Devices.FirstAsync(d => d.Id == registered.Id);
        _device.LocationId = KnownStockLocations.FirstVanId;
        await _database.Db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task The_same_sale_sent_twice_makes_one_bill()
    {
        var clientRequestId = Guid.NewGuid();

        var first = await SubmitAsync(SaleItem(clientRequestId, 10m, 45m));
        var second = await SubmitAsync(SaleItem(clientRequestId, 10m, 45m));

        Assert.Equal(SubmissionOutcome.Accepted, first.Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, second.Outcome);

        // Same bill, and only one of them.
        Assert.Equal(first.RecordId, second.RecordId);
        Assert.Equal(1, await _database.Db.Invoices.CountAsync());

        // And the stock only moved once.
        Assert.Equal(140m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
    }

    [Fact]
    public async Task Ten_offline_sales_arrive_together_and_each_lands_once()
    {
        var items = Enumerable.Range(0, 10)
            .Select(_ => SaleItem(Guid.NewGuid(), 5m, 45m))
            .ToList();

        var first = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id, items), default);
        var retry = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id, items), default);

        Assert.All(first.Results, r => Assert.Equal(SubmissionOutcome.Accepted, r.Outcome));
        Assert.All(retry.Results, r => Assert.Equal(SubmissionOutcome.AlreadyAccepted, r.Outcome));
        Assert.Equal(10, await _database.Db.Invoices.CountAsync());
    }

    [Fact]
    public async Task One_bad_item_does_not_stop_the_good_ones_behind_it()
    {
        var good = SaleItem(Guid.NewGuid(), 5m, 45m);
        var bad = SaleItem(Guid.NewGuid(), 5m, 45m) with
        {
            Sale = new MobileSaleRequest(Guid.NewGuid(), [new MobileSaleLineRequest(_packet.Id, 5m, 45m)],
                DateTime.UtcNow, null)
        };
        var alsoGood = SaleItem(Guid.NewGuid(), 5m, 45m);

        var batch = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id, [good, bad, alsoGood]), default);

        Assert.Equal(SubmissionOutcome.Accepted, batch.Results[0].Outcome);
        Assert.Equal(SubmissionOutcome.Rejected, batch.Results[1].Outcome);
        Assert.Equal(SubmissionOutcome.Accepted, batch.Results[2].Outcome);

        // The rejection says why, so the salesperson can be told something useful.
        Assert.Contains("not found", batch.Results[1].Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await _database.Db.Invoices.CountAsync());
    }

    [Fact]
    public async Task A_rejected_item_can_be_sent_again_once_the_problem_is_fixed()
    {
        var clientRequestId = Guid.NewGuid();
        var missingShop = SaleItem(clientRequestId, 5m, 45m) with
        {
            Sale = new MobileSaleRequest(Guid.NewGuid(), [new MobileSaleLineRequest(_packet.Id, 5m, 45m)],
                DateTime.UtcNow, null)
        };

        Assert.Equal(SubmissionOutcome.Rejected, (await SubmitAsync(missingShop)).Outcome);

        // Nothing was written, so the same client id is still free to succeed later.
        var retry = await SubmitAsync(SaleItem(clientRequestId, 5m, 45m));

        Assert.Equal(SubmissionOutcome.Accepted, retry.Outcome);
    }

    [Fact]
    public async Task A_sale_from_the_phone_comes_off_the_van()
    {
        await SubmitAsync(SaleItem(Guid.NewGuid(), 12m, 45m));

        Assert.Equal(138m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
        Assert.Equal(0m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task A_price_changed_while_the_phone_was_away_is_flagged_not_rewritten()
    {
        await _prices.SetAsync(_shop.Id, _packet.Id, 35m, default);

        // The salesperson sold at 35 out on the route; the office moved it to 36 meanwhile.
        await _prices.SetAsync(_shop.Id, _packet.Id, 36m, default);

        var result = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 35m));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        Assert.True(result.PriceMismatch);

        // The bill says what the shop was actually told, not what the price list says now.
        var invoice = await _database.Db.Invoices.Include(i => i.Lines).FirstAsync();
        Assert.Equal(35m, invoice.Lines[0].UnitPrice);
        Assert.Equal(350m, invoice.TotalAmount);
    }

    [Fact]
    public async Task A_sale_at_the_current_price_is_not_flagged()
    {
        await _prices.SetAsync(_shop.Id, _packet.Id, 35m, default);

        var result = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 35m));

        Assert.False(result.PriceMismatch);
    }

    [Fact]
    public async Task Money_collected_against_an_old_balance_needs_no_sale()
    {
        var shop = await _database.SeedCustomerAsync("Owing Shop", openingBalance: 10_000m);

        var result = await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.Payment, DateTime.UtcNow, null,
            new MobilePaymentRequest(shop.Id, 5_000m, PaymentMethod.Cash, null, null), null));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        Assert.Equal(5_000m, await new PaymentService(_database.Db).GetBalanceAsync(shop.Id, default));
    }

    [Fact]
    public async Task A_visit_that_sold_nothing_is_still_recorded()
    {
        var result = await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.Visit, DateTime.UtcNow, null, null,
            new MobileVisitRequest(_shop.Id, VisitOutcome.NoOrder, null, null, "Shop had enough stock")));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);

        var visit = await _database.Db.ShopVisits.FirstAsync();
        Assert.Equal(VisitOutcome.NoOrder, visit.Outcome);
        Assert.Null(visit.InvoiceId);
    }

    [Fact]
    public async Task A_visit_finds_its_sale_by_the_id_the_phone_gave_it()
    {
        var saleClientId = Guid.NewGuid();

        var batch = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id,
        [
            SaleItem(saleClientId, 10m, 45m),
            new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.Visit, DateTime.UtcNow, null, null,
                new MobileVisitRequest(_shop.Id, VisitOutcome.Sold, saleClientId, null, null))
        ]), default);

        var visit = await _database.Db.ShopVisits.FirstAsync();

        Assert.Equal(batch.Results[0].RecordId, visit.InvoiceId);
    }

    [Fact]
    public async Task The_snapshot_carries_what_the_phone_needs_to_work_with_no_signal()
    {
        await _prices.SetAsync(_shop.Id, _packet.Id, 35m, default);

        var snapshot = await _sync.GetSnapshotAsync(default);

        Assert.Contains(snapshot.Customers, c => c.Id == _shop.Id);
        Assert.Contains(snapshot.Products, p => p.Id == _packet.Id);
        Assert.Equal(35m, Assert.Single(snapshot.Prices).UnitPrice);
        Assert.Equal(KnownStockLocations.FirstVanId, snapshot.VanLocationId);
        Assert.Contains("Cash", snapshot.PaymentMethods);
    }

    [Fact]
    public async Task The_balance_in_the_snapshot_is_the_whole_outstanding_not_just_today()
    {
        var shop = await _database.SeedCustomerAsync("Owing Shop", openingBalance: 10_000m);

        var snapshot = await _sync.GetSnapshotAsync(default);

        Assert.Equal(10_000m, snapshot.Customers.Single(c => c.Id == shop.Id).Balance);
    }

    [Fact]
    public async Task A_deactivated_device_cannot_send_anything()
    {
        _device.IsActive = false;
        await _database.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => SubmitAsync(SaleItem(Guid.NewGuid(), 5m, 45m)));
    }

    [Fact]
    public async Task The_salespersons_day_counts_what_they_recorded()
    {
        await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 45m));
        await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.Payment, DateTime.UtcNow, null,
            new MobilePaymentRequest(_shop.Id, 200m, PaymentMethod.Cash, null, null), null));
        await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.Visit, DateTime.UtcNow, null, null,
            new MobileVisitRequest(_shop.Id, VisitOutcome.Sold, null, null, null)));

        var day = await _sync.GetDayAsync(IndiaTime.Today(), default);

        Assert.Equal(450m, day.TotalSales);
        Assert.Equal(1, day.SaleCount);
        Assert.Equal(1, day.ShopsVisited);
        Assert.Equal(200m, day.CashCollected);
    }

    private async Task<SubmissionResultDto> SubmitAsync(SubmissionItemRequest item)
    {
        var batch = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id, [item]), default);

        return batch.Results[0];
    }

    private SubmissionItemRequest SaleItem(Guid clientRequestId, decimal quantity, decimal unitPrice) =>
        new(clientRequestId, SubmissionType.Invoice, DateTime.UtcNow,
            new MobileSaleRequest(_shop.Id, [new MobileSaleLineRequest(_packet.Id, quantity, unitPrice)],
                DateTime.UtcNow, null),
            null, null);
}
