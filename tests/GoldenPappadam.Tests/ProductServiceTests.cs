using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Domain.Inventory;

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
                packet.UnitOfMeasureId, packet.Id, 12m, null, null),
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
}
