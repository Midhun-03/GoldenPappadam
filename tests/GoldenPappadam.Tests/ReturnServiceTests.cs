using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Reports;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Api.Features.Sales.Returns;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Returns (owner, 2026-09-25): only expired or damaged packets come back, they are never resold,
/// and the shop gets a replacement, a credit or nothing, as the office decides.
/// </summary>
public class ReturnServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 9, 20);

    private TestDatabase _database = null!;
    private InvoiceService _invoices = null!;
    private PaymentService _payments = null!;
    private StockService _stock = null!;
    private ReturnService _returns = null!;
    private Guid _productId;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        var prices = new CustomerPriceService(_database.Db, _database.CurrentUser);
        _invoices = new InvoiceService(_database.Db, _stock, prices);
        _payments = new PaymentService(_database.Db);
        _returns = new ReturnService(_database.Db, _stock, prices, _payments);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_replacement_takes_fresh_stock_out_and_leaves_the_balance_alone()
    {
        var customer = await SetUpAsync();
        await BillAsync(customer.Id, 10m, 30m);
        var stockBefore = await _stock.GetQuantityOnHandAsync(_productId, KnownStockLocations.MainWarehouseId, default);

        var result = await ReturnAsync(customer.Id, 4m, ReturnSettlement.Replacement);

        Assert.Equal(stockBefore - 4m, await _stock.GetQuantityOnHandAsync(_productId, KnownStockLocations.MainWarehouseId, default));
        Assert.Equal(300m, await _payments.GetBalanceAsync(customer.Id, default));
        Assert.Equal(120m, result.Return.Value);
        Assert.Equal(0m, result.Return.CreditAmount);
        Assert.Equal("Main warehouse", result.Return.ReplacementFrom);
    }

    [Fact]
    public async Task A_replacement_can_come_off_the_van()
    {
        var customer = await SetUpAsync();
        await _database.AddStockAsync(_productId, 20m, KnownStockLocations.FirstVanId);

        await ReturnAsync(customer.Id, 5m, ReturnSettlement.Replacement, replacementFrom: KnownStockLocations.FirstVanId);

        Assert.Equal(15m, await _stock.GetQuantityOnHandAsync(_productId, KnownStockLocations.FirstVanId, default));
    }

    [Fact]
    public async Task Returned_packets_are_never_put_back_into_stock()
    {
        var customer = await SetUpAsync();
        var before = await _database.Db.StockMovements.CountAsync();

        await ReturnAsync(customer.Id, 4m, ReturnSettlement.NoCompensation);

        Assert.Equal(before, await _database.Db.StockMovements.CountAsync());
        Assert.Equal(0m, await _payments.GetBalanceAsync(customer.Id, default));
    }

    [Fact]
    public async Task A_credit_is_valued_at_the_shops_own_rate_and_settles_the_oldest_bill()
    {
        var customer = await SetUpAsync();
        await new CustomerPriceService(_database.Db, _database.CurrentUser).SetAsync(customer.Id, _productId, 25m, default);
        var older = await BillAsync(customer.Id, 4m, 25m, Day.AddDays(-5));
        var newer = await BillAsync(customer.Id, 4m, 25m, Day.AddDays(-1));

        var result = await ReturnAsync(customer.Id, 6m, ReturnSettlement.Credit);

        Assert.Equal(25m, Assert.Single(result.Return.Lines).UnitRate);
        Assert.Equal(150m, result.Return.CreditAmount);
        Assert.Equal(50m, await _payments.GetBalanceAsync(customer.Id, default));
        Assert.Equal(0m, (await _invoices.GetDetailAsync(older.Id, default)).Outstanding);
        Assert.Equal(50m, (await _invoices.GetDetailAsync(newer.Id, default)).Outstanding);

        var credit = await _database.Db.Payments.SingleAsync(p => p.Method == PaymentMethod.ReturnCredit);
        Assert.Equal(result.Return.ReturnNumber, credit.Reference);
    }

    [Fact]
    public async Task The_office_can_credit_a_different_amount()
    {
        var customer = await SetUpAsync();
        await BillAsync(customer.Id, 10m, 30m);

        var result = await ReturnAsync(customer.Id, 4m, ReturnSettlement.Credit, creditAmount: 100m);

        Assert.Equal(120m, result.Return.Value);
        Assert.Equal(100m, result.Return.CreditAmount);
        Assert.Equal(200m, await _payments.GetBalanceAsync(customer.Id, default));
    }

    [Fact]
    public async Task A_credit_is_not_money_collected()
    {
        var customer = await SetUpAsync();
        await BillAsync(customer.Id, 10m, 30m);
        await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, Day, 100m, PaymentMethod.Cash, null, null, null), default);
        await ReturnAsync(customer.Id, 2m, ReturnSettlement.Credit);

        var collections = await new CollectionsReport(_database.Db).BuildAsync(Day, Day, default);

        Assert.Equal(100m, collections.Summary.Single(f => f.Label == "Collected").Value);

        var ledger = await new CustomerService(_database.Db).GetLedgerAsync(customer.Id, default);
        Assert.Contains(ledger, e => e.EntryType == "Return credit" && e.Paid == 60m);
    }

    [Fact]
    public async Task A_credit_cannot_be_typed_in_as_a_payment()
    {
        var customer = await SetUpAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(() => _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, Day, 100m, PaymentMethod.ReturnCredit, null, null, null), default));

        Assert.Contains("not money received", exception.Message);
    }

    [Fact]
    public async Task A_return_left_for_the_office_is_settled_once()
    {
        var customer = await SetUpAsync();
        await BillAsync(customer.Id, 10m, 30m);
        var pending = await ReturnAsync(customer.Id, 3m, ReturnSettlement.Pending);

        Assert.Equal(ReturnSettlement.Pending, pending.Return.Settlement);
        Assert.Equal(300m, await _payments.GetBalanceAsync(customer.Id, default));

        var settled = await _returns.SettleAsync(pending.Return.Id, new SettleReturnRequest(ReturnSettlement.Credit, null, null), default);

        Assert.Equal(90m, settled.Return.CreditAmount);
        Assert.Equal(210m, await _payments.GetBalanceAsync(customer.Id, default));

        var again = await Assert.ThrowsAsync<DomainException>(() =>
            _returns.SettleAsync(pending.Return.Id, new SettleReturnRequest(ReturnSettlement.NoCompensation, null, null), default));
        Assert.Contains("already settled", again.Message);
    }

    [Fact]
    public async Task Cancelling_a_replacement_puts_the_fresh_packets_back()
    {
        var customer = await SetUpAsync();
        var before = await _stock.GetQuantityOnHandAsync(_productId, KnownStockLocations.MainWarehouseId, default);
        var result = await ReturnAsync(customer.Id, 4m, ReturnSettlement.Replacement);

        var cancelled = await _returns.CancelAsync(result.Return.Id, "Wrong shop", default);

        Assert.Equal(ReturnStatus.Cancelled, cancelled.Status);
        Assert.Equal(result.Return.ReturnNumber, cancelled.ReturnNumber);
        Assert.Equal(before, await _stock.GetQuantityOnHandAsync(_productId, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task A_credited_return_cannot_be_cancelled()
    {
        var customer = await SetUpAsync();
        await BillAsync(customer.Id, 10m, 30m);
        var result = await ReturnAsync(customer.Id, 2m, ReturnSettlement.Credit);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _returns.CancelAsync(result.Return.Id, "Oops", default));

        Assert.Contains("cannot be taken back", exception.Message);
    }

    [Fact]
    public async Task Returns_have_their_own_gapless_numbers()
    {
        var customer = await SetUpAsync();

        var first = await ReturnAsync(customer.Id, 1m, ReturnSettlement.NoCompensation);
        var second = await ReturnAsync(customer.Id, 1m, ReturnSettlement.NoCompensation);

        Assert.Equal("RN/26-27/000001", first.Return.ReturnNumber);
        Assert.Equal("RN/26-27/000002", second.Return.ReturnNumber);
    }

    [Fact]
    public async Task A_recorded_return_cannot_be_edited()
    {
        var customer = await SetUpAsync();
        var result = await ReturnAsync(customer.Id, 1m, ReturnSettlement.NoCompensation);

        await using var db = _database.NewContext();
        var note = await db.ReturnNotes.SingleAsync(r => r.Id == result.Return.Id);
        note.Value = 1m;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_multi_branch_shop_must_say_which_branch()
    {
        var customer = await SetUpAsync();
        await _database.SeedBranchAsync(customer.Id);

        var exception = await Assert.ThrowsAsync<DomainException>(() => ReturnAsync(customer.Id, 1m, ReturnSettlement.Pending));

        Assert.Contains("select a branch", exception.Message);
    }

    private async Task<Customer> SetUpAsync()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        _productId = packet.Id;
        await _database.AddStockAsync(_productId, 1_000m);
        var customer = await _database.SeedCustomerAsync();

        // The shop's agreed rate, which is what returned packets are worth.
        await new CustomerPriceService(_database.Db, _database.CurrentUser).SetAsync(customer.Id, _productId, 30m, default);
        return customer;
    }

    private async Task<InvoiceDetailDto> BillAsync(Guid customerId, decimal quantity, decimal rate, DateOnly? date = null) =>
        (await _invoices.CreateAsync(
            new CreateInvoiceRequest(customerId, date ?? Day.AddDays(-3), 0m, null,
                [new InvoiceLineRequest(_productId, quantity, rate)]),
            default)).Invoice;

    private Task<ReturnResponse> ReturnAsync(
        Guid customerId,
        decimal quantity,
        ReturnSettlement settlement,
        decimal? creditAmount = null,
        Guid? replacementFrom = null) =>
        _returns.CreateAsync(
            new CreateReturnRequest(
                customerId, null, Day,
                [new ReturnLineRequest(_productId, quantity, ReturnReason.Expired, null)],
                settlement, creditAmount, replacementFrom, null),
            default);
}
