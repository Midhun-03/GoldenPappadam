using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

public class InvoiceServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private InvoiceService _invoices = null!;
    private StockService _stock = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _invoices = new InvoiceService(_database.Db, _stock, new CustomerPriceService(_database.Db));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task An_invoice_prices_its_lines_takes_stock_and_gets_a_number()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        await SetPriceAsync(packet.Id, 32m);
        await _database.AddStockAsync(packet.Id, 500m);
        var customer = await _database.SeedCustomerAsync();

        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, new DateOnly(2026, 9, 14), 0m, null,
                [new InvoiceLineRequest(packet.Id, 40m, null)]),
            default);

        Assert.Equal("INV-2026-00001", result.Invoice.InvoiceNumber);
        Assert.Equal(1280m, result.Invoice.SubTotal);   // 40 × 32
        Assert.Equal(1280m, result.Invoice.TotalAmount);
        Assert.Equal(1280m, result.Invoice.Outstanding);
        Assert.Empty(result.Warnings);

        Assert.Equal(460m, await _stock.GetQuantityOnHandAsync(packet.Id, default));

        var movement = await _database.Db.StockMovements
            .SingleAsync(m => m.ReferenceType == StockReferenceType.Invoice);
        Assert.Equal(StockMovementType.Sale, movement.MovementType);
        Assert.Equal(-40m, movement.Quantity);
        Assert.Equal(result.Invoice.Id, movement.ReferenceId);
    }

    [Fact]
    public async Task Invoice_numbers_run_on_within_the_indian_financial_year()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        await SetPriceAsync(packet.Id, 10m);
        var customer = await _database.SeedCustomerAsync();

        // The financial year starts on 1 April, so these three bills fall in FY 2025, 2026 and 2026.
        var march = await CreateAsync(customer.Id, packet.Id, new DateOnly(2026, 3, 31));
        var april = await CreateAsync(customer.Id, packet.Id, new DateOnly(2026, 4, 1));
        var september = await CreateAsync(customer.Id, packet.Id, new DateOnly(2026, 9, 14));

        Assert.Equal("INV-2025-00001", march.Invoice.InvoiceNumber);
        Assert.Equal("INV-2026-00001", april.Invoice.InvoiceNumber);
        Assert.Equal("INV-2026-00002", september.Invoice.InvoiceNumber);
    }

    [Fact]
    public async Task A_line_price_overrides_the_product_price_and_the_bill_keeps_the_name_printed_on_it()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        await SetPriceAsync(packet.Id, 32m);
        await _database.AddStockAsync(packet.Id, 100m);
        var customer = await _database.SeedCustomerAsync();

        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null,
                [new InvoiceLineRequest(packet.Id, 10m, 30m)]),
            default);

        var line = Assert.Single(result.Invoice.Lines);
        Assert.Equal(30m, line.UnitPrice);
        Assert.Equal(300m, line.LineTotal);
        Assert.Equal("250 g packet", line.Description);
        Assert.Equal("PKT", line.UnitCode);

        // Renaming the product later must not rewrite history.
        var product = await _database.Db.Products.FirstAsync(p => p.Id == packet.Id);
        product.Name = "250 g packet (new label)";
        await _database.Db.SaveChangesAsync();

        var reloaded = await _invoices.GetDetailAsync(result.Invoice.Id, default);
        Assert.Equal("250 g packet", reloaded.Lines[0].Description);
    }

    [Fact]
    public async Task A_product_with_no_price_must_be_priced_on_the_line()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var customer = await _database.SeedCustomerAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(() => _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null, [new InvoiceLineRequest(loose.Id, 5m, null)]),
            default));

        Assert.Contains("no selling price", exception.Message);
    }

    [Fact]
    public async Task A_bill_level_discount_comes_off_the_total()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        await SetPriceAsync(packet.Id, 32m);
        await _database.AddStockAsync(packet.Id, 100m);
        var customer = await _database.SeedCustomerAsync();

        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 80m, null, [new InvoiceLineRequest(packet.Id, 40m, null)]),
            default);

        Assert.Equal(1280m, result.Invoice.SubTotal);
        Assert.Equal(80m, result.Invoice.DiscountAmount);
        Assert.Equal(1200m, result.Invoice.TotalAmount);

        await Assert.ThrowsAsync<DomainException>(() => _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 5000m, null, [new InvoiceLineRequest(packet.Id, 10m, null)]),
            default));
    }

    [Fact]
    public async Task Selling_more_than_is_in_stock_warns_but_still_saves()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        await SetPriceAsync(packet.Id, 32m);
        await _database.AddStockAsync(packet.Id, 10m);
        var customer = await _database.SeedCustomerAsync();

        var result = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, null, 0m, null, [new InvoiceLineRequest(packet.Id, 40m, null)]),
            default);

        Assert.Single(result.Warnings);
        Assert.Contains("-30", result.Warnings[0]);
        Assert.Equal(-30m, await _stock.GetQuantityOnHandAsync(packet.Id, default));
        Assert.Equal(InvoiceStatus.Issued, result.Invoice.Status);
    }

    [Fact]
    public async Task Cancelling_puts_the_stock_back_and_keeps_the_bill()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        await SetPriceAsync(packet.Id, 32m);
        await _database.AddStockAsync(packet.Id, 100m);
        var customer = await _database.SeedCustomerAsync();

        var created = await CreateAsync(customer.Id, packet.Id, null, quantity: 40m);
        Assert.Equal(60m, await _stock.GetQuantityOnHandAsync(packet.Id, default));

        var cancelled = await _invoices.CancelAsync(created.Invoice.Id, "Shop refused delivery", default);

        Assert.Equal(InvoiceStatus.Cancelled, cancelled.Status);
        Assert.Equal(0m, cancelled.Outstanding);
        Assert.Equal("Shop refused delivery", cancelled.CancellationReason);
        Assert.Equal(100m, await _stock.GetQuantityOnHandAsync(packet.Id, default));
        Assert.Single(cancelled.Lines);

        var reversal = await _database.Db.StockMovements
            .SingleAsync(m => m.MovementType == StockMovementType.SaleReversal);
        Assert.Equal(40m, reversal.Quantity);
    }

    private async Task SetPriceAsync(Guid productId, decimal price)
    {
        var product = await _database.Db.Products.FirstAsync(p => p.Id == productId);
        product.SellingPrice = price;
        await _database.Db.SaveChangesAsync();
    }

    private Task<CreateInvoiceResponse> CreateAsync(
        Guid customerId,
        Guid productId,
        DateOnly? date,
        decimal quantity = 1m) =>
        _invoices.CreateAsync(
            new CreateInvoiceRequest(customerId, date, 0m, null, [new InvoiceLineRequest(productId, quantity, null)]),
            default);
}
