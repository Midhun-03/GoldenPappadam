using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Invoice numbers come from the database under a row lock, so they can never collide however
/// many devices finalize at once. These tests race real connections against real SQL Server,
/// because that is the only place the guarantee lives.
/// </summary>
public class InvoiceNumberingTests : IAsyncLifetime
{
    private static readonly DateOnly September = new(2026, 9, 23);

    private TestDatabase _database = null!;
    private Guid _customerId;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();

        var (_, packet) = await _database.SeedProductsAsync();
        packet.SellingPrice = 40m;
        await _database.Db.SaveChangesAsync();
        await _database.AddStockAsync(packet.Id, 10_000m);

        _productId = packet.Id;
        _customerId = (await _database.SeedCustomerAsync()).Id;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Many_devices_finalizing_at_the_same_moment_get_distinct_consecutive_numbers()
    {
        const int devices = 25;

        // Each simulated device has its own connection and its own services, as separate requests
        // would, and they are released together so their transactions genuinely overlap.
        using var start = new ManualResetEventSlim(false);

        var finalizations = Enumerable.Range(0, devices).Select(_ => Task.Run(async () =>
        {
            await using var db = _database.NewContext();
            var invoices = NewService(db);

            start.Wait();

            return await invoices.CreateAsync(Request(September), default);
        })).ToList();

        start.Set();
        var results = await Task.WhenAll(finalizations);

        var numbers = results.Select(r => r.Invoice.InvoiceNumber).ToList();
        Assert.Equal(devices, numbers.Distinct().Count());

        // Nothing skipped and nothing repeated: exactly 1..25, whichever order they finished in.
        var saved = await _database.Db.Invoices.AsNoTracking().OrderBy(i => i.SequenceNumber).ToListAsync();
        Assert.Equal(Enumerable.Range(1, devices), saved.Select(i => i.SequenceNumber));
        Assert.All(saved, i => Assert.Equal(InvoiceNumbering.Format("GP", "2026-27", i.SequenceNumber), i.InvoiceNumber));

        var counter = await _database.Db.InvoiceNumberSequences.AsNoTracking().SingleAsync();
        Assert.Equal(devices, counter.LastNumber);

        // Every bill is whole: its lines and its stock movement arrived with it.
        Assert.Equal(devices, await _database.Db.InvoiceLines.CountAsync());
        Assert.Equal(devices, await _database.Db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.Sale));
    }

    [Fact]
    public async Task The_first_invoices_of_a_new_financial_year_race_to_create_its_counter_and_both_succeed()
    {
        // No counter exists yet for 2027-28, so both requests try to create it.
        var firstOfApril = new DateOnly(2027, 4, 1);
        using var start = new ManualResetEventSlim(false);

        var both = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await using var db = _database.NewContext();
            start.Wait();
            return await NewService(db).CreateAsync(Request(firstOfApril), default);
        })).ToList();

        start.Set();
        var results = await Task.WhenAll(both);

        Assert.Equal(
            ["GP/27-28/000001", "GP/27-28/000002"],
            results.Select(r => r.Invoice.InvoiceNumber).Order());
        Assert.Single(await _database.Db.InvoiceNumberSequences.Where(s => s.FinancialYear == "2027-28").ToListAsync());
    }

    [Fact]
    public async Task A_new_financial_year_starts_again_at_one_without_anyone_resetting_anything()
    {
        var march = await CreateAsync(new DateOnly(2027, 3, 31));
        var april = await CreateAsync(new DateOnly(2027, 4, 1));
        var nextMarch = await CreateAsync(new DateOnly(2027, 3, 30));

        Assert.Equal("GP/26-27/000001", march);
        Assert.Equal("GP/27-28/000001", april);
        Assert.Equal("GP/26-27/000002", nextMarch);
    }

    [Fact]
    public async Task A_number_reserved_by_a_transaction_that_rolls_back_is_not_lost()
    {
        await using (var transaction = await _database.Db.Database.BeginTransactionAsync())
        {
            var reserved = await InvoiceNumbering.ReserveAsync(_database.Db, "GP", September, default);
            Assert.Equal(1, reserved.SequenceNumber);

            // The bill failed to save: the reservation goes back with everything else.
            await transaction.RollbackAsync();
        }

        Assert.Equal("GP/26-27/000001", await CreateAsync(September));
    }

    [Fact]
    public async Task A_number_cannot_be_reserved_outside_the_transaction_that_saves_the_invoice()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvoiceNumbering.ReserveAsync(_database.Db, "GP", September, default));
    }

    [Fact]
    public async Task A_counter_that_has_fallen_behind_is_repaired_rather_than_issuing_a_duplicate()
    {
        await CreateAsync(September);
        await CreateAsync(September);

        // As if a backup of the counter were restored over newer invoices.
        await _database.Db.Database.ExecuteSqlAsync($"UPDATE sales.InvoiceNumberSequences SET LastNumber = 0");

        var next = await CreateAsync(September);

        Assert.Equal("GP/26-27/000003", next);
        Assert.Equal(3, await _database.Db.Invoices.CountAsync());

        // The retried bill is whole: its line and its stock movement came with it, once each.
        var repaired = await _database.Db.Invoices.AsNoTracking().Include(i => i.Lines).SingleAsync(i => i.InvoiceNumber == next);
        Assert.Single(repaired.Lines);
        Assert.Single(await _database.Db.StockMovements.Where(m => m.ReferenceId == repaired.Id).ToListAsync());
        Assert.Single(await _database.Db.Customers.ToListAsync());
    }

    [Fact]
    public async Task A_different_series_counts_on_its_own()
    {
        await CreateAsync(September);
        await _database.ConfigureGstAsync(gstin: null, seriesCode: "GPA");

        Assert.Equal("GPA/26-27/000001", await CreateAsync(September));
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_invoice_with_the_same_number()
    {
        await CreateAsync(September);
        var first = await _database.Db.Invoices.AsNoTracking().Include(i => i.Lines).SingleAsync();

        // Bypass the service entirely: only the unique index stands in the way.
        await using var db = _database.NewContext();
        db.Invoices.Add(new Invoice
        {
            InvoiceNumber = first.InvoiceNumber,
            SeriesCode = first.SeriesCode,
            FinancialYear = first.FinancialYear,
            SequenceNumber = first.SequenceNumber,
            CustomerId = first.CustomerId,
            InvoiceDate = first.InvoiceDate,
            SupplierName = "x",
            SupplierStateCode = "32",
            CustomerName = "x"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task<string> CreateAsync(DateOnly date) =>
        (await NewService(_database.Db).CreateAsync(Request(date), default)).Invoice.InvoiceNumber;

    private CreateInvoiceRequest Request(DateOnly date) =>
        new(_customerId, date, 0m, null, [new InvoiceLineRequest(_productId, 2m, null)]);

    private static InvoiceService NewService(AppDbContext db) =>
        new(db, new StockService(db), new CustomerPriceService(db));
}
