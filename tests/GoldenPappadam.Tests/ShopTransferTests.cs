using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Packing;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.OwnShop.Transfers;
using GoldenPappadam.Api.Features.Reports;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Loose pappadam sent from the factory to the own shop (owner, 2026-09-30): recorded in kg, received in
/// pieces at the variety's own pieces per kg, rounded to the nearest whole piece, and refused when the
/// warehouse does not hold the kg.
/// </summary>
public class ShopTransferTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private StockService _stock = null!;
    private ShopTransferService _transfers = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _transfers = new ShopTransferService(_database.Db, _stock);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static CreateShopTransferRequest Send(Guid looseId, decimal kg, Guid? clientRequestId = null) =>
        new(looseId, kg, null, null, clientRequestId);

    private Task<decimal> FactoryKg(Guid looseId) =>
        _stock.GetQuantityOnHandAsync(looseId, KnownStockLocations.MainWarehouseId, default);

    private Task<decimal> ShopPieces(Guid piecesId) =>
        _stock.GetQuantityOnHandAsync(piecesId, KnownStockLocations.OwnShopId, default);

    [Fact]
    public async Task Owners_example_30_kg_arrives_as_6000_pieces()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(loose.Id, 50m);

        var result = await _transfers.CreateAsync(Send(loose.Id, 30m), default);

        Assert.Equal(6000m, result.PiecesReceived);
        Assert.Equal(200m, result.PiecesPerKg);
        Assert.Equal((50m, 20m), (result.SourceOnHandBefore, result.SourceOnHandAfter));
        Assert.Equal((0m, 6000m), (result.ShopOnHandBefore, result.ShopOnHandAfter));
        Assert.Equal("Main warehouse", result.FromLocationName);
        Assert.Equal("Own shop", result.ToLocationName);

        Assert.Equal(20m, await FactoryKg(loose.Id));
        Assert.Equal(6000m, await ShopPieces(pieces.Id));

        // Kg out of the warehouse and pieces into the shop, both pointing at the one transfer.
        var movements = await _database.Db.StockMovements
            .Where(m => m.ReferenceId == result.Id)
            .OrderBy(m => m.Quantity)
            .ToListAsync();
        Assert.Equal(
            [(loose.Id, KnownStockLocations.MainWarehouseId, -30m), (pieces.Id, KnownStockLocations.OwnShopId, 6000m)],
            movements.Select(m => (m.ProductId, m.LocationId, m.Quantity)));
        Assert.All(movements, m => Assert.Equal(StockMovementType.ShopTransfer, m.MovementType));
        Assert.All(movements, m => Assert.Equal(StockReferenceType.ShopTransfer, m.ReferenceType));
    }

    [Fact]
    public async Task A_larger_pappadam_is_converted_with_its_own_pieces_per_kg()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        loose.PiecesPerKg = 160m;
        await _database.Db.SaveChangesAsync();
        var pieces = await _database.SeedPiecesAsync(loose, rate: 2m, minimum: null);
        await _database.AddStockAsync(loose.Id, 10m);

        var result = await _transfers.CreateAsync(Send(loose.Id, 2.5m), default);

        Assert.Equal(400m, result.PiecesReceived);
        Assert.Equal(400m, await ShopPieces(pieces.Id));
    }

    [Theory]
    [InlineData(1.003, 170, 171)] // 170.51 pieces
    [InlineData(1.002, 170, 170)] // 170.34 pieces
    [InlineData(30.001, 200, 6000)] // 6,000.2 pieces
    public async Task Pieces_are_rounded_to_the_nearest_whole_piece(decimal kg, decimal perKg, decimal expected)
    {
        var (loose, _) = await _database.SeedProductsAsync();
        loose.PiecesPerKg = perKg;
        await _database.Db.SaveChangesAsync();
        var pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(loose.Id, 50m);

        var result = await _transfers.CreateAsync(Send(loose.Id, kg), default);

        Assert.Equal(expected, result.PiecesReceived);
        // The factory loses exactly the kg it recorded.
        Assert.Equal(50m - kg, await FactoryKg(loose.Id));
        Assert.Equal(expected, await ShopPieces(pieces.Id));
    }

    [Fact]
    public async Task Sending_more_than_the_warehouse_holds_is_refused_and_writes_nothing()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(loose.Id, 20m);
        var movementsBefore = await _database.Db.StockMovements.CountAsync();

        var preview = await _transfers.PreviewAsync(new ShopTransferPreviewRequest(loose.Id, 30m), default);
        var exception = await Assert.ThrowsAsync<DomainException>(() => _transfers.CreateAsync(Send(loose.Id, 30m), default));

        Assert.Equal(exception.Message, preview.Shortfall);
        Assert.Contains("the warehouse has 20 kg", exception.Message);
        Assert.Equal(movementsBefore, await _database.Db.StockMovements.CountAsync());
        Assert.Empty(await _database.Db.ShopTransfers.ToListAsync());
        Assert.Equal(20m, await FactoryKg(loose.Id));
        Assert.Equal(0m, await ShopPieces(pieces.Id));
    }

    [Fact]
    public async Task A_variety_the_shop_has_no_pieces_product_for_is_refused()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 50m);

        var exception = await Assert.ThrowsAsync<DomainException>(() => _transfers.CreateAsync(Send(loose.Id, 5m), default));

        Assert.Contains("no pieces product", exception.Message);
    }

    [Fact]
    public async Task Only_loose_pappadam_counted_in_kg_is_sent()
    {
        var (loose, packet) = await _database.SeedProductsAsync();
        await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(packet.Id, 50m);

        await Assert.ThrowsAsync<DomainException>(() => _transfers.CreateAsync(Send(packet.Id, 5m), default));
    }

    [Fact]
    public async Task The_same_transfer_sent_twice_is_recorded_once()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(loose.Id, 50m);
        var clientRequestId = Guid.NewGuid();

        var first = await _transfers.CreateAsync(Send(loose.Id, 10m, clientRequestId), default);
        var second = await _transfers.CreateAsync(Send(loose.Id, 10m, clientRequestId), default);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await _database.Db.ShopTransfers.ToListAsync());
        Assert.Equal(40m, await FactoryKg(loose.Id));
        Assert.Equal(2000m, await ShopPieces(pieces.Id));
    }

    [Fact]
    public async Task A_later_change_to_pieces_per_kg_does_not_rewrite_a_transfer()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(loose.Id, 50m);
        await _transfers.CreateAsync(Send(loose.Id, 10m), default);

        loose.PiecesPerKg = 180m;
        await _database.Db.SaveChangesAsync();

        var history = await _transfers.GetHistoryAsync(null, null, default);
        Assert.Equal((200m, 2000m), (history.Single().PiecesPerKg, history.Single().PiecesReceived));
        Assert.Equal(2000m, await ShopPieces(pieces.Id));
    }

    [Fact]
    public async Task A_transfer_and_a_packing_cannot_both_take_the_last_kilograms()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.SeedPiecesAsync(loose);
        var packet = new Product
        {
            ProductCode = "PKT-20",
            Name = "20 piece packet",
            CategoryId = loose.CategoryId,
            Kind = ProductKind.Packed,
            UnitOfMeasureId = (await _database.Db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT")).Id,
            SourceProductId = loose.Id,
            PiecesPerPack = 20
        };
        _database.Db.Add(packet);
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(loose.Id, 10m);

        async Task<bool> Succeeds(Func<Task> action)
        {
            try
            {
                await action();
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }

        // 10 kg each, from its own connection, at the same moment.
        var outcomes = await Task.WhenAll(
            Succeeds(async () =>
            {
                await using var db = _database.NewContext();
                await new ShopTransferService(db, new StockService(db)).CreateAsync(Send(loose.Id, 10m), default);
            }),
            Succeeds(async () =>
            {
                await using var db = _database.NewContext();
                await new PackingService(db, new StockService(db)).CreateAsync(new CreatePackingRequest(packet.Id, 100m, null, null), default);
            }));

        Assert.Single(outcomes, succeeded => succeeded);
        Assert.Equal(0m, await FactoryKg(loose.Id));
    }

    [Fact]
    public async Task The_stock_movement_report_shows_the_kg_moved_out_and_the_pieces_moved_in()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        var pieces = await _database.SeedPiecesAsync(loose);
        await _database.AddStockAsync(loose.Id, 50m);
        await _transfers.CreateAsync(Send(loose.Id, 30m), default);

        var today = IndiaTime.Today();
        var report = await new StockMovementReport(_database.Db).BuildAsync(today, today, default);

        var rows = report.Sections.SelectMany(s => s.Rows).ToList();
        var factory = rows.Single(r => ((string)r["product"]!).StartsWith(loose.Name + " (KG)"));
        var shop = rows.Single(r => ((string)r["product"]!).StartsWith(pieces.Name));

        Assert.Equal(-30m, factory["movedOut"]);
        Assert.Equal(20m, factory["closing"]);
        Assert.Equal(6000m, shop["movedIn"]);
        Assert.Equal(6000m, shop["closing"]);
    }
}
