using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The "GST registered" tick box on a customer, and the guards that keep GST from switching on
/// half-configured. Only shops that are GST registered get GST bills; every other shop gets a normal bill.
/// </summary>
public class GstCustomerTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private CustomerService _customers = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _customers = new CustomerService(_database.Db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_gst_customer_is_saved_with_its_gstin_and_its_state_comes_from_the_gstin()
    {
        await _database.ConfigureGstAsync();
        var customer = await _customers.CreateAsync(Request("Danya Supermarket", isGstRegistered: true, gstin: "32pqrsx9876k1z3"), default);

        Assert.Equal("32PQRSX9876K1Z3", customer.Gstin);
        Assert.Equal("32", customer.StateCode);

        var listed = await CustomerQueries.Project(_database.Db.Customers, _database.Db).SingleAsync();
        Assert.True(listed.IsGstRegistered);
    }

    [Fact]
    public async Task A_normal_customer_has_no_gstin()
    {
        await _customers.CreateAsync(Request("Sree Krishna Stores"), default);

        var listed = await CustomerQueries.Project(_database.Db.Customers, _database.Db).SingleAsync();
        Assert.False(listed.IsGstRegistered);
        Assert.Null(listed.Gstin);
    }

    [Fact]
    public async Task Ticking_gst_registered_without_a_gstin_is_refused()
    {
        var refused = await Assert.ThrowsAsync<DomainException>(() =>
            _customers.CreateAsync(Request("Danya Supermarket", isGstRegistered: true), default));

        Assert.Contains("Enter the GSTIN", refused.Message);
    }

    [Fact]
    public async Task A_gstin_without_the_tick_is_refused_rather_than_quietly_making_a_gst_customer()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _customers.CreateAsync(Request("Danya Supermarket", gstin: "32PQRSX9876K1Z3"), default));
    }

    [Fact]
    public async Task Unticking_gst_registered_turns_a_gst_customer_back_into_a_normal_one()
    {
        await _database.ConfigureGstAsync();
        var customer = await _customers.CreateAsync(Request("Danya Supermarket", isGstRegistered: true, gstin: "32PQRSX9876K1Z3"), default);

        var updated = await _customers.UpdateAsync(customer.Id, Request("Danya Supermarket"), default);

        Assert.Null(updated.Gstin);
    }

    [Fact]
    public async Task A_shop_cannot_be_marked_gst_registered_before_the_business_gstin_is_entered()
    {
        // Otherwise the salesman's next sale there would be saved on the phone and refused at sync.
        var refused = await Assert.ThrowsAsync<DomainException>(() =>
            _customers.CreateAsync(Request("Danya Supermarket", isGstRegistered: true, gstin: "32PQRSX9876K1Z3"), default));

        Assert.Contains("Settings", refused.Message);
    }

    [Fact]
    public async Task A_gst_customer_can_still_be_edited_if_the_business_gstin_is_later_removed()
    {
        await _database.ConfigureGstAsync();
        var customer = await _customers.CreateAsync(Request("Danya Supermarket", isGstRegistered: true, gstin: "32PQRSX9876K1Z3"), default);
        await _database.ConfigureGstAsync(gstin: null);

        var renamed = await _customers.UpdateAsync(
            customer.Id, Request("Danya Hypermarket", isGstRegistered: true, gstin: "32PQRSX9876K1Z3"), default);

        Assert.Equal("Danya Hypermarket", renamed.Name);
    }

    [Fact]
    public async Task While_the_business_has_a_gstin_a_product_cannot_be_left_without_a_gst_treatment()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.SetTaxAsync(loose.Id, TaxTreatment.Exempt);
        await _database.ConfigureGstAsync();
        var products = new ProductService(_database.Db);

        var refused = await Assert.ThrowsAsync<DomainException>(() => products.CreateAsync(
            new SaveProductRequest("LOOSE-2", "Loose pappadam (small)", loose.CategoryId, ProductKind.Loose,
                loose.UnitOfMeasureId, null, null, 150m, null),
            default));

        Assert.Contains("GST treatment", refused.Message);

        var saved = await products.CreateAsync(
            new SaveProductRequest("LOOSE-2", "Loose pappadam (small)", loose.CategoryId, ProductKind.Loose,
                loose.UnitOfMeasureId, null, null, 150m, null, "19059040", TaxTreatment.Exempt),
            default);

        Assert.Equal(TaxTreatment.Exempt, saved.TaxTreatment);
    }

    private static SaveCustomerRequest Request(string name, bool isGstRegistered = false, string? gstin = null) =>
        new(name, null, null, null, 0m, null, false, Gstin: gstin, IsGstRegistered: isGstRegistered);
}
