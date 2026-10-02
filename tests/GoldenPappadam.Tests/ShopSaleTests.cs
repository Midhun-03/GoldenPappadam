using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.OwnShop.Sales;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.OwnShop;
using GoldenPappadam.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Sales over the own shop's counter, by the piece (owner, 2026-09-30): paid at the time of buying,
/// by walk-in and known customers alike; the rate may be lowered to the product's minimum and never
/// raised above its rate; and the shop can never sell pieces it does not have.
/// </summary>
public class ShopSaleTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private StockService _stock = null!;
    private ShopSaleService _sales = null!;
    private Product _pieces = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _sales = NewService(_database.Db);

        var (loose, _) = await _database.SeedProductsAsync();
        _pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(_pieces.Id, 6000m, KnownStockLocations.OwnShopId);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private ShopSaleService NewService(Infrastructure.Persistence.AppDbContext db) =>
        new(db, new StockService(db), new CustomerPriceService(db, _database.CurrentUser));

    private CreateShopSaleRequest Sale(decimal pieces, decimal? rate = null, Guid? customerId = null, Guid? clientRequestId = null) =>
        new(customerId, PaymentMethod.Cash, [new ShopSaleLineRequest(_pieces.Id, pieces, rate)], null, clientRequestId);

    private Task<decimal> ShopPieces() => _stock.GetQuantityOnHandAsync(_pieces.Id, KnownStockLocations.OwnShopId, default);

    [Fact]
    public async Task Owners_example_47_pieces_at_the_standard_rate_to_a_walk_in_customer()
    {
        var sale = await _sales.CreateAsync(Sale(47m), default);

        var line = Assert.Single(sale.Lines);
        Assert.Equal((47m, 1.60m, 75.20m), (line.Quantity, line.UnitPrice, line.LineTotal));
        Assert.Equal(75.20m, sale.TotalAmount);
        Assert.Null(sale.CustomerId);
        Assert.Null(sale.CustomerName);
        Assert.Equal(ShopSaleStatus.Completed, sale.Status);
        Assert.Matches(@"^OS/\d{2}-\d{2}/000001$", sale.SaleNumber);
        Assert.Equal("Own shop", sale.LocationName);

        Assert.Equal(5953m, await ShopPieces());

        var movement = await _database.Db.StockMovements.SingleAsync(m => m.ReferenceId == sale.Id);
        Assert.Equal(
            (StockMovementType.Sale, StockReferenceType.ShopSale, KnownStockLocations.OwnShopId, -47m),
            (movement.MovementType, movement.ReferenceType, movement.LocationId, movement.Quantity));
    }

    [Fact]
    public async Task Owners_example_a_caterer_buys_100_pieces_at_the_wholesale_rate()
    {
        var caterer = await _database.SeedCustomerAsync("ABC Catering");

        var sale = await _sales.CreateAsync(Sale(100m, 1.30m, caterer.Id), default);

        Assert.Equal(130.00m, sale.TotalAmount);
        Assert.Equal((caterer.Id, "ABC Catering"), (sale.CustomerId, sale.CustomerName));
        Assert.Equal((1.60m, 1.30m), (sale.Lines[0].DefaultPrice, sale.Lines[0].MinimumPrice));
        Assert.True((await _sales.GetListAsync(null, null, caterer.Id, null, default)).Single().BelowStandardRate);

        // Paid at the counter: nothing is owed and no bill is made.
        Assert.Empty(await _database.Db.Invoices.ToListAsync());
        Assert.Empty(await _database.Db.Payments.ToListAsync());
    }

    [Theory]
    [InlineData(1.30, true)]
    [InlineData(1.45, true)]
    [InlineData(1.60, true)]
    [InlineData(1.29, false)] // below the minimum
    [InlineData(1.61, false)] // above the rate, which is the most a piece sells for
    public async Task The_rate_stays_between_the_minimum_and_the_standard_rate(decimal rate, bool allowed)
    {
        if (allowed)
        {
            var sale = await _sales.CreateAsync(Sale(10m, rate), default);
            Assert.Equal(rate, sale.Lines[0].UnitPrice);
        }
        else
        {
            await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(Sale(10m, rate), default));
            Assert.Equal(6000m, await ShopPieces());
            Assert.Empty(await _database.Db.ShopSales.ToListAsync());
        }
    }

    [Fact]
    public async Task With_no_minimum_set_the_rate_cannot_be_lowered()
    {
        _pieces.MinimumSellingPrice = null;
        await _database.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(Sale(10m, 1.50m), default));
        Assert.Equal(1.60m, (await _sales.CreateAsync(Sale(10m), default)).Lines[0].UnitPrice);
    }

    [Fact]
    public async Task The_admin_can_change_the_minimum_and_it_applies_from_the_next_sale()
    {
        var before = await _sales.CreateAsync(Sale(10m, 1.30m), default);

        _pieces.MinimumSellingPrice = 1.40m;
        await _database.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(Sale(10m, 1.30m), default));
        Assert.Equal(1.30m, (await _sales.GetAsync(before.Id, default)).Lines[0].MinimumPrice);
    }

    [Fact]
    public async Task A_customers_agreed_rate_per_piece_is_where_their_sale_starts()
    {
        var caterer = await _database.SeedCustomerAsync("ABC Catering");
        await new CustomerPriceService(_database.Db, _database.CurrentUser).SetAsync(caterer.Id, _pieces.Id, 1.35m, default);

        var sale = await _sales.CreateAsync(Sale(100m, customerId: caterer.Id), default);

        Assert.Equal(1.35m, sale.Lines[0].UnitPrice);
        Assert.Equal(135m, sale.TotalAmount);
    }

    [Fact]
    public async Task An_agreed_rate_outside_the_band_is_refused()
    {
        var caterer = await _database.SeedCustomerAsync("ABC Catering");
        var prices = new CustomerPriceService(_database.Db, _database.CurrentUser);

        await Assert.ThrowsAsync<DomainException>(() => prices.SetAsync(caterer.Id, _pieces.Id, 1.20m, default));
        await Assert.ThrowsAsync<DomainException>(() => prices.SetAsync(caterer.Id, _pieces.Id, 1.70m, default));
    }

    [Fact]
    public async Task Selling_more_pieces_than_the_shop_has_is_refused_and_writes_nothing()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(Sale(6001m), default));

        Assert.Contains("The shop has 6,000 pieces", exception.Message);
        Assert.Equal(6000m, await ShopPieces());
        Assert.Empty(await _database.Db.ShopSales.ToListAsync());

        // No number was used up by the refusal.
        var next = await _sales.CreateAsync(Sale(6000m), default);
        Assert.EndsWith("/000001", next.SaleNumber);
        Assert.Equal(0m, await ShopPieces());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(2.5)]
    public async Task Pieces_are_whole_and_more_than_zero(decimal pieces)
    {
        await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(Sale(pieces), default));
    }

    [Fact]
    public async Task Only_the_shops_pieces_products_are_sold_here()
    {
        var packet = await _database.Db.Products.FirstAsync(p => p.Kind == ProductKind.Packed);

        var request = new CreateShopSaleRequest(null, PaymentMethod.Cash, [new ShopSaleLineRequest(packet.Id, 1m)]);

        await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(request, default));
    }

    [Fact]
    public async Task A_return_credit_is_not_a_way_to_pay_at_the_counter()
    {
        var request = Sale(10m) with { PaymentMethod = PaymentMethod.ReturnCredit };

        await Assert.ThrowsAsync<DomainException>(() => _sales.CreateAsync(request, default));
    }

    [Fact]
    public async Task Two_sales_cannot_both_take_the_last_pieces()
    {
        await _sales.CreateAsync(Sale(5950m), default);

        async Task<bool> SellFifty()
        {
            await using var db = _database.NewContext();
            try
            {
                await NewService(db).CreateAsync(Sale(50m), default);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(SellFifty(), SellFifty());

        Assert.Single(outcomes, sold => sold);
        Assert.Equal(0m, await ShopPieces());
    }

    [Fact]
    public async Task The_same_sale_sent_twice_is_sold_once()
    {
        var clientRequestId = Guid.NewGuid();

        var first = await _sales.CreateAsync(Sale(47m, clientRequestId: clientRequestId), default);
        var second = await _sales.CreateAsync(Sale(47m, clientRequestId: clientRequestId), default);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(5953m, await ShopPieces());
    }

    [Fact]
    public async Task Cancelling_puts_the_pieces_back_and_keeps_the_sale_and_its_number()
    {
        var sale = await _sales.CreateAsync(Sale(47m), default);

        var cancelled = await _sales.CancelAsync(sale.Id, "Wrong quantity", default);

        Assert.Equal(ShopSaleStatus.Cancelled, cancelled.Status);
        Assert.Equal(sale.SaleNumber, cancelled.SaleNumber);
        Assert.Equal("Wrong quantity", cancelled.CancellationReason);
        Assert.Equal(6000m, await ShopPieces());
        await Assert.ThrowsAsync<DomainException>(() => _sales.CancelAsync(sale.Id, "Again", default));
    }

    [Fact]
    public async Task A_sale_cannot_be_edited_once_made()
    {
        var sale = await _sales.CreateAsync(Sale(47m), default);
        var tracked = await _database.Db.ShopSales.SingleAsync(s => s.Id == sale.Id);

        tracked.TotalAmount = 1m;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.Db.SaveChangesAsync());
    }
}
