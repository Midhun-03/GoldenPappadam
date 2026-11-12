using System.Data.Common;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Sales.Settings;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.FieldSales.Day;
using GoldenPappadam.Api.Features.FieldSales.StockRequests;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Mobile;
using GoldenPappadam.Api.Features.Sales.CustomerBranches;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Api.Features.Sales.Returns;
using GoldenPappadam.Api.Features.Sales.RateRequests;
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
        _prices = new CustomerPriceService(_database.Db, _database.CurrentUser);

        _sync = NewSync(_database.Db);

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
        var lastSync = DateTime.UtcNow;

        // The salesperson sold at 35 out on the route; the office moved it to 36 meanwhile.
        await _prices.SetAsync(_shop.Id, _packet.Id, 36m, default);

        var result = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 35m, lastSync));

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

    [Theory]
    [InlineData(30)]
    [InlineData(0)]
    [InlineData(45.5)]
    public async Task A_price_that_was_never_the_shops_rate_refuses_the_whole_bill(double price)
    {
        await _prices.SetAsync(_shop.Id, _packet.Id, 35m, default);
        var clientRequestId = Guid.NewGuid();

        var result = await SubmitAsync(SaleItem(clientRequestId, 10m, (decimal)price, DateTime.UtcNow.AddDays(-30)));

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.Contains("not this shop's rate", result.Error);

        // Nothing was written: no bill, no stock off the van, and the client id is free to be used
        // again once the office has sorted it out.
        Assert.False(await _database.Db.Invoices.AnyAsync());
        Assert.False(await _database.Db.SyncSubmissions.AnyAsync(s => s.ClientRequestId == clientRequestId));
        Assert.Equal(150m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
    }

    [Fact]
    public async Task One_bad_line_refuses_the_lines_beside_it_too()
    {
        var item = new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.Invoice, DateTime.UtcNow,
            new MobileSaleRequest(
                _shop.Id,
                [new MobileSaleLineRequest(_packet.Id, 5m, 45m), new MobileSaleLineRequest(_packet.Id, 5m, 12m)],
                DateTime.UtcNow, null),
            null, null);

        var result = await SubmitAsync(item);

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.False(await _database.Db.Invoices.AnyAsync());
    }

    [Fact]
    public async Task A_rate_the_shop_stopped_paying_before_the_phones_last_sync_is_refused()
    {
        await _prices.SetAsync(_shop.Id, _packet.Id, 35m, default);
        await _prices.SetAsync(_shop.Id, _packet.Id, 36m, default);

        // The phone synced after the change, so it was told 36. It has no honest way to charge 35.
        var result = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 35m, DateTime.UtcNow));

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task A_last_known_rate_is_accepted_however_long_the_phone_was_away()
    {
        await _prices.SetAsync(_shop.Id, _packet.Id, 35m, default);
        var lastSync = DateTime.UtcNow;
        await _prices.SetAsync(_shop.Id, _packet.Id, 36m, default);
        await _prices.SetAsync(_shop.Id, _packet.Id, 38m, default);

        // Two changes behind: 35 was the rate at the last sync, 36 came and went, 38 is the rate now.
        var old = await SubmitAsync(SaleItem(Guid.NewGuid(), 1m, 35m, lastSync));
        var between = await SubmitAsync(SaleItem(Guid.NewGuid(), 1m, 36m, lastSync));
        var current = await SubmitAsync(SaleItem(Guid.NewGuid(), 1m, 38m, lastSync));

        Assert.True(old is { Outcome: SubmissionOutcome.Accepted, PriceMismatch: true });
        Assert.True(between is { Outcome: SubmissionOutcome.Accepted, PriceMismatch: true });
        Assert.True(current is { Outcome: SubmissionOutcome.Accepted, PriceMismatch: false });
    }

    [Fact]
    public async Task A_standard_price_changed_while_the_phone_was_away_is_flagged_not_refused()
    {
        // Route Shop has no agreed rate, so it pays the standard price - 45 when the phone last synced.
        var lastSync = DateTime.UtcNow;
        var products = new GoldenPappadam.Api.Features.Inventory.Products.ProductService(_database.Db);

        await products.UpdateAsync(
            _packet.Id,
            new GoldenPappadam.Api.Features.Inventory.Products.SaveProductRequest(
                _packet.ProductCode, _packet.Name, _packet.CategoryId, _packet.Kind, _packet.UnitOfMeasureId,
                _packet.SourceProductId, _packet.SourceQuantityPerPack, 48m, null),
            default);

        var atTheOldPrice = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 45m, lastSync));
        var atTheNewPrice = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 48m, lastSync));

        Assert.True(atTheOldPrice is { Outcome: SubmissionOutcome.Accepted, PriceMismatch: true });
        Assert.True(atTheNewPrice is { Outcome: SubmissionOutcome.Accepted, PriceMismatch: false });
    }

    [Fact]
    public async Task A_rate_agreed_while_the_phone_was_away_leaves_the_standard_price_honest()
    {
        var lastSync = DateTime.UtcNow;

        // The office agrees 37 with the shop; the phone, still out, bills at the standard 45 it knows.
        await _prices.SetAsync(_shop.Id, _packet.Id, 37m, default);

        var result = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 45m, lastSync));

        Assert.True(result is { Outcome: SubmissionOutcome.Accepted, PriceMismatch: true });
    }

    [Fact]
    public async Task The_snapshot_is_stamped_with_when_it_was_taken()
    {
        var before = DateTime.UtcNow;

        var snapshot = await _sync.GetSnapshotAsync(default);

        // The phone sends this stamp back with every bill; a rate changed after it is "while away".
        Assert.InRange(snapshot.PricesAsOf, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task A_salesmans_discount_inside_the_cap_is_kept_with_his_reason()
    {
        // 10 packets at 45 is 450; the cap is 20%, so up to 90 may come off.
        var result = await SubmitAsync(DiscountedSale(10m, 45m, 50m, "  Old stock, expiring in 3 days  "));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);

        var invoice = await _database.Db.Invoices.Include(i => i.Lines).SingleAsync();
        Assert.Equal(450m, invoice.SubTotal);
        Assert.Equal(50m, invoice.DiscountAmount);
        Assert.Equal(400m, invoice.TotalAmount);
        Assert.Equal("Old stock, expiring in 3 days", invoice.DiscountReason);

        // The rate on the bill is still the real rate; the discount is beside it, not inside it.
        Assert.Equal(45m, invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task The_cap_is_exact_to_the_paisa()
    {
        var atTheCap = await SubmitAsync(DiscountedSale(10m, 45m, 90m, "Bulk order"));
        var onePaisaOver = await SubmitAsync(DiscountedSale(10m, 45m, 90.01m, "Bulk order"));

        Assert.Equal(SubmissionOutcome.Accepted, atTheCap.Outcome);
        Assert.Equal(SubmissionOutcome.Rejected, onePaisaOver.Outcome);
        Assert.Contains("more than 20% of the bill", onePaisaOver.Error);
        Assert.Single(await _database.Db.Invoices.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_discount_from_the_phone_needs_a_reason(string? reason)
    {
        var result = await SubmitAsync(DiscountedSale(10m, 45m, 50m, reason));

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.Contains("needs a reason", result.Error);
        Assert.False(await _database.Db.Invoices.AnyAsync());
    }

    [Fact]
    public async Task A_cap_of_zero_switches_discounts_off_on_the_phone()
    {
        await SetDiscountCapAsync(0m);

        var discounted = await SubmitAsync(DiscountedSale(10m, 45m, 1m, "Regular customer"));
        var plain = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m, 45m));

        Assert.Equal(SubmissionOutcome.Rejected, discounted.Outcome);
        Assert.Contains("switched off", discounted.Error);
        Assert.Equal(SubmissionOutcome.Accepted, plain.Outcome);
    }

    [Fact]
    public async Task The_cap_is_the_one_in_force_when_the_bill_arrives()
    {
        // The phone was told 20% and gave 90 off; the office has since lowered the cap to 10%.
        await SetDiscountCapAsync(10m);

        var result = await SubmitAsync(DiscountedSale(10m, 45m, 90m, "Bulk order"));

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.Contains("more than 10% of the bill", result.Error);
    }

    [Fact]
    public async Task The_snapshot_tells_the_phone_the_cap()
    {
        Assert.Equal(20m, (await _sync.GetSnapshotAsync(default)).DiscountCapPercent);

        await SetDiscountCapAsync(12.5m);

        Assert.Equal(12.5m, (await _sync.GetSnapshotAsync(default)).DiscountCapPercent);
    }

    [Fact]
    public async Task The_office_sees_each_discount_and_its_reason_on_today_on_the_road()
    {
        await SubmitAsync(DiscountedSale(10m, 45m, 50m, "Old stock"));
        await SubmitAsync(SaleItem(Guid.NewGuid(), 2m, 45m));

        var day = await new FieldSalesDayService(_database.Db).GetAsync(IndiaTime.Today(), default);

        Assert.Equal(50m, day.DiscountGiven);
        Assert.Equal(1, day.DiscountedSaleCount);

        // 400 after the discount, plus 90.
        Assert.Equal(490m, day.TotalSales);

        var discounted = Assert.Single(day.Sales, s => s.DiscountAmount > 0m);
        Assert.Equal("Old stock", discounted.DiscountReason);
    }

    [Fact]
    public async Task The_cap_is_a_setting_that_a_save_without_it_leaves_alone()
    {
        var settings = new InvoiceSettingsService(_database.Db);
        var current = await settings.GetAsync(default);

        SaveInvoiceSettingsRequest Request(decimal? cap) => new(
            current.LegalName, current.Address, current.Phone, current.Email, current.Gstin, current.StateCode,
            current.SeriesCode, current.PricesIncludeTax, current.RoundToNearestRupee, current.PaymentTerms,
            current.BankDetails, current.TermsAndConditions, cap);

        Assert.Equal(20m, current.SalesmanDiscountCapPercent);
        Assert.Equal(15m, (await settings.SaveAsync(Request(15m), default)).SalesmanDiscountCapPercent);
        Assert.Equal(15m, (await settings.SaveAsync(Request(null), default)).SalesmanDiscountCapPercent);

        await Assert.ThrowsAsync<DomainException>(() => settings.SaveAsync(Request(101m), default));
        await Assert.ThrowsAsync<DomainException>(() => settings.SaveAsync(Request(-1m), default));
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

    // ---------- a record and the row saying it arrived are saved together, or not at all ----------

    [Fact]
    public async Task A_crash_before_a_sale_is_marked_received_leaves_nothing_and_the_retry_bills_once()
    {
        var clientRequestId = Guid.NewGuid();

        await using (var crashing = _database.NewContext(new CrashBeforeSubmissionIsRecorded()))
        {
            var crash = await Assert.ThrowsAsync<DbUpdateException>(() => NewSync(crashing).SubmitAsync(
                new SubmissionBatchRequest(_device.Id, [SaleItem(clientRequestId, 10m, 45m)]), default));
            Assert.IsType<SimulatedCrashException>(crash.InnerException);
        }

        // Nothing of the sale survived: no bill, no stock off the van, no number used up.
        await using (var after = _database.NewContext())
        {
            Assert.Equal(0, await after.Invoices.CountAsync());
            Assert.Equal(0, await after.SyncSubmissions.CountAsync());
            Assert.Equal(150m, await new StockService(after).GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
        }

        // The phone never heard back, so it sends the sale again: one bill, the first number.
        var retry = await SubmitAsync(SaleItem(clientRequestId, 10m, 45m));
        var again = await SubmitAsync(SaleItem(clientRequestId, 10m, 45m));

        Assert.Equal(SubmissionOutcome.Accepted, retry.Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, again.Outcome);
        Assert.Equal(1, (await _database.NewContext().Invoices.SingleAsync()).SequenceNumber);
        Assert.Equal(140m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
    }

    [Fact]
    public async Task A_crash_before_a_payment_is_marked_received_leaves_nothing_and_the_retry_records_it_once()
    {
        var shop = await _database.SeedCustomerAsync("Owing Shop", openingBalance: 10_000m);
        var payment = PaymentItem(Guid.NewGuid(), shop.Id, 5_000m);

        await using (var crashing = _database.NewContext(new CrashBeforeSubmissionIsRecorded()))
        {
            var crash = await Assert.ThrowsAsync<DbUpdateException>(() =>
                NewSync(crashing).SubmitAsync(new SubmissionBatchRequest(_device.Id, [payment]), default));
            Assert.IsType<SimulatedCrashException>(crash.InnerException);
        }

        Assert.Equal(0, await _database.NewContext().Payments.CountAsync());

        Assert.Equal(SubmissionOutcome.Accepted, (await SubmitAsync(payment)).Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, (await SubmitAsync(payment)).Outcome);
        Assert.Equal(1, await _database.NewContext().Payments.CountAsync());
        Assert.Equal(5_000m, await new PaymentService(_database.NewContext()).GetBalanceAsync(shop.Id, default));
    }

    [Fact]
    public async Task A_crash_before_a_return_is_marked_received_leaves_nothing_and_the_retry_records_it_once()
    {
        var item = ReturnItem(Guid.NewGuid(), replacedFromVan: true);

        await CrashWhileSubmittingAsync(item);

        await using (var after = _database.NewContext())
        {
            Assert.Equal(0, await after.ReturnNotes.CountAsync());
            Assert.Equal(150m, await new StockService(after).GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
        }

        var retry = await SubmitAsync(item);

        Assert.Equal(SubmissionOutcome.Accepted, retry.Outcome);
        Assert.EndsWith("/000001", retry.DocumentNumber);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, (await SubmitAsync(item)).Outcome);
        Assert.Equal(1, await _database.NewContext().ReturnNotes.CountAsync());
        Assert.Equal(148m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
    }

    [Fact]
    public async Task A_crash_before_a_new_shop_is_marked_received_leaves_nothing_and_the_retry_creates_it_once()
    {
        var shopId = Guid.NewGuid();
        var item = new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.Customer, DateTime.UtcNow, null, null, null,
            Customer: new MobileCustomerRequest(shopId, "Found On The Road", null, null, null, false,
                [new MobileRateLine(_packet.Id, 40m)]));

        await CrashWhileSubmittingAsync(item);

        await using (var after = _database.NewContext())
        {
            Assert.False(await after.Customers.AnyAsync(c => c.Id == shopId));
            Assert.Equal(0, await after.CustomerPrices.CountAsync(p => p.CustomerId == shopId));
        }

        // Before the fix this retry was refused: the shop existed, so its first rates looked like a rate change.
        Assert.Equal(SubmissionOutcome.Accepted, (await SubmitAsync(item)).Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, (await SubmitAsync(item)).Outcome);

        await using var db = _database.NewContext();
        Assert.Equal(1, await db.Customers.CountAsync(c => c.Id == shopId));
        Assert.Equal(40m, (await db.CustomerPrices.SingleAsync(p => p.CustomerId == shopId)).UnitPrice);
        Assert.Equal(1, await db.CustomerPriceChanges.CountAsync(c => c.CustomerId == shopId));
    }

    [Fact]
    public async Task A_crash_before_a_van_load_is_marked_received_leaves_nothing_and_the_retry_loads_once()
    {
        await _database.AddStockAsync(_packet.Id, 100m);
        var item = new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.VanLoad, DateTime.UtcNow, null, null, null,
            VanLoad: new MobileVanLoadRequest([new MobileVanLoadLineRequest(_packet.Id, 30m)], null));

        await CrashWhileSubmittingAsync(item);

        Assert.Equal(0, await _database.NewContext().VanLoads.CountAsync());

        Assert.Equal(SubmissionOutcome.Accepted, (await SubmitAsync(item)).Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, (await SubmitAsync(item)).Outcome);
        Assert.Equal(1, await _database.NewContext().VanLoads.CountAsync());
        Assert.Equal(180m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.FirstVanId, default));
        Assert.Equal(70m, await _stock.GetQuantityOnHandAsync(_packet.Id, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task A_return_refused_at_its_replacement_saves_nothing_even_with_items_behind_it()
    {
        // The office stopped making the packet after the phone last synced, so it cannot be replaced.
        await SetActiveAsync(_packet.Id, false);

        var batch = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id,
        [
            ReturnItem(Guid.NewGuid(), replacedFromVan: true),
            PaymentItem(Guid.NewGuid(), _shop.Id, 100m)
        ]), default);

        Assert.Equal(SubmissionOutcome.Rejected, batch.Results[0].Outcome);
        Assert.Equal(SubmissionOutcome.Accepted, batch.Results[1].Outcome);

        // The refused return was not carried in by the payment's save, and used no number.
        Assert.Equal(0, await _database.NewContext().ReturnNotes.CountAsync());

        await SetActiveAsync(_packet.Id, true);
        var next = await SubmitAsync(ReturnItem(Guid.NewGuid(), replacedFromVan: false));

        Assert.Equal(SubmissionOutcome.Accepted, next.Outcome);
        Assert.EndsWith("/000001", next.DocumentNumber);
    }

    [Fact]
    public async Task A_clash_with_a_record_on_file_is_refused_not_reported_as_already_accepted()
    {
        Assert.Equal(SubmissionOutcome.Accepted, (await SubmitAsync(ReturnItem(Guid.NewGuid(), replacedFromVan: false))).Outcome);

        // The return counter falls behind the notes on file (a hand edit, a partly restored backup), so the
        // next return's number is already taken. That is not a repeat of anything the phone sent.
        await _database.Db.Database.ExecuteSqlAsync(
            $"UPDATE sales.InvoiceNumberSequences SET LastNumber = 0 WHERE SeriesCode = {InvoiceSettingsService.ReturnSeries}");

        var clash = await SubmitAsync(ReturnItem(Guid.NewGuid(), replacedFromVan: false));

        // Reporting it as already accepted would make the phone mark it sent and lose it.
        Assert.Equal(SubmissionOutcome.Rejected, clash.Outcome);
        Assert.Null(clash.RecordId);
        Assert.False(string.IsNullOrWhiteSpace(clash.Error));
        Assert.Equal(1, await _database.NewContext().ReturnNotes.CountAsync());
    }

    [Fact]
    public async Task A_phone_bill_takes_the_next_free_number_when_the_counter_has_fallen_behind()
    {
        await SubmitAsync(SaleItem(Guid.NewGuid(), 5m, 45m));

        await _database.Db.Database.ExecuteSqlAsync(
            $"UPDATE sales.InvoiceNumberSequences SET LastNumber = 0 WHERE SeriesCode NOT IN ({InvoiceSettingsService.ReturnSeries}, {InvoiceSettingsService.OwnShopSeries})");

        var second = await SubmitAsync(SaleItem(Guid.NewGuid(), 5m, 45m));

        Assert.Equal(SubmissionOutcome.Accepted, second.Outcome);
        Assert.Equal([1, 2], await _database.NewContext().Invoices.OrderBy(i => i.SequenceNumber).Select(i => i.SequenceNumber).ToListAsync());
    }

    private MobileSyncService NewSync(AppDbContext db)
    {
        var stock = new StockService(db);
        var prices = new CustomerPriceService(db, _database.CurrentUser);

        return new MobileSyncService(
            db,
            _database.CurrentUser,
            new InvoiceService(db, stock, prices),
            new PaymentService(db),
            prices,
            new VanLoadService(db, stock),
            new StockRequestService(db),
            new CustomerService(db),
            new CustomerBranchService(db),
            new ReturnService(db, stock, prices, new PaymentService(db)),
            new RateRequestService(db, new CustomerPriceService(db, _database.CurrentUser), _database.CurrentUser));
    }

    /// <summary>Sends one item through a connection that dies just before the item is marked received.</summary>
    private async Task CrashWhileSubmittingAsync(SubmissionItemRequest item)
    {
        await using var crashing = _database.NewContext(new CrashBeforeSubmissionIsRecorded());

        var crash = await Assert.ThrowsAsync<DbUpdateException>(() =>
            NewSync(crashing).SubmitAsync(new SubmissionBatchRequest(_device.Id, [item]), default));
        Assert.IsType<SimulatedCrashException>(crash.InnerException);
    }

    private async Task SetActiveAsync(Guid productId, bool isActive)
    {
        await using var db = _database.NewContext();
        await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, isActive));
    }

    private SubmissionItemRequest ReturnItem(Guid clientRequestId, bool replacedFromVan) =>
        new(clientRequestId, SubmissionType.Return, DateTime.UtcNow, null, null, null,
            Return: new MobileReturnRequest(_shop.Id, [new MobileReturnLineRequest(_packet.Id, 2m, ReturnReason.Expired)],
                replacedFromVan, null));

    private static SubmissionItemRequest PaymentItem(Guid clientRequestId, Guid customerId, decimal amount) =>
        new(clientRequestId, SubmissionType.Payment, DateTime.UtcNow, null,
            new MobilePaymentRequest(customerId, amount, PaymentMethod.Cash, null, null), null);

    private sealed class SimulatedCrashException() : Exception("The server stopped before the submission was recorded.");

    /// <summary>
    /// Fails the insert of the submission row, the way the process dying between the two saves would: the
    /// record has been written by then, and nothing after this point runs. EF reports it wrapped in a
    /// DbUpdateException, which the sync does not mistake for a duplicate because it carries no SQL error.
    /// </summary>
    private sealed class CrashBeforeSubmissionIsRecorded : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfRecordingSubmission(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfRecordingSubmission(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private static void ThrowIfRecordingSubmission(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO [fieldsales].[SyncSubmissions]"))
            {
                throw new SimulatedCrashException();
            }
        }
    }

    private async Task<SubmissionResultDto> SubmitAsync(SubmissionItemRequest item)
    {
        var batch = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id, [item]), default);

        return batch.Results[0];
    }

    private SubmissionItemRequest DiscountedSale(decimal quantity, decimal unitPrice, decimal discount, string? reason) =>
        new(Guid.NewGuid(), SubmissionType.Invoice, DateTime.UtcNow,
            new MobileSaleRequest(_shop.Id, [new MobileSaleLineRequest(_packet.Id, quantity, unitPrice)],
                DateTime.UtcNow, null, null, discount, reason),
            null, null);

    private async Task SetDiscountCapAsync(decimal percent)
    {
        var settings = await _database.Db.InvoiceSettings.SingleAsync();
        settings.SalesmanDiscountCapPercent = percent;
        await _database.Db.SaveChangesAsync();
    }

    /// <summary>pricesAsOf is when the phone last synced; left out, it synced a moment ago.</summary>
    private SubmissionItemRequest SaleItem(
        Guid clientRequestId, decimal quantity, decimal unitPrice, DateTime? pricesAsOf = null) =>
        new(clientRequestId, SubmissionType.Invoice, DateTime.UtcNow,
            new MobileSaleRequest(_shop.Id, [new MobileSaleLineRequest(_packet.Id, quantity, unitPrice)],
                pricesAsOf ?? DateTime.UtcNow, null),
            null, null);
}
