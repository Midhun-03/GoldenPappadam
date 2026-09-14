using GoldenPappadam.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The ledger rule from CLAUDE.md: transactions are never edited or deleted, only corrected
/// with new records. Enforced in AppDbContext so no feature can bypass it by mistake.
/// </summary>
public class LedgerImmutabilityTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_stock_movement_cannot_be_edited()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 10m);

        var movement = await _database.Db.StockMovements.FirstAsync();
        movement.Quantity = 999m;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _database.Db.SaveChangesAsync());

        Assert.Contains("cannot be edited", exception.Message);
    }

    [Fact]
    public async Task A_stock_movement_cannot_be_deleted()
    {
        var (loose, _) = await _database.SeedProductsAsync();
        await _database.AddStockAsync(loose.Id, 10m);

        var movement = await _database.Db.StockMovements.FirstAsync();
        _database.Db.StockMovements.Remove(movement);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _database.Db.SaveChangesAsync());

        Assert.Contains("cannot be deleted", exception.Message);
    }

    [Fact]
    public async Task Master_data_is_editable_and_stamps_the_audit_fields()
    {
        var (loose, _) = await _database.SeedProductsAsync();

        Assert.NotEqual(default, loose.CreatedAt);
        Assert.Null(loose.UpdatedAt);

        loose.Name = "Loose pappadam (large)";
        await _database.Db.SaveChangesAsync();

        Assert.NotNull(loose.UpdatedAt);
    }
}
