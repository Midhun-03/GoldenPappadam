using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Packing;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Packing converts loose stock into packets (CLAUDE.md §4 "Packing conversion", owner's answers
/// 2026-09-30): what the loose loses is calculated - packets × pieces ÷ the variety's pieces per kg, or
/// packets × weight - and packing is refused when the warehouse does not hold enough.
/// </summary>
public class PackingServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private PackingService _packing = null!;
    private StockService _stock = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _packing = new PackingService(_database.Db, _stock);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static CreatePackingRequest Pack(Guid productId, decimal packs, Guid? clientRequestId = null) =>
        new(productId, packs, null, null, clientRequestId);

    private async Task<Product> CountBasedPacketAsync(Product loose, int pieces, string code = "PKT-20")
    {
        var packet = new Product
        {
            ProductCode = code,
            Name = $"{pieces} piece packet",
            CategoryId = loose.CategoryId,
            Kind = ProductKind.Packed,
            UnitOfMeasureId = (await _database.Db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT")).Id,
            SourceProductId = loose.Id,
            PiecesPerPack = pieces
        };
        _database.Db.Add(packet);
        await _database.Db.SaveChangesAsync();
        return packet;
    }

    [Fact]
    public async Task Owners_example_100_packets_of_20_take_10_kg_out_of_50()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 50m);

        var result = await _packing.CreateAsync(Pack(packet.Id, 100m), default);

        // 100 × 20 = 2,000 pieces = 10 kg at 200 per kg.
        Assert.Equal(10m, result.SourceQuantityUsed);
        Assert.Equal(40m, result.SourceQuantityOnHand);
        Assert.Equal(100m, result.PackedQuantityOnHand);
        Assert.Equal(2000m, result.Plan.PiecesUsed);

        var movements = await _database.Db.StockMovements
            .Where(m => m.ReferenceId == result.PackingEntryId)
            .OrderBy(m => m.Quantity)
            .ToListAsync();

        // Loose down and packets up, both pointing at the one packing entry.
        Assert.Equal([(loose.Id, -10m), (packet.Id, 100m)], movements.Select(m => (m.ProductId, m.Quantity)));
        Assert.All(movements, m => Assert.Equal(StockMovementType.Packing, m.MovementType));
        Assert.All(movements, m => Assert.Equal(StockReferenceType.PackingEntry, m.ReferenceType));
    }

    [Fact]
    public async Task Rule_example_250_packets_of_20_take_25_kg_out_of_1000()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 1000m);

        var result = await _packing.CreateAsync(Pack(packet.Id, 250m), default);

        Assert.Equal(25m, result.SourceQuantityUsed);
        Assert.Equal(975m, result.SourceQuantityOnHand);
    }

    [Fact]
    public async Task A_weight_based_packet_uses_its_weight()
    {
        // The seeded packet is 250 g.
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 100m);

        var result = await _packing.CreateAsync(Pack(packet.Id, 100m), default);

        Assert.Equal(25m, result.SourceQuantityUsed);
        Assert.Equal(75m, result.SourceQuantityOnHand);
        Assert.Null(result.Plan.PiecesUsed);
    }

    [Fact]
    public async Task A_larger_pappadam_is_converted_with_its_own_pieces_per_kg()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        loose.PiecesPerKg = 160m;
        await _database.Db.SaveChangesAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 50m);

        var result = await _packing.CreateAsync(Pack(packet.Id, 100m), default);

        // 2,000 pieces at 160 per kg is 12.5 kg, not the standard pappadam's 10.
        Assert.Equal(12.5m, result.SourceQuantityUsed);
    }

    [Fact]
    public async Task Packing_more_than_the_loose_stock_holds_is_refused_and_writes_nothing()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 10m);
        var movementsBefore = await _database.Db.StockMovements.CountAsync();

        // 150 × 20 = 3,000 pieces, but 10 kg is only 2,000.
        var exception = await Assert.ThrowsAsync<DomainException>(() => _packing.CreateAsync(Pack(packet.Id, 150m), default));

        Assert.Contains("needs 3,000 pieces (15 KG)", exception.Message);
        Assert.Contains("has 2,000 pieces (10 KG)", exception.Message);
        Assert.Equal(movementsBefore, await _database.Db.StockMovements.CountAsync());
        Assert.Empty(await _database.Db.PackingEntries.ToListAsync());
        Assert.Equal(10m, await _stock.GetQuantityOnHandAsync(loose.Id, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task Exactly_all_the_loose_stock_can_be_packed()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 10m);

        var result = await _packing.CreateAsync(Pack(packet.Id, 100m), default);

        Assert.Equal(0m, result.SourceQuantityOnHand);
    }

    [Fact]
    public async Task The_preview_shows_the_shortfall_the_save_would_refuse()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 10m);

        var fits = await _packing.PreviewAsync(new PackingPreviewRequest(packet.Id, 50m), default);
        var tooMany = await _packing.PreviewAsync(new PackingPreviewRequest(packet.Id, 150m), default);

        Assert.Equal((10m, 5m, 5m, 0.1m), (fits.SourceOnHandBefore, fits.SourceUsed, fits.SourceOnHandAfter, fits.SourcePerPack));
        Assert.Null(fits.Shortfall);
        Assert.NotNull(tooMany.Shortfall);
        Assert.Empty(await _database.Db.PackingEntries.ToListAsync());
    }

    [Fact]
    public async Task The_entry_keeps_the_conversion_it_used_when_the_figure_changes_later()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 50m);

        var result = await _packing.CreateAsync(Pack(packet.Id, 100m), default);

        loose.PiecesPerKg = 180m;
        await _database.Db.SaveChangesAsync();

        var entry = Assert.Single(await _packing.GetHistoryAsync(null, null, null, default));
        Assert.Equal(result.PackingEntryId, entry.Id);
        Assert.Equal((20, 200m, 0.1m, 50m, 40m), (entry.PiecesPerPack, entry.PiecesPerKg, entry.SourcePerPack, entry.SourceOnHandBefore, entry.SourceOnHandAfter));
    }

    [Fact]
    public async Task Pressing_save_twice_packs_once()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 50m);
        var clientRequestId = Guid.NewGuid();

        var first = await _packing.CreateAsync(Pack(packet.Id, 100m, clientRequestId), default);
        var second = await _packing.CreateAsync(Pack(packet.Id, 100m, clientRequestId), default);

        Assert.Equal(first.PackingEntryId, second.PackingEntryId);
        Assert.Single(await _database.Db.PackingEntries.ToListAsync());
        Assert.Equal(40m, await _stock.GetQuantityOnHandAsync(loose.Id, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task Two_packings_of_the_last_loose_stock_at_once_cannot_both_succeed()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 10m);

        // Each needs all 10 kg. Separate contexts, as two office computers would be.
        async Task<bool> PackAsync()
        {
            await using var db = _database.NewContext();
            try
            {
                await new PackingService(db, new StockService(db)).CreateAsync(Pack(packet.Id, 100m), default);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }

        var outcomes = await Task.WhenAll(PackAsync(), PackAsync());

        Assert.Equal(1, outcomes.Count(ok => ok));
        Assert.Equal(0m, await _stock.GetQuantityOnHandAsync(loose.Id, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task A_count_based_packet_from_a_pappadam_with_no_pieces_per_kg_asks_rather_than_guesses()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        loose.PiecesPerKg = null;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(loose.Id, 50m);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _packing.CreateAsync(Pack(packet.Id, 10m), default));

        Assert.Contains("has no pieces per kg", exception.Message);
    }

    [Fact]
    public async Task A_packet_of_pieces_is_counted_whole()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var packet = await CountBasedPacketAsync(loose, 20);
        await _database.AddStockAsync(loose.Id, 50m);

        await Assert.ThrowsAsync<DomainException>(() => _packing.CreateAsync(Pack(packet.Id, 10.5m), default));
    }

    [Fact]
    public async Task Packing_into_a_loose_product_is_rejected()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        var exception = await Assert.ThrowsAsync<DomainException>(() => _packing.CreateAsync(Pack(loose.Id, 10m), default));

        Assert.Contains("loose product", exception.Message);
    }

    [Fact]
    public async Task A_box_is_packed_from_packets_and_refused_when_there_are_too_few()
    {
        var (_, packet) = await _database.SeedProductsAsync();
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

        var result = await _packing.CreateAsync(Pack(box.Id, 5m), default);

        Assert.Equal(packet.Id, result.SourceProductId);
        Assert.Equal(60m, result.SourceQuantityUsed);
        Assert.Equal(60m, result.SourceQuantityOnHand);
        Assert.Equal(5m, result.PackedQuantityOnHand);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _packing.CreateAsync(Pack(box.Id, 6m), default));
        Assert.Contains("needs 72 PKT", exception.Message);
    }
}
