using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

public class ProductServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ProductService _products = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _products = new ProductService(_database.Db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_packed_product_without_a_source_is_rejected()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(
            new SaveProductRequest("PKT-6", "6 piece packet", loose.CategoryId, ProductKind.Packed,
                loose.UnitOfMeasureId, null, null, null, null),
            default));
    }

    [Fact]
    public async Task Several_packet_sizes_can_share_one_loose_source()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        foreach (var (code, perPack) in new[] { ("PKT-6", 6m), ("PKT-20", 20m), ("PKT-25", 25m) })
        {
            var product = await _products.CreateAsync(
                new SaveProductRequest(code, code, loose.CategoryId, ProductKind.Packed,
                    loose.UnitOfMeasureId, loose.Id, perPack, 30m, null),
                default);

            Assert.Equal(loose.Id, product.SourceProductId);
        }
    }

    [Fact]
    public async Task A_product_cannot_be_packed_from_itself_through_a_chain()
    {
        var (loose, packet) = await _database.SeedProductsAsync();

        var box = await _products.CreateAsync(
            new SaveProductRequest("BOX-12", "Box of 12", packet.CategoryId, ProductKind.Packed,
                packet.UnitOfMeasureId, packet.Id, 12m, 360m, null),
            default);

        // Trying to make the packet come from the box closes the loop: box -> packet -> box.
        var exception = await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(
            packet.Id,
            new SaveProductRequest(packet.ProductCode, packet.Name, packet.CategoryId, ProductKind.Packed,
                packet.UnitOfMeasureId, box.Id, 1m, null, null),
            default));

        Assert.Contains("packed from itself", exception.Message);
        Assert.Equal(loose.Id, packet.SourceProductId);
    }

    [Fact]
    public async Task The_unit_cannot_change_once_stock_has_moved()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 10m);

        await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(
            loose.Id,
            new SaveProductRequest(loose.ProductCode, loose.Name, loose.CategoryId, ProductKind.Loose,
                packet.UnitOfMeasureId, null, null, null, null),
            default));
    }

    [Fact]
    public async Task A_product_cannot_switch_between_loose_and_packed()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(
            loose.Id,
            new SaveProductRequest(loose.ProductCode, loose.Name, loose.CategoryId, ProductKind.Packed,
                loose.UnitOfMeasureId, loose.Id, 5m, null, null),
            default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_product_cannot_be_saved_without_a_standard_price_above_zero(int? price)
    {
        var (loose, _) = await _database.SeedProductsAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(
            new SaveProductRequest("LOOSE-2", "Loose pappadam (large)", loose.CategoryId, ProductKind.Loose,
                loose.UnitOfMeasureId, null, null, price, null),
            default));

        Assert.Contains("standard price", error.Message);
        Assert.False(await _database.Db.Products.AnyAsync(p => p.ProductCode == "LOOSE-2"));
    }

    [Fact]
    public async Task A_product_that_has_a_price_cannot_have_it_taken_away()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var product = await CreateLooseAsync(loose, 150m);

        await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(product.Id, Request(product, null), default));
        await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(product.Id, Request(product, 0m), default));

        Assert.Equal(150m, product.SellingPrice);
    }

    [Fact]
    public async Task Every_change_to_the_standard_price_is_kept_with_what_it_was_before_and_who_changed_it()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var office = Guid.NewGuid();
        _database.CurrentUser.UserId = office;

        var product = await CreateLooseAsync(loose, 150m);
        await _products.UpdateAsync(product.Id, Request(product, 160m), default);

        // Saving the product again at the same price is not a price change.
        await _products.UpdateAsync(product.Id, Request(product, 160m), default);

        var history = await _database.Db.ProductPriceChanges
            .Where(c => c.ProductId == product.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        List<(decimal?, decimal)> expected = [(null, 150m), (150m, 160m)];
        Assert.Equal(expected, history.Select(h => (h.PreviousPrice, h.NewPrice)));
        Assert.All(history, row => Assert.Equal(office, row.CreatedBy));
    }

    private Task<Product> CreateLooseAsync(Product like, decimal price) =>
        _products.CreateAsync(
            new SaveProductRequest("LOOSE-2", "Loose pappadam (large)", like.CategoryId, ProductKind.Loose,
                like.UnitOfMeasureId, null, null, price, null),
            default);

    private static SaveProductRequest Request(Product product, decimal? price) =>
        new(product.ProductCode, product.Name, product.CategoryId, product.Kind, product.UnitOfMeasureId,
            null, null, price, null);
}
