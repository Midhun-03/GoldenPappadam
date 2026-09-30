using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Reports;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Reports only add up what the rest of the system already records, so each test checks a report's
/// figures against the same numbers read the ordinary way: bill totals, payments, the customer's
/// balance and ledger.
/// </summary>
public class ReportTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 9, 23);

    private TestDatabase _database = null!;
    private InvoiceService _invoices = null!;
    private PaymentService _payments = null!;
    private Product _packet = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _invoices = new InvoiceService(_database.Db, new StockService(_database.Db), new CustomerPriceService(_database.Db, _database.CurrentUser));
        _payments = new PaymentService(_database.Db);

        (_, _packet) = await _database.SeedProductsAsync();
        _packet.SellingPrice = 40m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(_packet.Id, 1_000m);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task The_sales_report_adds_up_to_the_bills_and_leaves_cancelled_ones_out()
    {
        var kumar = await _database.SeedCustomerAsync("Kumar Stores");
        var anand = await _database.SeedCustomerAsync("Anand Bakery");

        await BillAsync(kumar.Id, 10m, Day);           // 400
        await BillAsync(anand.Id, 5m, Day);            // 200
        var refused = await BillAsync(anand.Id, 3m, Day);
        await _invoices.CancelAsync(refused.Id, "Shop refused", default);
        await BillAsync(kumar.Id, 1m, Day.AddDays(1)); // outside the day

        await _payments.CreateAsync(
            new CreatePaymentRequest(kumar.Id, Day, 150m, PaymentMethod.Cash, null, null, null), default);

        var report = await new SalesReport(_database.Db).BuildAsync(Day, Day, default);

        Assert.Equal(2m, Figure(report, "Bills"));
        Assert.Equal(600m, Figure(report, "Sales"));
        Assert.Equal(150m, Figure(report, "Paid so far"));
        Assert.Equal(450m, Figure(report, "Still due"));

        var bills = Section(report, "Bills");
        Assert.Equal(2, bills.Rows.Count);
        Assert.Equal(600m, bills.Totals!["total"]);

        var byProduct = Section(report, "By product");
        Assert.Equal(15m, Assert.Single(byProduct.Rows)["quantity"]);
        Assert.Equal(600m, byProduct.Totals!["value"]);

        Assert.Equal(600m, Section(report, "By shop").Totals!["total"]);
        Assert.Single(Section(report, "Cancelled bills (not counted above)").Rows);
    }

    [Fact]
    public async Task The_collections_report_lists_each_payment_with_the_bills_it_settled()
    {
        var kumar = await _database.SeedCustomerAsync("Kumar Stores");
        var bill = await BillAsync(kumar.Id, 10m, Day.AddDays(-3));   // 400

        await _payments.CreateAsync(
            new CreatePaymentRequest(kumar.Id, Day, 500m, PaymentMethod.UPI, "UPI-1", null, null), default);
        await _payments.CreateAsync(
            new CreatePaymentRequest(kumar.Id, Day.AddDays(-1), 50m, PaymentMethod.Cash, null, null, null), default);

        var report = await new CollectionsReport(_database.Db).BuildAsync(Day, Day, default);

        Assert.Equal(500m, Figure(report, "Collected"));
        Assert.Equal(100m, Figure(report, "Kept on account"));   // 500 against the 400 bill leaves 100

        var payment = Assert.Single(Section(report, "Payments").Rows);
        Assert.Equal(bill.InvoiceNumber, payment["bills"]);
        Assert.Equal("UPI", Assert.Single(Section(report, "By method").Rows)["method"]);
    }

    [Fact]
    public async Task The_outstanding_report_ages_each_bill_and_every_row_adds_up_to_the_shops_balance()
    {
        var kumar = await _database.SeedCustomerAsync("Kumar Stores", openingBalance: 1_000m);
        await BillAsync(kumar.Id, 10m, Day.AddDays(-3));    // 400, 3 days old
        await BillAsync(kumar.Id, 5m, Day.AddDays(-20));    // 200, 20 days old

        var anand = await _database.SeedCustomerAsync("Anand Bakery");
        await BillAsync(anand.Id, 2m, Day.AddDays(-70));    // 80, 70 days old
        await _payments.CreateAsync(   // 80 settles the bill, 20 is paid ahead
            new CreatePaymentRequest(anand.Id, Day.AddDays(-1), 100m, PaymentMethod.Cash, null, null, null), default);

        var report = await new OutstandingReport(_database.Db).BuildAsync(Day, default);
        var rows = Section(report, "By shop").Rows;

        var kumarRow = rows.Single(r => (string)r["customer"]! == "Kumar Stores");
        Assert.Equal(1_000m, kumarRow["opening"]);
        Assert.Equal(400m, kumarRow["d0"]);
        Assert.Equal(200m, kumarRow["d16"]);
        Assert.Equal(1_600m, kumarRow["balance"]);
        Assert.Equal(Day.AddDays(-20), kumarRow["oldest"]);

        var anandRow = rows.Single(r => (string)r["customer"]! == "Anand Bakery");
        Assert.Null(anandRow["d61"]);
        Assert.Equal(-20m, anandRow["onAccount"]);
        Assert.Equal(-20m, anandRow["balance"]);

        // The same balances the customer screens show.
        var balances = await CustomerQueries.Project(_database.Db.Customers, _database.Db)
            .ToDictionaryAsync(c => c.Name, c => c.Balance);
        Assert.All(rows, r => Assert.Equal(balances[(string)r["customer"]!], r["balance"]));

        Assert.Equal(1_600m, Figure(report, "Total owed"));
        Assert.Equal(1m, Figure(report, "Shops owing"));
    }

    [Fact]
    public async Task Money_paid_without_naming_a_bill_clears_the_oldest_debt_first()
    {
        // Paid its before-system balance in cash: nothing old is still owing.
        var sree = await _database.SeedCustomerAsync("Sree Krishna Stores", openingBalance: 3_500m);
        await _payments.CreateAsync(
            new CreatePaymentRequest(sree.Id, Day.AddDays(-9), 3_500m, PaymentMethod.Cash, null, null, null), default);
        await BillAsync(sree.Id, 1m, Day.AddDays(-1));   // 40, the only thing owed

        // Paid 300 in advance, then took a 200 bill: that bill is not overdue, the shop is 100 ahead.
        var dhanya = await _database.SeedCustomerAsync("Dhanya Super Market");
        await _payments.CreateAsync(
            new CreatePaymentRequest(dhanya.Id, Day.AddDays(-5), 300m, PaymentMethod.Cash, null, null, null), default);
        await BillAsync(dhanya.Id, 5m, Day.AddDays(-2));

        var rows = Section(await new OutstandingReport(_database.Db).BuildAsync(Day, default), "By shop").Rows;

        var sreeRow = rows.Single(r => (string)r["customer"]! == "Sree Krishna Stores");
        Assert.Null(sreeRow["opening"]);
        Assert.Null(sreeRow["onAccount"]);
        Assert.Equal(40m, sreeRow["d0"]);
        Assert.Equal(40m, sreeRow["balance"]);

        var dhanyaRow = rows.Single(r => (string)r["customer"]! == "Dhanya Super Market");
        Assert.Null(dhanyaRow["d0"]);
        Assert.Equal(-100m, dhanyaRow["onAccount"]);
        Assert.Equal(-100m, dhanyaRow["balance"]);
        Assert.Null(dhanyaRow["oldest"]);
    }

    [Fact]
    public async Task An_earlier_day_reads_as_it_did_that_evening()
    {
        var kumar = await _database.SeedCustomerAsync("Kumar Stores");
        await BillAsync(kumar.Id, 10m, Day.AddDays(-5));   // 400
        await _payments.CreateAsync(
            new CreatePaymentRequest(kumar.Id, Day, 400m, PaymentMethod.Cash, null, null, null), default);

        var yesterday = await new OutstandingReport(_database.Db).BuildAsync(Day.AddDays(-1), default);
        var today = await new OutstandingReport(_database.Db).BuildAsync(Day, default);

        Assert.Equal(400m, Figure(yesterday, "Total owed"));
        Assert.Equal(0m, Figure(today, "Total owed"));
    }

    [Fact]
    public async Task A_statement_brings_the_earlier_balance_forward_and_ends_on_the_ledgers_balance()
    {
        var kumar = await _database.SeedCustomerAsync("Kumar Stores", openingBalance: 500m);
        await BillAsync(kumar.Id, 10m, Day.AddDays(-10));  // 400, before the period
        await BillAsync(kumar.Id, 5m, Day);                // 200
        await _payments.CreateAsync(
            new CreatePaymentRequest(kumar.Id, Day, 300m, PaymentMethod.Cash, null, null, null), default);

        var customers = new CustomerService(_database.Db);
        var report = await new StatementReport(_database.Db, customers).BuildAsync(kumar.Id, Day.AddDays(-2), Day, default);

        Assert.Equal(900m, Figure(report, "Brought forward"));
        Assert.Equal(200m, Figure(report, "Billed"));
        Assert.Equal(300m, Figure(report, "Paid"));
        Assert.Equal(800m, Figure(report, "Owed at the end"));

        var ledger = await customers.GetLedgerAsync(kumar.Id, default);
        Assert.Equal(ledger[^1].Balance, Figure(report, "Owed at the end"));
        Assert.Equal("Brought forward", Section(report, "Account").Rows[0]["type"]);
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-01")]
    [InlineData("2025-09-01", "2026-09-10")]
    public void A_backwards_or_over_long_period_is_refused(string from, string to)
    {
        Assert.Throws<DomainException>(() => ReportPeriod.Resolve(DateOnly.Parse(from), DateOnly.Parse(to)));
    }

    private async Task<InvoiceDetailDto> BillAsync(Guid customerId, decimal quantity, DateOnly date) =>
        (await _invoices.CreateAsync(
            new CreateInvoiceRequest(customerId, date, 0m, null, [new InvoiceLineRequest(_packet.Id, quantity, null)]),
            default)).Invoice;

    private static decimal Figure(ReportDocument report, string label) =>
        report.Summary.Single(f => f.Label == label).Value;

    private static ReportSection Section(ReportDocument report, string title) =>
        report.Sections.Single(s => s.Title == title);
}
