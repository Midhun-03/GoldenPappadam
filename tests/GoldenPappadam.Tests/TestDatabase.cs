using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// A throwaway database per test class on the local SQL Express instance. Tests run against
/// real SQL Server rather than an in-memory provider, so check constraints, transactions and
/// decimal precision behave the way they will in production.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    public AppDbContext Db { get; }

    /// <summary>
    /// Who the tests are signed in as. Null by default, which is how everything behaved before
    /// there were users; a test that needs a real signed-in user sets it.
    /// </summary>
    public TestCurrentUser CurrentUser { get; } = new();

    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        var name = $"GoldenPappadam_Tests_{Guid.NewGuid():N}";
        var options = _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server=.\\SQLEXPRESS;Database={name};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        Db = new AppDbContext(options, CurrentUser);
        Db.Database.EnsureCreated();
    }

    /// <summary>
    /// Another connection to the same database, the way a second device's request would arrive.
    /// Concurrency tests give each simulated device its own.
    /// </summary>
    public AppDbContext NewContext() => new(_options, CurrentUser);

    /// <summary>Switches GST on (or off, with a null GSTIN) the way the settings screen does.</summary>
    public async Task ConfigureGstAsync(
        string? gstin = "32AAAAA1234A1Z5",
        bool pricesIncludeTax = false,
        bool roundToNearestRupee = false,
        string seriesCode = "GP")
    {
        var settings = await Db.InvoiceSettings.SingleAsync(s => s.Id == InvoiceSettings.SingletonId);
        settings.Gstin = gstin;
        settings.PricesIncludeTax = pricesIncludeTax;
        settings.RoundToNearestRupee = roundToNearestRupee;
        settings.SeriesCode = seriesCode;
        await Db.SaveChangesAsync();
    }

    public async Task SetTaxAsync(Guid productId, TaxTreatment treatment, decimal? gstRate = null, string? hsn = "19059040")
    {
        var product = await Db.Products.SingleAsync(p => p.Id == productId);
        product.TaxTreatment = treatment;
        product.GstRate = gstRate;
        product.HsnCode = hsn;
        await Db.SaveChangesAsync();
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
            UnitOfMeasureId = kg.Id,
            // The standard pappadam's average (owner, 2026-09-30).
            PiecesPerKg = 200m
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

    public async Task<Customer> SeedCustomerAsync(string name = "Test Shop", decimal openingBalance = 0m)
    {
        var customer = new Customer { Name = name, OpeningBalance = openingBalance };
        Db.Add(customer);
        await Db.SaveChangesAsync();

        return customer;
    }

    /// <summary>Sets the parent customer's flag too, so it stays consistent the way the service enforces it.</summary>
    public async Task<CustomerBranch> SeedBranchAsync(Guid customerId, string name = "Kundara")
    {
        var branch = new CustomerBranch { CustomerId = customerId, Name = name };
        Db.Add(branch);

        var customer = await Db.Customers.FirstAsync(c => c.Id == customerId);
        customer.HasMultipleBranches = true;

        await Db.SaveChangesAsync();

        return branch;
    }

    public async Task AddStockAsync(Guid productId, decimal quantity, Guid? locationId = null)
    {
        Db.Add(new StockMovement
        {
            ProductId = productId,
            LocationId = locationId ?? KnownStockLocations.MainWarehouseId,
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

    public sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
    }
}
