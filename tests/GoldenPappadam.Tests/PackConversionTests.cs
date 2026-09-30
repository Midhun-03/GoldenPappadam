using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Api.Features.Inventory.Repacking;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The conversion as the product screen sets it up and repacking uses it: loose pappadam counted in kg
/// with its pieces per kg, and packets counted in pieces (CLAUDE.md §4 "Packing conversion").
/// </summary>
public class PackConversionTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private ProductService _products = null!;
    private StockService _stock = null!;
    private Product _loose = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _products = new ProductService(_database.Db);
        _stock = new StockService(_database.Db);
        (_loose, _) = await _database.SeedProductsAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private Task<Guid> PacketUnitAsync() =>
        _database.Db.UnitOfMeasures.Where(u => u.Code == "PKT").Select(u => u.Id).FirstAsync();

    private async Task<Product> PacketAsync(string code, int? pieces, decimal? quantity = null, Guid? source = null) =>
        await _products.CreateAsync(
            new SaveProductRequest(code, code, _loose.CategoryId, ProductKind.Packed, await PacketUnitAsync(),
                source ?? _loose.Id, quantity, null, null, PiecesPerPack: pieces),
            default);

    [Fact]
    public async Task A_packet_holds_pieces_or_a_quantity_but_not_both_and_not_neither()
    {
        var both = await Assert.ThrowsAsync<DomainException>(() => PacketAsync("PKT-X", 20, 0.1m));
        var neither = await Assert.ThrowsAsync<DomainException>(() => PacketAsync("PKT-Y", null));

        Assert.Contains("one, not both", both.Message);
        Assert.Contains("one, not both", neither.Message);
    }

    [Fact]
    public async Task The_product_shows_what_one_packet_uses_of_the_loose()
    {
        var twenty = await PacketAsync("PKT-20", 20);

        var dto = await ProductQueries.Project(_database.Db.Products.Where(p => p.Id == twenty.Id)).SingleAsync();

        Assert.Equal((20, 0.1m), (dto.PiecesPerPack, dto.SourcePerPack));
        Assert.Null(dto.SourceQuantityPerPack);
    }

    [Fact]
    public async Task A_box_counts_packets_not_pieces()
    {
        var twenty = await PacketAsync("PKT-20", 20);

        var exception = await Assert.ThrowsAsync<DomainException>(() => PacketAsync("BOX-12", 240, source: twenty.Id));

        Assert.Contains("number of packets", exception.Message);
        Assert.Equal(12m, (await PacketAsync("BOX-12", null, 12m, twenty.Id)).SourceQuantityPerPack);
    }

    [Fact]
    public async Task Pieces_per_kg_belongs_to_loose_kg_and_cannot_be_taken_off_while_packets_need_it()
    {
        var twenty = await PacketAsync("PKT-20", 20);

        var onPacket = await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(twenty.Id,
            new SaveProductRequest(twenty.ProductCode, twenty.Name, twenty.CategoryId, ProductKind.Packed,
                twenty.UnitOfMeasureId, _loose.Id, null, null, null, PiecesPerPack: 20, PiecesPerKg: 200m), default));

        var cleared = await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(_loose.Id,
            new SaveProductRequest(_loose.ProductCode, _loose.Name, _loose.CategoryId, ProductKind.Loose,
                _loose.UnitOfMeasureId, null, null, null, null), default));

        Assert.Contains("loose product counted in kg", onPacket.Message);
        Assert.Contains("needs its pieces per kg", cleared.Message);
    }

    [Fact]
    public async Task Repacking_counts_in_pieces_and_returns_the_leftover_as_kg()
    {
        var twenty = await PacketAsync("PKT-20", 20);
        var six = await PacketAsync("PKT-6", 6);
        await _database.AddStockAsync(twenty.Id, 5m);

        var plan = await new RepackingService(_database.Db, _stock)
            .PreviewAsync(new RepackRequest(twenty.Id, 5m, six.Id, null, null), default);

        // 5 × 20 = 100 pieces = 16 × 6 + 4 pieces; 4 pieces at 200 per kg is 0.020 kg.
        Assert.Equal((16m, 0.02m), (plan.ToQuantity, plan.LeftoverQuantity));
        Assert.Equal(_loose.Id, plan.LeftoverProductId);
    }

    [Fact]
    public async Task Repacking_a_pappadam_with_an_awkward_pieces_per_kg_still_divides_exactly()
    {
        _loose.PiecesPerKg = 170m;
        await _database.Db.SaveChangesAsync();
        var twenty = await PacketAsync("PKT-20", 20);
        var ten = await PacketAsync("PKT-10", 10);
        await _database.AddStockAsync(twenty.Id, 5m);

        var plan = await new RepackingService(_database.Db, _stock)
            .PreviewAsync(new RepackRequest(twenty.Id, 5m, ten.Id, null, null), default);

        // In kg, 20 ÷ 170 repeats and ten packets could come out as nine.
        Assert.Equal((10m, 0m), (plan.ToQuantity, plan.LeftoverQuantity));
    }
}
