using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The credit rules from CLAUDE.md §1: bills are paid later, often partly, and one payment
/// can settle several bills.
/// </summary>
public class PaymentServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private InvoiceService _invoices = null!;
    private PaymentService _payments = null!;
    private CustomerService _customers = null!;
    private Guid _productId;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _invoices = new InvoiceService(_database.Db, new StockService(_database.Db));
        _payments = new PaymentService(_database.Db);
        _customers = new CustomerService(_database.Db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_partial_payment_leaves_the_rest_outstanding()
    {
        var customer = await SetUpAsync();
        var invoice = await BillAsync(customer.Id, 10_000m, new DateOnly(2026, 9, 1));

        var result = await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, new DateOnly(2026, 9, 8), 5_000m, PaymentMethod.Cash, null, null, null),
            default);

        Assert.Equal(5_000m, result.Payment.AllocatedAmount);
        Assert.Equal(0m, result.Payment.UnallocatedAmount);
        Assert.Equal(5_000m, result.CustomerBalance);

        var reloaded = await _invoices.GetDetailAsync(invoice.Id, default);
        Assert.Equal(5_000m, reloaded.AmountPaid);
        Assert.Equal(5_000m, reloaded.Outstanding);
    }

    [Fact]
    public async Task One_payment_settles_the_oldest_bills_first()
    {
        var customer = await SetUpAsync();
        var first = await BillAsync(customer.Id, 4_000m, new DateOnly(2026, 8, 1));
        var second = await BillAsync(customer.Id, 6_000m, new DateOnly(2026, 8, 20));

        // Enough for the first bill and part of the second.
        var result = await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 7_000m, PaymentMethod.UPI, "UPI-9911", null, null),
            default);

        Assert.Equal(2, result.Payment.Allocations.Count);
        Assert.Equal(4_000m, result.Payment.Allocations.Single(a => a.InvoiceId == first.Id).Amount);
        Assert.Equal(3_000m, result.Payment.Allocations.Single(a => a.InvoiceId == second.Id).Amount);
        Assert.Equal(3_000m, result.CustomerBalance);

        Assert.Equal(0m, (await _invoices.GetDetailAsync(first.Id, default)).Outstanding);
        Assert.Equal(3_000m, (await _invoices.GetDetailAsync(second.Id, default)).Outstanding);
    }

    [Fact]
    public async Task Money_beyond_the_bills_stays_on_account_and_still_reduces_the_balance()
    {
        var customer = await SetUpAsync();
        await BillAsync(customer.Id, 1_000m, new DateOnly(2026, 9, 1));

        var result = await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 1_500m, PaymentMethod.Cash, null, null, null),
            default);

        Assert.Equal(1_000m, result.Payment.AllocatedAmount);
        Assert.Equal(500m, result.Payment.UnallocatedAmount);
        Assert.Equal(-500m, result.CustomerBalance);
    }

    [Fact]
    public async Task A_payment_can_be_pointed_at_a_particular_bill()
    {
        var customer = await SetUpAsync();
        var older = await BillAsync(customer.Id, 4_000m, new DateOnly(2026, 8, 1));
        var newer = await BillAsync(customer.Id, 6_000m, new DateOnly(2026, 8, 20));

        // The shop says it is paying the newer bill, not the oldest one.
        var result = await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 6_000m, PaymentMethod.Cheque, "CHQ-42", null,
                [new PaymentAllocationRequest(newer.Id, 6_000m)]),
            default);

        Assert.Equal(newer.Id, Assert.Single(result.Payment.Allocations).InvoiceId);
        Assert.Equal(4_000m, (await _invoices.GetDetailAsync(older.Id, default)).Outstanding);
        Assert.Equal(0m, (await _invoices.GetDetailAsync(newer.Id, default)).Outstanding);
    }

    [Fact]
    public async Task A_bill_cannot_be_paid_twice_over()
    {
        var customer = await SetUpAsync();
        var invoice = await BillAsync(customer.Id, 1_000m, new DateOnly(2026, 9, 1));

        await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 800m, PaymentMethod.Cash, null, null, null), default);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 500m, PaymentMethod.Cash, null, null,
                [new PaymentAllocationRequest(invoice.Id, 500m)]),
            default));

        Assert.Contains("200.00 outstanding", exception.Message);
    }

    [Fact]
    public async Task A_payment_cannot_be_applied_to_another_customers_bill()
    {
        var customer = await SetUpAsync();
        var other = await _database.SeedCustomerAsync("Another Shop");
        var theirInvoice = await BillAsync(other.Id, 500m, new DateOnly(2026, 9, 1));

        var exception = await Assert.ThrowsAsync<DomainException>(() => _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 500m, PaymentMethod.Cash, null, null,
                [new PaymentAllocationRequest(theirInvoice.Id, 500m)]),
            default));

        Assert.Contains("different customer", exception.Message);
    }

    [Fact]
    public async Task A_bill_with_money_on_it_cannot_be_cancelled()
    {
        var customer = await SetUpAsync();
        var invoice = await BillAsync(customer.Id, 1_000m, new DateOnly(2026, 9, 1));
        await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, null, 400m, PaymentMethod.Cash, null, null, null), default);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _invoices.CancelAsync(invoice.Id, "Wrong shop", default));

        Assert.Contains("400.00 are applied", exception.Message);
    }

    [Fact]
    public async Task The_ledger_starts_from_the_opening_balance_and_runs_through_bills_and_payments()
    {
        var customer = await SetUpAsync(openingBalance: 2_000m);
        await BillAsync(customer.Id, 3_000m, new DateOnly(2026, 9, 1));
        await _payments.CreateAsync(
            new CreatePaymentRequest(customer.Id, new DateOnly(2026, 9, 10), 4_000m, PaymentMethod.Cash, null, null, null),
            default);

        var ledger = await _customers.GetLedgerAsync(customer.Id, default);

        Assert.Equal(["Opening", "Invoice", "Payment"], ledger.Select(e => e.EntryType));
        Assert.Equal([2_000m, 5_000m, 1_000m], ledger.Select(e => e.Balance));
    }

    private async Task<Customer> SetUpAsync(decimal openingBalance = 0m)
    {
        var (_, packet) = await _database.SeedProductsAsync();
        _productId = packet.Id;
        await _database.AddStockAsync(_productId, 10_000m);

        return await _database.SeedCustomerAsync(openingBalance: openingBalance);
    }

    /// <summary>Bills the given rupee amount by selling one unit priced at that amount.</summary>
    private async Task<InvoiceDetailDto> BillAsync(Guid customerId, decimal amount, DateOnly date)
    {
        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customerId, date, 0m, null,
                [new InvoiceLineRequest(_productId, 1m, amount)]),
            default);

        return result.Invoice;
    }
}
