using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// How an invoice is taxed and what it keeps. The supplier here is in Kerala (32), as Golden
/// Pappadam is; GST is off until a test switches it on, exactly as a fresh installation starts.
/// </summary>
public class InvoiceGstTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 9, 23);

    private TestDatabase _database = null!;
    private InvoiceService _invoices = null!;
    private Product _packet = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _invoices = new InvoiceService(_database.Db, new StockService(_database.Db), new CustomerPriceService(_database.Db));

        (_, _packet) = await _database.SeedProductsAsync();
        _packet.SellingPrice = 100m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(_packet.Id, 1_000m);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Without_a_gstin_a_bill_is_a_plain_invoice_with_the_same_totals_as_before()
    {
        var customer = await _database.SeedCustomerAsync();

        var invoice = await CreateAsync(customer.Id, quantity: 10m, discount: 50m);

        Assert.Equal(InvoiceDocumentType.Invoice, invoice.DocumentType);
        Assert.Equal(1000m, invoice.SubTotal);
        Assert.Equal(950m, invoice.TaxableAmount);
        Assert.Equal(950m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.CgstAmount + invoice.SgstAmount + invoice.IgstAmount);
        Assert.Null(invoice.Supplier.Gstin);
        Assert.Equal("Golden Pappadam", invoice.Supplier.Name);
    }

    [Fact]
    public async Task Once_gst_is_on_a_product_with_no_tax_treatment_cannot_be_billed()
    {
        await _database.ConfigureGstAsync();
        var customer = await StateCustomerAsync("32");

        var refused = await Assert.ThrowsAsync<DomainException>(() => CreateAsync(customer.Id));

        Assert.Contains("GST treatment", refused.Message);
        Assert.Empty(await _database.Db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task A_taxable_sale_needs_to_know_where_the_goods_went()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var customer = await _database.SeedCustomerAsync();

        var refused = await Assert.ThrowsAsync<DomainException>(() => CreateAsync(customer.Id));

        Assert.Contains("place of supply", refused.Message);
    }

    [Fact]
    public async Task A_sale_inside_kerala_is_a_tax_invoice_with_cgst_and_sgst()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var customer = await StateCustomerAsync("32");

        var invoice = await CreateAsync(customer.Id, quantity: 10m);

        Assert.Equal(InvoiceDocumentType.TaxInvoice, invoice.DocumentType);
        Assert.Equal("32", invoice.PlaceOfSupplyStateCode);
        Assert.False(invoice.IsInterState);
        Assert.Equal(25m, invoice.CgstAmount);
        Assert.Equal(25m, invoice.SgstAmount);
        Assert.Equal(0m, invoice.IgstAmount);
        Assert.Equal(1050m, invoice.TotalAmount);
        Assert.Equal("32AAAAA1234A1Z5", invoice.Supplier.Gstin);

        var line = Assert.Single(invoice.Lines);
        Assert.Equal("19059040", line.HsnCode);
        Assert.Equal(5m, line.GstRate);
        Assert.Equal(1050m, line.Amount);
    }

    [Fact]
    public async Task A_branch_in_tamil_nadu_is_charged_igst_even_though_its_parent_is_in_kerala()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var danya = await StateCustomerAsync("32", "Danya Supermarket");
        var coimbatore = await _database.SeedBranchAsync(danya.Id, "Coimbatore");
        coimbatore.StateCode = "33";
        coimbatore.Address = "Avinashi Road, Coimbatore";
        await _database.Db.SaveChangesAsync();

        var invoice = await CreateAsync(danya.Id, quantity: 10m, branchId: coimbatore.Id);

        Assert.True(invoice.IsInterState);
        Assert.Equal("33", invoice.PlaceOfSupplyStateCode);
        Assert.Equal(50m, invoice.IgstAmount);
        Assert.Equal(0m, invoice.CgstAmount + invoice.SgstAmount);
        Assert.Equal("Coimbatore", invoice.Branch!.Name);
        Assert.Equal("Avinashi Road, Coimbatore", invoice.Branch.Address);
        Assert.Equal("Danya Supermarket", invoice.Customer.Name);
    }

    [Fact]
    public async Task A_branch_with_no_state_is_not_assumed_to_be_where_its_parent_is()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var danya = await StateCustomerAsync("32", "Danya Supermarket");
        var branch = await _database.SeedBranchAsync(danya.Id, "Kundrathur");

        var refused = await Assert.ThrowsAsync<DomainException>(() => CreateAsync(danya.Id, branchId: branch.Id));

        Assert.Contains("Kundrathur", refused.Message);
    }

    [Fact]
    public async Task A_registered_shop_with_no_state_picked_is_placed_by_its_gstin()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var customer = await _database.SeedCustomerAsync("Chennai Stores");
        customer.Gstin = "33BBBBB5678B1Z2";
        await _database.Db.SaveChangesAsync();

        var invoice = await CreateAsync(customer.Id);

        Assert.Equal("33", invoice.PlaceOfSupplyStateCode);
        Assert.True(invoice.IsInterState);
        Assert.Equal("33BBBBB5678B1Z2", invoice.Customer.Gstin);
    }

    [Fact]
    public async Task A_shop_without_gst_gets_a_normal_bill_even_though_the_business_has_a_gstin()
    {
        // Pappadam today: one HSN code, exempt from GST.
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Exempt);
        var customer = await _database.SeedCustomerAsync();

        var invoice = await CreateAsync(customer.Id, quantity: 10m);

        Assert.Equal(InvoiceDocumentType.Invoice, invoice.DocumentType);
        Assert.Equal(1000m, invoice.TotalAmount);
        Assert.Null(invoice.Customer.Gstin);
    }

    [Fact]
    public async Task A_gst_customer_gets_a_gst_bill_of_supply_for_exempt_pappadam_with_both_gstins()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Exempt);
        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        customer.Gstin = "32PQRSX9876K1Z3";
        await _database.Db.SaveChangesAsync();

        var invoice = await CreateAsync(customer.Id, quantity: 10m);

        Assert.Equal(InvoiceDocumentType.BillOfSupply, invoice.DocumentType);
        Assert.Equal("32AAAAA1234A1Z5", invoice.Supplier.Gstin);
        Assert.Equal("32PQRSX9876K1Z3", invoice.Customer.Gstin);
        Assert.Equal("32", invoice.PlaceOfSupplyStateCode);
        Assert.Equal(1000m, invoice.TotalAmount);
        Assert.Equal(0m, invoice.CgstAmount + invoice.SgstAmount + invoice.IgstAmount);

        var line = Assert.Single(invoice.Lines);
        Assert.Equal(TaxTreatment.Exempt, line.TaxTreatment);
        Assert.Equal("19059040", line.HsnCode);
    }

    [Fact]
    public async Task A_gst_customer_cannot_be_billed_until_the_business_gstin_is_entered()
    {
        var customer = await _database.SeedCustomerAsync("Danya Supermarket");
        customer.Gstin = "32PQRSX9876K1Z3";
        await _database.Db.SaveChangesAsync();

        var refused = await Assert.ThrowsAsync<DomainException>(() => CreateAsync(customer.Id));

        Assert.Contains("GST customer", refused.Message);
        Assert.Empty(await _database.Db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task If_pappadam_ever_becomes_taxable_a_shop_without_gst_is_still_charged_it()
    {
        // Tax follows the product, not the customer: a normal shop never escapes a tax that applies.
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var customer = await StateCustomerAsync("32");

        var invoice = await CreateAsync(customer.Id, quantity: 10m);

        Assert.Equal(InvoiceDocumentType.TaxInvoice, invoice.DocumentType);
        Assert.Equal(50m, invoice.CgstAmount + invoice.SgstAmount);
        Assert.Null(invoice.Customer.Gstin);
    }

    [Fact]
    public async Task Taxable_and_exempt_goods_on_one_bill_are_each_treated_correctly()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var loose = await _database.Db.Products.SingleAsync(p => p.ProductCode == "LOOSE-1");
        loose.SellingPrice = 200m;
        loose.TaxTreatment = TaxTreatment.Exempt;
        await _database.Db.SaveChangesAsync();
        var customer = await StateCustomerAsync("32");

        var created = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, Today, 0m, null,
            [
                new InvoiceLineRequest(_packet.Id, 10m, null),
                new InvoiceLineRequest(loose.Id, 5m, null)
            ]),
            default);

        var invoice = created.Invoice;
        Assert.Equal(InvoiceDocumentType.TaxInvoice, invoice.DocumentType);
        Assert.Equal(25m, invoice.Lines[0].CgstAmount);
        Assert.Equal(0m, invoice.Lines[1].CgstAmount);
        Assert.Equal(2000m, invoice.TaxableAmount);
        Assert.Equal(2050m, invoice.TotalAmount);
    }

    [Fact]
    public async Task A_branch_bill_charges_the_parent_customers_agreed_rate_and_keeps_the_total_when_rates_include_tax()
    {
        await _database.ConfigureGstAsync(pricesIncludeTax: true);
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var danya = await StateCustomerAsync("32", "Danya Supermarket");
        var kundara = await _database.SeedBranchAsync(danya.Id, "Kundara");
        kundara.StateCode = "32";
        _database.Db.CustomerPrices.Add(new CustomerPrice { CustomerId = danya.Id, ProductId = _packet.Id, UnitPrice = 37m });
        await _database.Db.SaveChangesAsync();

        var invoice = await CreateAsync(danya.Id, quantity: 10m, branchId: kundara.Id);

        Assert.Equal(37m, Assert.Single(invoice.Lines).UnitPrice);
        Assert.Equal(370m, invoice.TotalAmount);
        Assert.Equal(invoice.CgstAmount, invoice.SgstAmount);
        Assert.Equal(370m, invoice.TaxableAmount + invoice.CgstAmount + invoice.SgstAmount);
    }

    [Fact]
    public async Task An_old_invoice_reads_the_same_after_the_shop_the_branch_and_the_product_change()
    {
        await _database.ConfigureGstAsync();
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 5m);
        var danya = await StateCustomerAsync("32", "Danya Supermarket");
        var kundara = await _database.SeedBranchAsync(danya.Id, "Kundara");
        kundara.StateCode = "32";
        kundara.Address = "Near the bus stand, Kundara";
        await _database.Db.SaveChangesAsync();

        var before = await CreateAsync(danya.Id, quantity: 10m, branchId: kundara.Id);

        danya.Name = "Danya Hypermarket";
        kundara.Name = "Kundara East";
        kundara.Address = "New building";
        _packet.Name = "Renamed packet";
        _packet.GstRate = 18m;
        await _database.Db.SaveChangesAsync();

        var after = await _invoices.GetDetailAsync(before.Id, default);

        Assert.Equal("Danya Supermarket", after.CustomerName);
        Assert.Equal("Kundara", after.Branch!.Name);
        Assert.Equal("Near the bus stand, Kundara", after.Branch.Address);
        Assert.Equal("250 g packet", after.Lines[0].Description);
        Assert.Equal(5m, after.Lines[0].GstRate);
        Assert.Equal(before.TotalAmount, after.TotalAmount);
    }

    [Fact]
    public async Task A_finalized_invoice_cannot_be_changed_by_any_code_path_except_cancellation()
    {
        var customer = await _database.SeedCustomerAsync();
        var created = await CreateAsync(customer.Id);

        var invoice = await _database.Db.Invoices.SingleAsync(i => i.Id == created.Id);
        invoice.TotalAmount = 1m;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => _database.Db.SaveChangesAsync());
        Assert.Contains("TotalAmount", refused.Message);
        _database.Db.ChangeTracker.Clear();

        var cancelled = await _invoices.CancelAsync(created.Id, "Shop refused delivery", default);
        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        Assert.Equal(created.InvoiceNumber, cancelled.InvoiceNumber);

        // The cancelled number stays taken; the next bill gets the one after it.
        var next = await CreateAsync(customer.Id);
        Assert.Equal("GP/26-27/000002", next.InvoiceNumber);
    }

    [Fact]
    public async Task The_preview_matches_the_invoice_exactly_and_uses_up_nothing()
    {
        await _database.ConfigureGstAsync(roundToNearestRupee: true);
        await _database.SetTaxAsync(_packet.Id, TaxTreatment.Taxable, 18m);
        var customer = await StateCustomerAsync("29");   // Karnataka: inter-state
        var request = new CreateInvoiceRequest(customer.Id, Today, 12.35m, null, [new InvoiceLineRequest(_packet.Id, 3m, 33.47m)]);

        var preview = await _invoices.PreviewAsync(request, default);

        Assert.Empty(await _database.Db.Invoices.ToListAsync());
        Assert.Empty(await _database.Db.InvoiceNumberSequences.ToListAsync());

        var invoice = (await _invoices.CreateAsync(request, default)).Invoice;

        Assert.Equal("GP/26-27/000001", invoice.InvoiceNumber);
        Assert.Equal(preview.TotalAmount, invoice.TotalAmount);
        Assert.Equal(preview.IgstAmount, invoice.IgstAmount);
        Assert.Equal(preview.TaxableAmount, invoice.TaxableAmount);
        Assert.Equal(preview.RoundOff, invoice.RoundOff);
        Assert.Equal(decimal.Truncate(invoice.TotalAmount), invoice.TotalAmount);
    }

    private async Task<Customer> StateCustomerAsync(string stateCode, string name = "Test Shop")
    {
        var customer = await _database.SeedCustomerAsync(name);
        customer.StateCode = stateCode;
        await _database.Db.SaveChangesAsync();

        return customer;
    }

    private async Task<InvoiceDetailDto> CreateAsync(Guid customerId, decimal quantity = 1m, decimal discount = 0m, Guid? branchId = null) =>
        (await _invoices.CreateAsync(
            new CreateInvoiceRequest(customerId, Today, discount, null, [new InvoiceLineRequest(_packet.Id, quantity, null)],
                BranchId: branchId),
            default)).Invoice;
}
