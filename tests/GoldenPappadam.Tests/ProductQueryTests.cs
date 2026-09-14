using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Runs the list queries against SQL Server so an expression EF cannot translate fails here
/// rather than as a 500 in the browser.
/// </summary>
public class ProductQueryTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task The_product_list_query_translates_to_sql()
    {
        var (loose, packet) = await _database.SeedProductsAsync();

        var products = await ProductQueries.Project(
                _database.Db.Products
                    .Where(p => p.IsActive)
                    .Where(p => p.Name.Contains("pack") || p.ProductCode.Contains("PKT"))
                    .Where(p => p.Kind == ProductKind.Packed)
                    .OrderBy(p => p.Name))
            .ToListAsync();

        var dto = Assert.Single(products);
        Assert.Equal(packet.Id, dto.Id);
        Assert.Equal("250 g packet", dto.Name);
        Assert.Equal("PKT", dto.UnitCode);
        Assert.Equal("Pappadam", dto.CategoryName);
        Assert.Equal(loose.Id, dto.SourceProductId);
        Assert.Equal("Loose pappadam", dto.SourceProductName);
        Assert.Equal(0.250m, dto.SourceQuantityPerPack);
    }

    [Fact]
    public async Task A_loose_product_projects_without_source_details()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        var dto = await ProductQueries.Project(_database.Db.Products.Where(p => p.Id == loose.Id))
            .SingleAsync();

        Assert.Null(dto.SourceProductId);
        Assert.Null(dto.SourceProductName);
        Assert.Null(dto.SourceQuantityPerPack);
    }
}
