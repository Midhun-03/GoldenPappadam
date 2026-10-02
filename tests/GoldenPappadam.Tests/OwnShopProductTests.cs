using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The own shop's pieces products: how one is set up, and that they stay at the shop - never on an
/// invoice, a van, the warehouse's stock list or a packet.
/// </summary>
public class OwnShopProductTests : IAsyncLifetime
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

    private SaveProductRequest PiecesRequest(
        decimal? rate = 1.60m,
        decimal? minimum = 1.30m,
        Guid? unitId = null,
        Guid? sourceId = null,
        string code = "SHOP-STD") =>
        new(code, "Pappadam by the piece", _loose.CategoryId, ProductKind.Pieces, unitId ?? KnownUnits.PieceId,
            sourceId ?? _loose.Id, null, rate, null, MinimumSellingPrice: minimum);

    [Fact]
    public async Task A_pieces_product_is_set_up_with_its_rate_band()
    {
        var product = await _products.CreateAsync(PiecesRequest(), default);

        Assert.Equal((1.60m, 1.30m, _loose.Id), (product.SellingPrice, product.MinimumSellingPrice, product.SourceProductId));
    }

    [Fact]
    public async Task The_minimum_cannot_be_above_the_rate()
    {
        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(PiecesRequest(minimum: 1.70m), default));
    }

    [Fact]
    public async Task A_pieces_product_needs_its_rate_the_PCS_unit_and_a_loose_kg_variety()
    {
        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(PiecesRequest(rate: null, minimum: null), default));
        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(PiecesRequest(unitId: KnownUnits.KilogramId), default));

        var packet = await _database.Db.Products.FirstAsync(p => p.Kind == ProductKind.Packed);
        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(PiecesRequest(sourceId: packet.Id), default));
    }

    [Fact]
    public async Task A_minimum_rate_belongs_to_pieces_products_only()
    {
        var packet = await _database.Db.Products.AsNoTracking().FirstAsync(p => p.Kind == ProductKind.Packed);
        var request = new SaveProductRequest(
            packet.ProductCode, packet.Name, packet.CategoryId, packet.Kind, packet.UnitOfMeasureId, packet.SourceProductId,
            packet.SourceQuantityPerPack, 32m, null, MinimumSellingPrice: 30m);

        await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(packet.Id, request, default));
    }

    [Fact]
    public async Task One_variety_has_one_pieces_product()
    {
        await _products.CreateAsync(PiecesRequest(), default);

        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(PiecesRequest(code: "SHOP-2"), default));
    }

    [Fact]
    public async Task The_shops_pieces_are_never_packed_into_a_bundle_product()
    {
        var pieces = await _products.CreateAsync(PiecesRequest(), default);
        var box = (await _database.Db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT")).Id;
        var bundle = new SaveProductRequest("BUNDLE-50", "50 piece bundle", _loose.CategoryId, ProductKind.Packed, box,
            pieces.Id, 50m, 80m, null);

        await Assert.ThrowsAsync<DomainException>(() => _products.CreateAsync(bundle, default));
    }

    [Fact]
    public async Task The_variety_keeps_its_pieces_per_kg_while_the_shop_sells_it()
    {
        await _products.CreateAsync(PiecesRequest(), default);
        var kg = new SaveProductRequest(_loose.ProductCode, _loose.Name, _loose.CategoryId, ProductKind.Loose,
            KnownUnits.KilogramId, null, null, null, null, PiecesPerKg: null);

        await Assert.ThrowsAsync<DomainException>(() => _products.UpdateAsync(_loose.Id, kg, default));
    }

    [Fact]
    public async Task Pieces_products_are_never_billed_on_an_invoice()
    {
        var pieces = await _database.SeedPiecesAsync(_loose);
        var customer = await _database.SeedCustomerAsync();
        var invoices = new InvoiceService(_database.Db, _stock, new CustomerPriceService(_database.Db, _database.CurrentUser));

        var request = new CreateInvoiceRequest(customer.Id, null, 0m, null, [new InvoiceLineRequest(pieces.Id, 10m, 1.60m)]);

        var exception = await Assert.ThrowsAsync<DomainException>(() => invoices.CreateAsync(request, default));
        Assert.Contains("own shop", exception.Message);
    }

    [Fact]
    public async Task Pieces_products_are_never_loaded_on_a_van()
    {
        var pieces = await _database.SeedPiecesAsync(_loose);
        var vanLoads = new VanLoadService(_database.Db, _stock);

        var request = new CreateVanLoadRequest(KnownStockLocations.FirstVanId, VanLoadDirection.Loading, null, null,
            [new VanLoadLineRequest(pieces.Id, 100m)]);

        await Assert.ThrowsAsync<DomainException>(() => vanLoads.CreateAsync(request, default));
    }

    [Fact]
    public async Task The_shop_keeps_only_pieces_and_pieces_are_kept_only_at_the_shop()
    {
        var pieces = await _database.SeedPiecesAsync(_loose);

        // Loose kg reach the shop through a transfer, never by typing them in.
        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(_loose.Id, StockMovementType.Opening, 10m, null, null, KnownStockLocations.OwnShopId), default));
        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(pieces.Id, StockMovementType.Opening, 10m, null, null, KnownStockLocations.MainWarehouseId), default));

        // The shop's opening count on the first day is fine, in whole pieces.
        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(pieces.Id, StockMovementType.Opening, 10.5m, null, null, KnownStockLocations.OwnShopId), default));
        var opening = await _stock.AddEntryAsync(
            new CreateStockEntryRequest(pieces.Id, StockMovementType.Opening, 2000m, null, null, KnownStockLocations.OwnShopId), default);
        Assert.Equal(2000m, opening.QuantityOnHand);
    }

    [Fact]
    public async Task Damage_at_the_shop_never_takes_it_below_zero()
    {
        var pieces = await _database.SeedPiecesAsync(_loose);
        await _database.AddStockAsync(pieces.Id, 100m, KnownStockLocations.OwnShopId);

        await Assert.ThrowsAsync<DomainException>(() => _stock.AddEntryAsync(
            new CreateStockEntryRequest(pieces.Id, StockMovementType.Damage, 101m, null, "Broken", KnownStockLocations.OwnShopId), default));

        var damaged = await _stock.AddEntryAsync(
            new CreateStockEntryRequest(pieces.Id, StockMovementType.Damage, 100m, null, "Broken", KnownStockLocations.OwnShopId), default);
        Assert.Equal(0m, damaged.QuantityOnHand);
    }

    [Fact]
    public async Task Each_place_lists_only_what_it_can_hold()
    {
        var pieces = await _database.SeedPiecesAsync(_loose);

        var warehouse = await _stock.GetOnHandAsync(null, false, false, KnownStockLocations.MainWarehouseId, default);
        var shop = await _stock.GetOnHandAsync(null, false, false, KnownStockLocations.OwnShopId, default);
        var everywhere = await _stock.GetOnHandAsync(null, false, false, null, default);

        Assert.DoesNotContain(warehouse, r => r.ProductId == pieces.Id);
        Assert.Equal([pieces.Id], shop.Select(r => r.ProductId));
        Assert.Contains(everywhere, r => r.ProductId == pieces.Id);
    }
}
