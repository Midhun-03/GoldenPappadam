using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerBranches;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Danya Supermarket is one customer with several physical shops. Pricing stays on the parent
/// customer - a branch never gets its own price row - and a bill for a multi-branch customer must
/// name which shop it is for.
/// </summary>
public class CustomerBranchTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private CustomerBranchService _branches = null!;
    private InvoiceService _invoices = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _branches = new CustomerBranchService(_database.Db);
        _invoices = new InvoiceService(_database.Db, new StockService(_database.Db), new CustomerPriceService(_database.Db, _database.CurrentUser));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Adding_a_branch_marks_the_customer_multi_branch()
    {
        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        Assert.False(customer.HasMultipleBranches);

        await _branches.CreateAsync(customer.Id, new SaveCustomerBranchRequest("Kundara", "Kollam", null, null, null), default);

        var reloaded = await _database.Db.Customers.AsNoTracking().FirstAsync(c => c.Id == customer.Id);
        Assert.True(reloaded.HasMultipleBranches);

        var active = await _branches.GetForCustomerAsync(customer.Id, includeInactive: false, default);
        Assert.Single(active);
    }

    [Fact]
    public async Task A_branch_name_cannot_repeat_under_the_same_customer()
    {
        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        await _branches.CreateAsync(customer.Id, new SaveCustomerBranchRequest("Kundara", null, null, null, null), default);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _branches.CreateAsync(
            customer.Id, new SaveCustomerBranchRequest("Kundara", null, null, null, null), default));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task A_deactivated_branch_is_kept_not_deleted()
    {
        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        var branch = await _branches.CreateAsync(
            customer.Id, new SaveCustomerBranchRequest("Kundrathur", null, null, null, null), default);

        var deactivated = await _branches.SetActiveAsync(customer.Id, branch.Id, false, default);
        Assert.False(deactivated.IsActive);

        var active = await _branches.GetForCustomerAsync(customer.Id, includeInactive: false, default);
        Assert.Empty(active);

        var all = await _branches.GetForCustomerAsync(customer.Id, includeInactive: true, default);
        Assert.Single(all);
    }

    [Fact]
    public async Task A_bill_for_a_multi_branch_customer_needs_a_branch()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 32m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        await _database.SeedBranchAsync(customer.Id, "Kundara");

        var exception = await Assert.ThrowsAsync<DomainException>(() => _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null, [new InvoiceLineRequest(packet.Id, 5m, null)]),
            default));

        Assert.Contains("select a branch", exception.Message);
    }

    [Fact]
    public async Task A_bill_for_a_multi_branch_customer_records_the_chosen_branch()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 32m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        var kundara = await _database.SeedBranchAsync(customer.Id, "Kundara");

        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null,
                [new InvoiceLineRequest(packet.Id, 5m, null)], BranchId: kundara.Id),
            default);

        Assert.Equal(kundara.Id, result.Invoice.BranchId);
        Assert.Equal("Kundara", result.Invoice.BranchName);
    }

    [Fact]
    public async Task An_inactive_branch_cannot_be_billed()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 32m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        var kundara = await _database.SeedBranchAsync(customer.Id, "Kundara");
        await _branches.SetActiveAsync(customer.Id, kundara.Id, false, default);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null,
                [new InvoiceLineRequest(packet.Id, 5m, null)], BranchId: kundara.Id),
            default));

        Assert.Contains("not active", exception.Message);
    }

    [Fact]
    public async Task A_single_location_customer_never_carries_a_branch()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 32m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var customer = await _database.SeedCustomerAsync("ABC Supermarket");

        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null, [new InvoiceLineRequest(packet.Id, 5m, null)]),
            default);

        Assert.Null(result.Invoice.BranchId);
    }

    [Fact]
    public async Task Branch_pricing_falls_back_to_the_parent_customers_agreed_price()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 45.50m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        var prices = new CustomerPriceService(_database.Db, _database.CurrentUser);
        await prices.SetAsync(customer.Id, packet.Id, 37m, default);

        var kundara = await _database.SeedBranchAsync(customer.Id, "Kundara");
        var coimbatore = await _database.SeedBranchAsync(customer.Id, "Coimbatore");

        var billKundara = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null,
                [new InvoiceLineRequest(packet.Id, 1m, null)], BranchId: kundara.Id),
            default);
        var billCoimbatore = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null,
                [new InvoiceLineRequest(packet.Id, 1m, null)], BranchId: coimbatore.Id),
            default);

        Assert.Equal(37m, billKundara.Invoice.Lines[0].UnitPrice);
        Assert.Equal(37m, billCoimbatore.Invoice.Lines[0].UnitPrice);
    }
}
