using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.FieldSales.StockRequests;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Mobile;
using GoldenPappadam.Api.Features.Sales.CustomerBranches;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Api.Features.Sales.Returns;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The salesman takes 350 of the 500 that were packed and writes it in his book. Now he writes it
/// in the app instead - and that is the only stock he can move: from the warehouse onto his own van.
/// Plus what he asks the packing unit to make for tomorrow.
/// </summary>
public class SalespersonVanAndOrdersTests : IAsyncLifetime
{
    private static readonly Guid Warehouse = KnownStockLocations.MainWarehouseId;
    private static readonly Guid Van = KnownStockLocations.FirstVanId;

    private TestDatabase _database = null!;
    private MobileSyncService _sync = null!;
    private StockService _stock = null!;
    private StockRequestService _requests = null!;
    private VanLoadService _vanLoads = null!;
    private Product _packet = null!;
    private Customer _shop = null!;
    private Device _device = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _database.CurrentUser.UserId = Guid.NewGuid();

        _stock = new StockService(_database.Db);
        _requests = new StockRequestService(_database.Db);
        _vanLoads = new VanLoadService(_database.Db, _stock);

        _sync = new MobileSyncService(
            _database.Db,
            _database.CurrentUser,
            new InvoiceService(_database.Db, _stock, new CustomerPriceService(_database.Db)),
            new PaymentService(_database.Db),
            new CustomerPriceService(_database.Db),
            _vanLoads,
            _requests,
            new CustomerService(_database.Db),
            new CustomerBranchService(_database.Db),
            new ReturnService(_database.Db, _stock, new CustomerPriceService(_database.Db), new PaymentService(_database.Db)));

        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 45m;
        await _database.Db.SaveChangesAsync();
        _packet = packet;

        _shop = await _database.SeedCustomerAsync("Route Shop");

        // The packing unit made 500 this morning.
        await _database.AddStockAsync(_packet.Id, 500m, Warehouse);

        var registered = await _sync.RegisterAsync(new RegisterDeviceRequest("Nokia", "Android"), default);
        _device = await _database.Db.Devices.FirstAsync(d => d.Id == registered.Id);
        _device.LocationId = Van;
        await _database.Db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    // ---------- van stock ----------

    [Fact]
    public async Task The_salesman_records_what_he_actually_took()
    {
        var result = await SubmitAsync(VanLoadItem(Guid.NewGuid(), 350m));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);

        // 500 were packed; he took 350, so 150 are still at the warehouse.
        Assert.Equal(350m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
        Assert.Equal(150m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
    }

    [Fact]
    public async Task A_load_the_salesman_entered_says_so_for_the_office()
    {
        await SubmitAsync(VanLoadItem(Guid.NewGuid(), 350m));

        var load = await _vanLoads.GetAllAsync(Van, null, null, default);

        // This is the office's notification: the entry names the phone it came from.
        Assert.Equal("Nokia", Assert.Single(load).DeviceName);
    }

    [Fact]
    public async Task A_load_the_office_entered_names_no_phone()
    {
        await _vanLoads.CreateAsync(
            new CreateVanLoadRequest(Van, VanLoadDirection.Loading, null, null,
                [new VanLoadLineRequest(_packet.Id, 350m)]),
            default);

        Assert.Null(Assert.Single(await _vanLoads.GetAllAsync(Van, null, null, default)).DeviceName);
    }

    [Fact]
    public async Task Sending_the_same_load_twice_moves_the_stock_once()
    {
        var clientRequestId = Guid.NewGuid();

        var first = await SubmitAsync(VanLoadItem(clientRequestId, 350m));
        var second = await SubmitAsync(VanLoadItem(clientRequestId, 350m));

        Assert.Equal(SubmissionOutcome.Accepted, first.Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted, second.Outcome);
        Assert.Equal(350m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
    }

    [Fact]
    public async Task A_phone_with_no_van_cannot_load_one()
    {
        _device.LocationId = null;
        await _database.Db.SaveChangesAsync();

        var result = await SubmitAsync(VanLoadItem(Guid.NewGuid(), 350m));

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.Contains("not assigned to a van", result.Error!);
    }

    [Fact]
    public async Task A_phone_with_no_van_cannot_bill_a_shop()
    {
        _device.LocationId = null;
        await _database.Db.SaveChangesAsync();

        var result = await SubmitAsync(SaleItem(Guid.NewGuid(), 10m));

        // A bill follows the goods and the goods come off the van, so with no van there is nothing
        // to have delivered. Refusing beats quietly taking it off the warehouse, which would
        // balance the books and leave the van's wrong.
        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.Contains("not assigned to a van", result.Error!);
        Assert.Empty(await _database.Db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task Money_can_still_be_collected_without_a_van()
    {
        _device.LocationId = null;
        await _database.Db.SaveChangesAsync();

        // Settling last week's bills moves no product, so it has nothing to do with the van.
        var payment = await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.Payment, DateTime.UtcNow, null,
            new MobilePaymentRequest(_shop.Id, 500m, PaymentMethod.Cash, null, null), null));

        var visit = await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.Visit, DateTime.UtcNow, null, null,
            new MobileVisitRequest(_shop.Id, VisitOutcome.NoOrder, null, null, null)));

        var request = await SubmitAsync(StockRequestItem(Guid.NewGuid(), IndiaTime.Today().AddDays(1), 250m));

        Assert.Equal(SubmissionOutcome.Accepted, payment.Outcome);
        Assert.Equal(SubmissionOutcome.Accepted, visit.Outcome);
        Assert.Equal(SubmissionOutcome.Accepted, request.Outcome);
    }

    [Fact]
    public async Task Assigning_the_van_is_what_unblocks_the_load()
    {
        // Straight off a real phone: the app said "not assigned to a van yet" and the load failed.
        _device.LocationId = null;
        await _database.Db.SaveChangesAsync();

        var clientRequestId = Guid.NewGuid();
        var refused = await SubmitAsync(VanLoadItem(clientRequestId, 350m));
        Assert.Equal(SubmissionOutcome.Rejected, refused.Outcome);

        // The office assigns the van, the salesperson taps "try again", and it goes through - the
        // rejection wrote nothing, so the same client id is still free.
        _device.LocationId = Van;
        await _database.Db.SaveChangesAsync();

        var accepted = await SubmitAsync(VanLoadItem(clientRequestId, 350m));

        Assert.Equal(SubmissionOutcome.Accepted, accepted.Outcome);
        Assert.Equal(350m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
    }

    [Fact]
    public async Task The_van_screen_shows_loaded_sold_and_what_is_left()
    {
        await SubmitAsync(VanLoadItem(Guid.NewGuid(), 350m));
        await SellFromVanAsync(138m);

        var van = await _sync.GetVanStockAsync(IndiaTime.Today(), default);
        var line = Assert.Single(van.Lines);

        Assert.Equal(350m, line.Loaded);
        Assert.Equal(138m, line.Sold);
        Assert.Equal(212m, line.Unaccounted);
    }

    // ---------- the day summary ----------

    [Fact]
    public async Task The_day_separates_credit_from_cash_and_counts_the_empty_stops()
    {
        await SubmitAsync(VanLoadItem(Guid.NewGuid(), 350m));

        // One delivery of 450, settled on the spot.
        var paidSale = Guid.NewGuid();
        await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id,
        [
            SaleItem(paidSale, 10m),
            new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.Payment, DateTime.UtcNow, null,
                new MobilePaymentRequest(_shop.Id, 450m, PaymentMethod.Cash, null, null), null)
        ]), default);

        // Another of 450, left on credit.
        var creditShop = await _database.SeedCustomerAsync("Credit Shop");
        await SubmitAsync(new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.Invoice, DateTime.UtcNow,
            new MobileSaleRequest(creditShop.Id, [new MobileSaleLineRequest(_packet.Id, 10m, 45m)],
                DateTime.UtcNow, null),
            null, null));

        // And a shop that needed nothing.
        var quietShop = await _database.SeedCustomerAsync("Quiet Shop");
        await SubmitAsync(new SubmissionItemRequest(Guid.NewGuid(), SubmissionType.Visit, DateTime.UtcNow,
            null, null, new MobileVisitRequest(quietShop.Id, VisitOutcome.NoOrder, null, null, null)));

        var day = await _sync.GetDayAsync(IndiaTime.Today(), default);

        Assert.Equal(900m, day.TotalSales);
        Assert.Equal(2, day.SaleCount);
        Assert.Equal(450m, day.CashCollected);
        Assert.Equal(450m, day.CreditSales);
        Assert.Equal(1, day.NoSaleVisits);
        Assert.Equal(450m, day.TotalOutstanding);
    }

    // ---------- payment history on the phone ----------

    [Fact]
    public async Task The_snapshot_carries_recent_payments_so_the_shop_page_works_offline()
    {
        await new PaymentService(_database.Db).CreateAsync(
            new CreatePaymentRequest(_shop.Id, null, 5_000m, PaymentMethod.UPI, "UPI-8811", null, null),
            default);

        var snapshot = await _sync.GetSnapshotAsync(default);
        var payment = Assert.Single(snapshot.Payments);

        Assert.Equal(_shop.Id, payment.CustomerId);
        Assert.Equal(5_000m, payment.Amount);
        Assert.Equal("UPI", payment.Method);
        Assert.Equal("UPI-8811", payment.Reference);
    }

    // ---------- stock requests ----------

    [Fact]
    public async Task The_salesman_asks_the_packing_unit_for_tomorrow()
    {
        var tomorrow = IndiaTime.Today().AddDays(1);

        var result = await SubmitAsync(StockRequestItem(Guid.NewGuid(), tomorrow, 250m));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);

        var request = Assert.Single(await _requests.GetAllAsync(null, null, null, default));
        Assert.Equal(tomorrow, request.RequiredDate);
        Assert.Equal(StockRequestStatus.Requested, request.Status);
        Assert.Equal(250m, Assert.Single(request.Lines).Quantity);
        Assert.Equal("Nokia", request.DeviceName);
    }

    [Fact]
    public async Task Asking_twice_because_the_signal_dropped_asks_once()
    {
        var clientRequestId = Guid.NewGuid();
        var tomorrow = IndiaTime.Today().AddDays(1);

        Assert.Equal(SubmissionOutcome.Accepted,
            (await SubmitAsync(StockRequestItem(clientRequestId, tomorrow, 250m))).Outcome);
        Assert.Equal(SubmissionOutcome.AlreadyAccepted,
            (await SubmitAsync(StockRequestItem(clientRequestId, tomorrow, 250m))).Outcome);

        Assert.Single(await _requests.GetAllAsync(null, null, null, default));
    }

    [Fact]
    public async Task The_packing_unit_sees_one_figure_per_product_per_day()
    {
        var tomorrow = IndiaTime.Today().AddDays(1);

        await SubmitAsync(StockRequestItem(Guid.NewGuid(), tomorrow, 250m));
        await SubmitAsync(StockRequestItem(Guid.NewGuid(), tomorrow, 120m));

        var needs = Assert.Single(await _requests.GetPackingNeedsAsync(null, null, default));

        Assert.Equal(370m, needs.Quantity);
        Assert.Equal(2, needs.RequestCount);
        Assert.Equal(tomorrow, needs.RequiredDate);
    }

    [Fact]
    public async Task A_cancelled_request_is_not_something_to_pack()
    {
        var tomorrow = IndiaTime.Today().AddDays(1);
        await SubmitAsync(StockRequestItem(Guid.NewGuid(), tomorrow, 250m));

        var request = Assert.Single(await _requests.GetAllAsync(null, null, null, default));
        await _requests.SetStatusAsync(request.Id, StockRequestStatus.Cancelled, default);

        Assert.Empty(await _requests.GetPackingNeedsAsync(null, null, default));
    }

    [Fact]
    public async Task A_request_for_a_product_that_does_not_exist_is_refused_and_writes_nothing()
    {
        var result = await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.StockRequest, DateTime.UtcNow, null, null, null, null,
            new MobileStockRequestRequest(IndiaTime.Today().AddDays(1),
                [new MobileStockRequestLineRequest(Guid.NewGuid(), 10m)], null)));

        Assert.Equal(SubmissionOutcome.Rejected, result.Outcome);
        Assert.Empty(await _requests.GetAllAsync(null, null, null, default));
    }

    [Fact]
    public async Task One_request_can_carry_several_products_for_the_same_day()
    {
        var (loose, _) = (await _database.Db.Products.FirstAsync(p => p.Kind == ProductKind.Loose), 0);
        var tomorrow = IndiaTime.Today().AddDays(1);

        await SubmitAsync(new SubmissionItemRequest(
            Guid.NewGuid(), SubmissionType.StockRequest, DateTime.UtcNow, null, null, null, null,
            new MobileStockRequestRequest(tomorrow,
            [
                new MobileStockRequestLineRequest(_packet.Id, 250m),
                new MobileStockRequestLineRequest(loose.Id, 40m)
            ], "Pepper sells well on Fridays")));

        var request = Assert.Single(await _requests.GetAllAsync(null, null, null, default));

        Assert.Equal(2, request.Lines.Count);
        Assert.Equal("Pepper sells well on Fridays", request.Notes);
    }

    // ---------- helpers ----------

    // ---------- returns collected on the road ----------

    [Fact]
    public async Task Packets_collected_with_nothing_given_wait_for_the_office()
    {
        var result = await SubmitAsync(ReturnItem(Guid.NewGuid(), 4m, replacedFromVan: false));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        Assert.StartsWith("RN/", result.DocumentNumber);

        var note = await _database.Db.ReturnNotes.Include(r => r.Lines).SingleAsync();
        Assert.Equal(ReturnSettlement.Pending, note.Settlement);
        Assert.Equal(0m, note.CreditAmount);

        // Valued at what the shop pays, decided by the server: the phone sent no rate at all.
        Assert.Equal(45m, Assert.Single(note.Lines).UnitRate);

        // Nothing went back into stock: returned packets are never resold.
        Assert.Equal(500m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
    }

    [Fact]
    public async Task Fresh_packets_handed_over_come_off_this_phones_van()
    {
        await SubmitAsync(VanLoadItem(Guid.NewGuid(), 100m));

        var result = await SubmitAsync(ReturnItem(Guid.NewGuid(), 6m, replacedFromVan: true));

        Assert.Equal(SubmissionOutcome.Accepted, result.Outcome);
        Assert.Equal(94m, await _stock.GetQuantityOnHandAsync(_packet.Id, Van, default));
        Assert.Equal(400m, await _stock.GetQuantityOnHandAsync(_packet.Id, Warehouse, default));
        Assert.Equal(ReturnSettlement.Replacement, (await _database.Db.ReturnNotes.SingleAsync()).Settlement);

        // The evening count names them, so the van still reconciles without calling it a correction.
        var line = Assert.Single((await _sync.GetVanStockAsync(IndiaTime.Today(), default)).Lines);
        Assert.Equal(6m, line.Replaced);
        Assert.Equal(0m, line.Other);
        Assert.Equal(94m, line.Unaccounted);
    }

    [Fact]
    public async Task A_phone_with_no_van_can_collect_but_cannot_replace()
    {
        _device.LocationId = null;
        await _database.Db.SaveChangesAsync();

        var collected = await SubmitAsync(ReturnItem(Guid.NewGuid(), 2m, replacedFromVan: false));
        var replaced = await SubmitAsync(ReturnItem(Guid.NewGuid(), 2m, replacedFromVan: true));

        Assert.Equal(SubmissionOutcome.Accepted, collected.Outcome);
        Assert.Equal(SubmissionOutcome.Rejected, replaced.Outcome);
        Assert.Contains("not assigned to a van", replaced.Error!);
        Assert.Equal(1, await _database.Db.ReturnNotes.CountAsync());
    }

    [Fact]
    public async Task Sending_the_same_return_twice_records_it_once_with_the_same_number()
    {
        var clientRequestId = Guid.NewGuid();

        var first = await SubmitAsync(ReturnItem(clientRequestId, 3m, replacedFromVan: false));
        var second = await SubmitAsync(ReturnItem(clientRequestId, 3m, replacedFromVan: false));

        Assert.Equal(SubmissionOutcome.AlreadyAccepted, second.Outcome);
        Assert.Equal(first.DocumentNumber, second.DocumentNumber);
        Assert.Equal(1, await _database.Db.ReturnNotes.CountAsync());
    }

    private async Task<SubmissionResultDto> SubmitAsync(SubmissionItemRequest item)
    {
        var batch = await _sync.SubmitAsync(new SubmissionBatchRequest(_device.Id, [item]), default);

        return batch.Results[0];
    }

    private SubmissionItemRequest ReturnItem(Guid clientRequestId, decimal quantity, bool replacedFromVan) =>
        new(clientRequestId, SubmissionType.Return, DateTime.UtcNow, null, null, null,
            Return: new MobileReturnRequest(
                _shop.Id, [new MobileReturnLineRequest(_packet.Id, quantity, ReturnReason.Expired)], replacedFromVan, null));

    private SubmissionItemRequest VanLoadItem(Guid clientRequestId, decimal quantity) =>
        new(clientRequestId, SubmissionType.VanLoad, DateTime.UtcNow, null, null, null,
            new MobileVanLoadRequest([new MobileVanLoadLineRequest(_packet.Id, quantity)], null));

    private SubmissionItemRequest StockRequestItem(Guid clientRequestId, DateOnly requiredDate, decimal quantity) =>
        new(clientRequestId, SubmissionType.StockRequest, DateTime.UtcNow, null, null, null, null,
            new MobileStockRequestRequest(requiredDate,
                [new MobileStockRequestLineRequest(_packet.Id, quantity)], null));

    private SubmissionItemRequest SaleItem(Guid clientRequestId, decimal quantity) =>
        new(clientRequestId, SubmissionType.Invoice, DateTime.UtcNow,
            new MobileSaleRequest(_shop.Id, [new MobileSaleLineRequest(_packet.Id, quantity, 45m)],
                DateTime.UtcNow, null),
            null, null);

    private async Task SellFromVanAsync(decimal quantity) =>
        await SubmitAsync(SaleItem(Guid.NewGuid(), quantity));
}
