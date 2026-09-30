using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.RateRequests;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// The rule in the services, whatever door is used (CLAUDE.md §4 "Rate-change approval", 2026-09-30):
/// a salesperson sets rates only for a customer being created; afterwards a rate changes only when an
/// admin approves a request. MobileContractTests proves the same through real HTTP.
/// </summary>
public class RateRequestServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;
    private CustomerPriceService _prices = null!;
    private RateRequestService _requests = null!;
    private Customer _shop = null!;
    private Guid _productId;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase();
        _prices = new CustomerPriceService(_database.Db, _database.CurrentUser);
        _requests = new RateRequestService(_database.Db, _prices, _database.CurrentUser);

        var (_, packet) = await _database.SeedProductsAsync();
        _productId = packet.Id;
        _shop = await _database.SeedCustomerAsync();
        await _prices.SetAsync(_shop.Id, _productId, 35m, default);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private void ActAsSalesperson(Guid? id = null)
    {
        _database.CurrentUser.UserId = id ?? Guid.NewGuid();
        _database.CurrentUser.Roles.Add(Roles.Salesperson);
    }

    private void ActAsOffice()
    {
        _database.CurrentUser.UserId = Guid.NewGuid();
        _database.CurrentUser.Roles.Clear();
    }

    private async Task<decimal> RateAsync() =>
        await _database.Db.CustomerPrices.AsNoTracking()
            .Where(cp => cp.CustomerId == _shop.Id && cp.ProductId == _productId)
            .Select(cp => cp.UnitPrice)
            .SingleAsync();

    [Fact]
    public async Task A_salesperson_cannot_change_or_remove_a_rate_through_the_office_path()
    {
        ActAsSalesperson();

        var set = await Assert.ThrowsAsync<DomainException>(() => _prices.SetAsync(_shop.Id, _productId, 50m, default));
        var removed = await Assert.ThrowsAsync<DomainException>(() => _prices.RemoveAsync(_shop.Id, _productId, default));

        Assert.Contains("rate-change request", set.Message);
        Assert.Contains("rate-change request", removed.Message);
        Assert.Equal(35m, await RateAsync());
    }

    [Fact]
    public async Task First_rates_are_only_for_a_customer_with_no_rate_history()
    {
        ActAsSalesperson();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _prices.SetInitialRatesAsync(_shop.Id, [(_productId, 20m)], default));

        Assert.Contains("already set", exception.Message);
        Assert.Equal(35m, await RateAsync());
    }

    [Fact]
    public async Task A_new_customers_first_rates_are_accepted_from_a_salesperson()
    {
        ActAsSalesperson();
        var newShop = await _database.SeedCustomerAsync("New Shop");

        await _prices.SetInitialRatesAsync(newShop.Id, [(_productId, 36m)], default);
        await _database.Db.SaveChangesAsync();

        Assert.Equal(36m, (await _prices.GetAgreedPricesAsync(newShop.Id, [_productId], default))[_productId]);
    }

    [Fact]
    public async Task Approval_changes_the_rate_and_records_the_request_rejection_leaves_it()
    {
        var salesman = Guid.NewGuid();
        ActAsSalesperson(salesman);
        var approveMe = await _requests.CreateAsync(Guid.NewGuid(), _shop.Id, _productId, 38m, "festival", DateTime.UtcNow, default);

        Assert.Equal((35m, RateRequestStatus.Pending), (approveMe.PriceWhenRequested!.Value, approveMe.Status));
        Assert.Equal(35m, await RateAsync());

        ActAsOffice();
        await _requests.ApproveAsync(approveMe.Id, null, default);

        Assert.Equal(38m, await RateAsync());
        var change = await _database.Db.CustomerPriceChanges.OrderByDescending(c => c.CreatedAt).FirstAsync();
        Assert.Equal((approveMe.Id, 38m), (change.RateRequestId!.Value, change.NewPrice!.Value));

        ActAsSalesperson(salesman);
        var rejectMe = await _requests.CreateAsync(Guid.NewGuid(), _shop.Id, _productId, 30m, null, DateTime.UtcNow, default);
        ActAsOffice();
        var rejected = await _requests.RejectAsync(rejectMe.Id, "Too low", default);

        Assert.Equal((RateRequestStatus.Rejected, "Too low"), (rejected.Status, rejected.DecisionNote));
        Assert.Equal(38m, await RateAsync());
    }

    [Fact]
    public async Task A_salesperson_cannot_approve_even_by_calling_the_service()
    {
        ActAsSalesperson();
        var request = await _requests.CreateAsync(Guid.NewGuid(), _shop.Id, _productId, 50m, null, DateTime.UtcNow, default);

        await Assert.ThrowsAsync<DomainException>(() => _requests.ApproveAsync(request.Id, null, default));

        Assert.Equal(35m, await RateAsync());
    }

    [Fact]
    public async Task Sent_twice_is_one_request_and_only_its_sender_can_withdraw_it()
    {
        var salesman = Guid.NewGuid();
        ActAsSalesperson(salesman);
        var id = Guid.NewGuid();

        await _requests.CreateAsync(id, _shop.Id, _productId, 38m, null, DateTime.UtcNow, default);
        await _requests.CreateAsync(id, _shop.Id, _productId, 38m, null, DateTime.UtcNow, default);
        Assert.Equal(1, await _database.Db.CustomerRateRequests.CountAsync());

        ActAsSalesperson(Guid.NewGuid());
        await Assert.ThrowsAsync<DomainException>(() => _requests.CancelAsync(id, default));

        ActAsSalesperson(salesman);
        var withdrawn = await _requests.CancelAsync(id, default);
        Assert.Equal(RateRequestStatus.Cancelled, withdrawn.Status);
    }

    [Fact]
    public async Task A_request_cannot_be_rewritten_only_decided()
    {
        ActAsSalesperson();
        var request = await _requests.CreateAsync(Guid.NewGuid(), _shop.Id, _productId, 38m, null, DateTime.UtcNow, default);

        await using var db = _database.NewContext();
        var loaded = await db.CustomerRateRequests.SingleAsync(r => r.Id == request.Id);
        loaded.RequestedPrice = 99m;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
