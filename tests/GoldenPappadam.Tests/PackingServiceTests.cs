using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Packing;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

public class PackingServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private PackingService _packing = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _packing = new PackingService(_database.Db, new StockService(_database.Db));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Packing_moves_stock_from_source_to_packs()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 100m);

        // 40 packets of 250 g, using the suggested quantity.
        var result = await _packing.CreateAsync(new CreatePackingRequest(packet.Id, 40m, null, null, null), default);

        Assert.Equal(10m, result.SourceQuantityUsed);
        Assert.Equal(90m, result.SourceQuantityOnHand);
        Assert.Equal(40m, result.PackedQuantityOnHand);
        Assert.Null(result.Warning);

        var movements = await _database.Db.StockMovements
            .Where(m => m.ReferenceId == result.PackingEntryId)
            .OrderBy(m => m.Quantity)
            .ToListAsync();

        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal(StockMovementType.Packing, m.MovementType));
        Assert.All(movements, m => Assert.Equal(StockReferenceType.PackingEntry, m.ReferenceType));
        Assert.Equal(-10m, movements[0].Quantity);
        Assert.Equal(loose.Id, movements[0].ProductId);
        Assert.Equal(40m, movements[1].Quantity);
        Assert.Equal(packet.Id, movements[1].ProductId);
    }

    [Fact]
    public async Task Packing_records_the_quantity_actually_used_so_packing_loss_is_visible()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 100m);

        // 40 packets should take 10 kg, but 10.5 kg went in.
        var result = await _packing.CreateAsync(new CreatePackingRequest(packet.Id, 40m, 10.5m, null, null), default);

        Assert.Equal(10.5m, result.SourceQuantityUsed);
        Assert.Equal(89.5m, result.SourceQuantityOnHand);
    }

    [Fact]
    public async Task Packing_more_than_available_warns_but_still_saves()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 5m);

        var result = await _packing.CreateAsync(new CreatePackingRequest(packet.Id, 40m, null, null, null), default);

        Assert.Equal(-5m, result.SourceQuantityOnHand);
        Assert.NotNull(result.Warning);
        Assert.True(await _database.Db.PackingEntries.AnyAsync(e => e.Id == result.PackingEntryId));
    }

    [Fact]
    public async Task Packing_into_a_loose_product_is_rejected()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _packing.CreateAsync(new CreatePackingRequest(loose.Id, 10m, null, null, null), default));

        Assert.Contains("loose product", exception.Message);
    }

    [Fact]
    public async Task A_box_can_be_packed_from_packets()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(packet.Id, 120m);

        var box = new Product
        {
            ProductCode = "BOX-12",
            Name = "Box of 12 packets",
            CategoryId = packet.CategoryId,
            Kind = ProductKind.Packed,
            UnitOfMeasureId = packet.UnitOfMeasureId,
            SourceProductId = packet.Id,
            SourceQuantityPerPack = 12m
        };
        _database.Db.Add(box);
        await _database.Db.SaveChangesAsync();

        var result = await _packing.CreateAsync(new CreatePackingRequest(box.Id, 5m, null, null, null), default);

        Assert.Equal(packet.Id, result.SourceProductId);
        Assert.Equal(60m, result.SourceQuantityUsed);
        Assert.Equal(60m, result.SourceQuantityOnHand);
        Assert.Equal(5m, result.PackedQuantityOnHand);
    }
}
