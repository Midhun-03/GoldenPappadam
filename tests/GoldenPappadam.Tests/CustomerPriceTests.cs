using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Tests;

/// <summary>
/// The business rule this milestone exists for: the same packet costs different shops different
/// money, the office decides that, and a bill charges the right one without anybody being asked.
/// </summary>
public class CustomerPriceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private CustomerPriceService _prices = null!;
    private InvoiceService _invoices = null!;
    private Product _packet = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _prices = new CustomerPriceService(_database.Db);
        _invoices = new InvoiceService(_database.Db, new StockService(_database.Db), _prices);

        var (_, packet) = await _database.SeedProductsAsync();

        // The MRP: what the packet sells for when no shop has agreed anything different.
        packet.SellingPrice = 45m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 1000m);

        _packet = packet;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Two_shops_buying_the_same_packet_are_charged_their_own_prices()
    {
        var shopA = await _database.SeedCustomerAsync("Shop A");
        var shopB = await _database.SeedCustomerAsync("Shop B");

        await _prices.SetAsync(shopA.Id, _packet.Id, 35m, default);
        await _prices.SetAsync(shopB.Id, _packet.Id, 34m, default);

        var billA = await BillAsync(shopA.Id, quantity: 10m);
        var billB = await BillAsync(shopB.Id, quantity: 10m);

        Assert.Equal(35m, billA.Invoice.Lines[0].UnitPrice);
        Assert.Equal(350m, billA.Invoice.TotalAmount);

        Assert.Equal(34m, billB.Invoice.Lines[0].UnitPrice);
        Assert.Equal(340m, billB.Invoice.TotalAmount);
    }

    [Fact]
    public async Task A_shop_with_no_arrangement_pays_the_product_price()
    {
        var shop = await _database.SeedCustomerAsync("Ordinary Shop");

        var bill = await BillAsync(shop.Id, quantity: 2m);

        Assert.Equal(45m, bill.Invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task A_price_typed_on_the_bill_still_wins()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");
        await _prices.SetAsync(shop.Id, _packet.Id, 35m, default);

        var bill = await _invoices.CreateAsync(
            new CreateInvoiceRequest(shop.Id, null, 0m, null,
                [new InvoiceLineRequest(_packet.Id, 1m, 30m)]),
            default);

        Assert.Equal(30m, bill.Invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Changing_a_price_does_not_disturb_a_bill_already_made()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");
        await _prices.SetAsync(shop.Id, _packet.Id, 35m, default);

        var before = await BillAsync(shop.Id, quantity: 10m);

        await _prices.SetAsync(shop.Id, _packet.Id, 36m, default);

        var after = await BillAsync(shop.Id, quantity: 10m);
        var reread = await _invoices.GetDetailAsync(before.Invoice.Id, default);

        Assert.Equal(35m, reread.Lines[0].UnitPrice);
        Assert.Equal(36m, after.Invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Ending_an_arrangement_puts_the_shop_back_on_the_product_price()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");
        await _prices.SetAsync(shop.Id, _packet.Id, 35m, default);

        await _prices.RemoveAsync(shop.Id, _packet.Id, default);

        var bill = await BillAsync(shop.Id, quantity: 1m);
        Assert.Equal(45m, bill.Invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task Setting_a_price_twice_changes_it_rather_than_adding_a_second_one()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");

        await _prices.SetAsync(shop.Id, _packet.Id, 35m, default);
        await _prices.SetAsync(shop.Id, _packet.Id, 33m, default);

        var agreed = await _prices.GetForCustomerAsync(shop.Id, agreedOnly: true, default);

        Assert.Equal(33m, Assert.Single(agreed).AgreedPrice);
    }

    [Fact]
    public async Task A_price_agreed_again_after_being_ended_comes_back()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");

        await _prices.SetAsync(shop.Id, _packet.Id, 35m, default);
        await _prices.RemoveAsync(shop.Id, _packet.Id, default);
        await _prices.SetAsync(shop.Id, _packet.Id, 37m, default);

        var bill = await BillAsync(shop.Id, quantity: 1m);
        Assert.Equal(37m, bill.Invoice.Lines[0].UnitPrice);
    }

    [Fact]
    public async Task A_negative_price_is_refused()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");

        await Assert.ThrowsAsync<DomainException>(
            () => _prices.SetAsync(shop.Id, _packet.Id, -1m, default));
    }

    [Fact]
    public async Task A_product_nobody_has_priced_asks_for_a_price_instead_of_guessing()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");

        _packet.SellingPrice = null;
        await _database.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => BillAsync(shop.Id, quantity: 1m));

        Assert.Contains("has no selling price", error.Message);
    }

    [Fact]
    public async Task The_price_list_shows_what_each_shop_would_actually_be_charged()
    {
        var shop = await _database.SeedCustomerAsync("Shop A");
        await _prices.SetAsync(shop.Id, _packet.Id, 35m, default);

        var list = await _prices.GetForCustomerAsync(shop.Id, agreedOnly: false, default);
        var packet = list.Single(p => p.ProductId == _packet.Id);
        var loose = list.Single(p => p.ProductId != _packet.Id);

        Assert.Equal(45m, packet.DefaultPrice);
        Assert.Equal(35m, packet.AgreedPrice);
        Assert.Equal(35m, packet.EffectivePrice);

        // The loose product has no price anywhere, and the list says so rather than inventing one.
        Assert.Null(loose.AgreedPrice);
        Assert.Null(loose.EffectivePrice);
    }

    private Task<CreateInvoiceResponse> BillAsync(Guid customerId, decimal quantity) =>
        _invoices.CreateAsync(
            new CreateInvoiceRequest(customerId, null, 0m, null,
                [new InvoiceLineRequest(_packet.Id, quantity, null)]),
            default);
}
