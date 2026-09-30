using GoldenPappadam.Api.Features.Dashboard;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The dashboard's product chart groups invoice lines in SQL, so an expression EF cannot
/// translate has to fail here rather than as a 500 in the browser.
/// </summary>
public class DashboardServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private DashboardService _dashboard = null!;
    private InvoiceService _invoices = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        var stock = new StockService(_database.Db);
        _invoices = new InvoiceService(_database.Db, stock, new CustomerPriceService(_database.Db, _database.CurrentUser));
        _dashboard = new DashboardService(_database.Db, stock);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Product_sales_add_up_the_lines_of_issued_bills()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        var customer = await _database.SeedCustomerAsync();
        await _database.AddStockAsync(loose.Id, 100m);
        await _database.AddStockAsync(packet.Id, 100m);

        var today = Api.Common.IndiaTime.Today();

        await _invoices.CreateAsync(
            new CreateInvoiceRequest(
                customer.Id,
                today,
                0m,
                null,
                [new InvoiceLineRequest(packet.Id, 10m, 30m), new InvoiceLineRequest(loose.Id, 2m, 100m)]),
            CancellationToken.None);

        // A second bill for the same product must fold into the same row.
        await _invoices.CreateAsync(
            new CreateInvoiceRequest(
                customer.Id,
                today,
                0m,
                null,
                [new InvoiceLineRequest(packet.Id, 5m, 30m)]),
            CancellationToken.None);

        var sales = await _dashboard.GetProductSalesAsync(today, today, CancellationToken.None);

        Assert.Equal(2, sales.Count);

        // Ordered by value, so the packet (15 x 30 = 450) comes before the loose (2 x 100 = 200).
        var packetSales = sales[0];
        Assert.Equal(packet.Id, packetSales.ProductId);
        Assert.Equal("250 g packet", packetSales.ProductName);
        Assert.Equal("PKT", packetSales.UnitCode);
        Assert.Equal("Pappadam", packetSales.CategoryName);
        Assert.Equal(15m, packetSales.QuantitySold);
        Assert.Equal(450m, packetSales.SalesValue);

        Assert.Equal(loose.Id, sales[1].ProductId);
        Assert.Equal(200m, sales[1].SalesValue);
    }

    [Fact]
    public async Task A_cancelled_bill_leaves_the_product_chart()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        var customer = await _database.SeedCustomerAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var today = Api.Common.IndiaTime.Today();

        var invoice = await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, today, 0m, null, [new InvoiceLineRequest(packet.Id, 10m, 30m)]),
            CancellationToken.None);

        await _invoices.CancelAsync(invoice.Invoice.Id, "Shop refused delivery", CancellationToken.None);

        var sales = await _dashboard.GetProductSalesAsync(today, today, CancellationToken.None);

        Assert.Empty(sales);
    }

    [Fact]
    public async Task Bills_outside_the_range_are_left_out()
    {
        var (_, packet) = await _database.SeedProductsAsync();
        var customer = await _database.SeedCustomerAsync();
        await _database.AddStockAsync(packet.Id, 100m);

        var today = Api.Common.IndiaTime.Today();
        var yesterday = today.AddDays(-1);

        await _invoices.CreateAsync(
            new CreateInvoiceRequest(customer.Id, yesterday, 0m, null, [new InvoiceLineRequest(packet.Id, 4m, 30m)]),
            CancellationToken.None);

        var justToday = await _dashboard.GetProductSalesAsync(today, today, CancellationToken.None);
        Assert.Empty(justToday);

        var bothDays = await _dashboard.GetProductSalesAsync(yesterday, today, CancellationToken.None);
        Assert.Equal(120m, Assert.Single(bothDays).SalesValue);
    }
}
