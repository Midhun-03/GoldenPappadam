using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// A throwaway LocalDB database per test class. Tests run against real SQL Server rather than
/// an in-memory provider, so check constraints, transactions and decimal precision behave
/// the way they will in production.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    public AppDbContext Db { get; }

    public TestDatabase()
    {
        var name = $"GoldenPappadam_Tests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        Db = new AppDbContext(options, new TestCurrentUser());
        Db.Database.EnsureCreated();
    }

    /// <summary>Category, unit, a loose product and a packet packed from it.</summary>
    public async Task<(Product Loose, Product Packet)> SeedProductsAsync(decimal sourceQuantityPerPack = 0.250m)
    {
        var category = new ProductCategory { Name = "Pappadam" };
        Db.Add(category);
        await Db.SaveChangesAsync();

        // KG, PCS, PKT and BOX are seeded by the model itself.
        var kg = await Db.UnitOfMeasures.FirstAsync(u => u.Code == "KG");
        var packet = await Db.UnitOfMeasures.FirstAsync(u => u.Code == "PKT");

        var loose = new Product
        {
            ProductCode = "LOOSE-1",
            Name = "Loose pappadam",
            CategoryId = category.Id,
            Kind = ProductKind.Loose,
            UnitOfMeasureId = kg.Id
        };
        Db.Add(loose);
        await Db.SaveChangesAsync();

        var packed = new Product
        {
            ProductCode = "PKT-250",
            Name = "250 g packet",
            CategoryId = category.Id,
            Kind = ProductKind.Packed,
            UnitOfMeasureId = packet.Id,
            SourceProductId = loose.Id,
            SourceQuantityPerPack = sourceQuantityPerPack
        };
        Db.Add(packed);
        await Db.SaveChangesAsync();

        return (loose, packed);
    }

    public async Task AddStockAsync(Guid productId, decimal quantity)
    {
        Db.Add(new StockMovement
        {
            ProductId = productId,
            MovementType = StockMovementType.Opening,
            Quantity = quantity,
            OccurredAt = DateTime.UtcNow
        });
        await Db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Db.Database.EnsureDeletedAsync();
        await Db.DisposeAsync();
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
    }
}
