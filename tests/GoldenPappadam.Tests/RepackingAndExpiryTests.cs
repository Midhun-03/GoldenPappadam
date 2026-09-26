using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Repacking;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Reports;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Repacking, shelf life and writing off expired stock against real SQL Server: loose pappadam in
/// pieces, packed as 20-, 10- and 6-piece packets with a 20-day life.
/// </summary>
public class RepackingAndExpiryTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private StockService _stock = null!;
    private RepackingService _repacking = null!;
    private StockAgeService _ages = null!;
    private Product _loose = null!;
    private Product _twenty = null!;
    private Product _ten = null!;
    private Product _six = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _stock = new StockService(_database.Db);
        _repacking = new RepackingService(_database.Db, _stock);
        _ages = new StockAgeService(_database.Db, _stock);

        var category = new ProductCategory { Name = "Pappadam" };
        _database.Db.Add(category);
        await _database.Db.SaveChangesAsync();

        var pieces = await _database.Db.UnitOfMeasures.FirstAsync(u => u.Code == "PCS");
        var packet = await _database.Db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT");

        _loose = new Product { ProductCode = "LOOSE", Name = "Loose pappadam", CategoryId = category.Id, Kind = ProductKind.Loose, UnitOfMeasureId = pieces.Id, ShelfLifeDays = 20 };
        _database.Db.Add(_loose);
        await _database.Db.SaveChangesAsync();

        Product Packed(string code, decimal size) => new()
        {
            ProductCode = code, Name = $"{size:0}-piece packet", CategoryId = category.Id, Kind = ProductKind.Packed,
            UnitOfMeasureId = packet.Id, SourceProductId = _loose.Id, SourceQuantityPerPack = size, ShelfLifeDays = 20
        };

        _twenty = Packed("PKT-20", 20m);
        _ten = Packed("PKT-10", 10m);
        _six = Packed("PKT-6", 6m);
        _database.Db.AddRange(_twenty, _ten, _six);
        await _database.Db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private async Task PackedOnAsync(Product product, decimal quantity, int daysAgo, Guid? location = null)
    {
        _database.Db.Add(new StockMovement
        {
            ProductId = product.Id,
            LocationId = location ?? KnownStockLocations.MainWarehouseId,
            MovementType = StockMovementType.Packing,
            Quantity = quantity,
            OccurredAt = IndiaTime.DayRangeUtc(IndiaTime.Today().AddDays(-daysAgo)).Start.AddHours(10)
        });
        await _database.Db.SaveChangesAsync();
    }

    private Task<decimal> OnHandAsync(Product product) =>
        _stock.GetQuantityOnHandAsync(product.Id, KnownStockLocations.MainWarehouseId, default);

    [Fact]
    public async Task Five_twenty_piece_packets_become_exactly_ten_ten_piece_packets()
    {
        await PackedOnAsync(_twenty, 5m, daysAgo: 7);

        var result = (await _repacking.CreateAsync(new RepackRequest(_twenty.Id, 5m, _ten.Id, null, null), default)).Result;

        Assert.Equal(10m, result.ToQuantity);
        Assert.Equal(0m, result.LeftoverQuantity);
        Assert.Null(result.Warning);
        Assert.Equal(0m, await OnHandAsync(_twenty));
        Assert.Equal(10m, await OnHandAsync(_ten));
    }

    [Fact]
    public async Task Pieces_that_do_not_fill_a_packet_go_back_to_loose_and_nothing_is_lost()
    {
        await PackedOnAsync(_twenty, 5m, daysAgo: 7);

        var result = (await _repacking.CreateAsync(new RepackRequest(_twenty.Id, 5m, _six.Id, null, null), default)).Result;

        // 100 pieces: sixteen 6-piece packets (96) and 4 pieces loose.
        Assert.Equal(16m, result.ToQuantity);
        Assert.Equal(4m, result.LeftoverQuantity);
        Assert.Equal(_loose.Id, result.LeftoverProductId);
        Assert.Equal(100m, await OnHandAsync(_six) * 6m + await OnHandAsync(_loose) + await OnHandAsync(_twenty) * 20m);

        var entry = await _database.Db.RepackEntries.SingleAsync();
        Assert.Equal(3, await _database.Db.StockMovements.CountAsync(m => m.ReferenceId == entry.Id));
    }

    [Fact]
    public async Task Repacked_packets_start_a_fresh_life_and_the_loose_left_over_does_not()
    {
        await PackedOnAsync(_twenty, 5m, daysAgo: 8);

        await _repacking.CreateAsync(new RepackRequest(_twenty.Id, 5m, _six.Id, null, null), default);
        var rows = await _ages.GetAsync(IndiaTime.Today(), default);

        var six = rows.Single(r => r.ProductId == _six.Id);
        Assert.Equal(0, Assert.Single(six.Layers).AgeDays);
        Assert.Equal(16m, six.Fresh);

        var loose = rows.Single(r => r.ProductId == _loose.Id);
        Assert.Equal(8, Assert.Single(loose.Layers).AgeDays);
    }

    [Fact]
    public async Task Repacking_into_the_same_size_keeps_the_count_but_resets_the_age()
    {
        await PackedOnAsync(_twenty, 5m, daysAgo: 9);

        var result = (await _repacking.CreateAsync(new RepackRequest(_twenty.Id, 5m, _twenty.Id, null, null), default)).Result;

        Assert.Equal(5m, result.ToQuantity);
        Assert.Equal(5m, await OnHandAsync(_twenty));
        var row = (await _ages.GetAsync(IndiaTime.Today(), default)).Single(r => r.ProductId == _twenty.Id);
        Assert.Equal(0, Assert.Single(row.Layers).AgeDays);
    }

    [Fact]
    public async Task Things_that_cannot_be_repacked_are_refused_with_a_reason()
    {
        var other = new Product
        {
            ProductCode = "OTHER-LOOSE", Name = "Masala pappadam", CategoryId = _loose.CategoryId, Kind = ProductKind.Loose,
            UnitOfMeasureId = _loose.UnitOfMeasureId
        };
        _database.Db.Add(other);
        await _database.Db.SaveChangesAsync();
        var masalaPacket = new Product
        {
            ProductCode = "MASALA-10", Name = "Masala 10-piece", CategoryId = _loose.CategoryId, Kind = ProductKind.Packed,
            UnitOfMeasureId = _twenty.UnitOfMeasureId, SourceProductId = other.Id, SourceQuantityPerPack = 10m
        };
        _database.Db.Add(masalaPacket);
        await _database.Db.SaveChangesAsync();

        var differentVariety = await Assert.ThrowsAsync<DomainException>(() =>
            _repacking.PreviewAsync(new RepackRequest(_twenty.Id, 5m, masalaPacket.Id, null, null), default));
        Assert.Contains("cannot be repacked", differentVariety.Message);

        var loose = await Assert.ThrowsAsync<DomainException>(() =>
            _repacking.PreviewAsync(new RepackRequest(_loose.Id, 5m, _ten.Id, null, null), default));
        Assert.Contains("Packing screen", loose.Message);

        var tooFew = await Assert.ThrowsAsync<DomainException>(() =>
            _repacking.PreviewAsync(new RepackRequest(_six.Id, 1m, _ten.Id, null, null), default));
        Assert.Contains("Open more packets", tooFew.Message);

        Assert.Empty(await _database.Db.RepackEntries.ToListAsync());
    }

    [Fact]
    public async Task Opening_more_packets_than_the_warehouse_holds_warns_but_still_repacks()
    {
        var result = (await _repacking.CreateAsync(new RepackRequest(_twenty.Id, 2m, _ten.Id, null, null), default)).Result;

        Assert.NotNull(result.Warning);
        Assert.Equal(-2m, await OnHandAsync(_twenty));
    }

    [Fact]
    public async Task Stock_is_expiring_on_day_twenty_and_expired_on_day_twenty_one()
    {
        await PackedOnAsync(_twenty, 5m, daysAgo: 20);
        await PackedOnAsync(_twenty, 3m, daysAgo: 21);
        await PackedOnAsync(_twenty, 4m, daysAgo: 6);

        var row = (await _ages.GetAsync(IndiaTime.Today(), default)).Single(r => r.ProductId == _twenty.Id);

        Assert.Equal(5m, row.ExpiringSoon);
        Assert.Equal(3m, row.Expired);
        Assert.Equal(4m, row.RepackWindow);
        Assert.Equal(12m, row.Total);
        Assert.Equal(5m, row.ExpiringWithinDays);
    }

    [Fact]
    public async Task Writing_off_takes_exactly_the_expired_packets_and_leaves_the_rest_their_age()
    {
        await PackedOnAsync(_twenty, 3m, daysAgo: 22);
        await PackedOnAsync(_twenty, 4m, daysAgo: 2);

        await _ages.WriteOffExpiredAsync(_twenty.Id, KnownStockLocations.MainWarehouseId, default);

        var damage = await _database.Db.StockMovements.SingleAsync(m => m.MovementType == StockMovementType.Damage);
        Assert.Equal(-3m, damage.Quantity);
        Assert.Contains("Expired", damage.Notes);

        var row = (await _ages.GetAsync(IndiaTime.Today(), default)).Single(r => r.ProductId == _twenty.Id);
        Assert.Equal(0m, row.Expired);
        Assert.Equal(4m, row.Fresh);

        // Once it is gone there is nothing left to write off.
        await Assert.ThrowsAsync<DomainException>(() =>
            _ages.WriteOffExpiredAsync(_twenty.Id, KnownStockLocations.MainWarehouseId, default));
    }

    [Fact]
    public async Task Every_row_of_the_stock_report_adds_up_to_its_closing_stock()
    {
        await PackedOnAsync(_twenty, 10m, daysAgo: 3);
        await PackedOnAsync(_twenty, 5m, daysAgo: 0);
        await _repacking.CreateAsync(new RepackRequest(_twenty.Id, 2m, _ten.Id, null, null), default);

        var today = IndiaTime.Today();
        var report = await new StockMovementReport(_database.Db).BuildAsync(today, today, default);
        var warehouse = Assert.Single(report.Sections);

        foreach (var row in warehouse.Rows)
        {
            var sum = row.Where(c => c.Key is not ("product" or "closing") && c.Value is decimal).Sum(c => (decimal)c.Value!);
            Assert.Equal(row["closing"], sum);
        }

        var twenty = warehouse.Rows.Single(r => ((string)r["product"]!).StartsWith(_twenty.Name));
        Assert.Equal(10m, twenty["opening"]);
        Assert.Equal(5m, twenty["made"]);
        Assert.Equal(-2m, twenty["repackedOut"]);
        Assert.Equal(await OnHandAsync(_twenty), twenty["closing"]);
    }
}
