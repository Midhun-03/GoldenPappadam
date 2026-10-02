using System.Net;
using System.Net.Http.Json;
using GoldenPappadam.Infrastructure.Identity;

namespace GoldenPappadam.Tests;

/// <summary>
/// What a salesperson token may and may not reach. The phone hiding a button is not security;
/// these tests are the thing that actually holds the line.
/// </summary>
public class AuthorizationTests : IAsyncLifetime
{
    /// <summary>
    /// One endpoint from every controller written in phase 1. None of them was edited for phase 3:
    /// they are admin-only because the fallback policy says so, and that is exactly what this checks.
    /// </summary>
    public static TheoryData<string> AdminEndpoints =>
    [
        "/api/admin/users",
        "/api/dashboard/summary",
        "/api/inventory/categories",
        "/api/inventory/units",
        "/api/inventory/products",
        "/api/inventory/stock",
        "/api/sales/customers",
        "/api/sales/customer-price-changes",
        "/api/sales/invoices",
        "/api/sales/payments",
        "/api/fieldsales/day",
        "/api/fieldsales/van-loads",
        "/api/fieldsales/stock-requests",
        "/api/fieldsales/stock-requests/packing-needs",
        "/api/fieldsales/devices",
        "/api/staff/employees",
        "/api/staff/attendance",
        "/api/staff/attendance-statuses",
        "/api/staff/wages/week",
        "/api/staff/wage-payments",
        "/api/accounting/expenses",
        "/api/accounting/expenses/summary",
        "/api/accounting/expense-categories",
        "/api/own-shop/stock",
        "/api/own-shop/transfers",
        "/api/own-shop/sales"
    ];

    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory();
        await _api.InitializeAsync();

        await _api.CreateUserAsync("office@test.local", Roles.Admin);
        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task A_salesperson_is_refused_every_admin_endpoint(string endpoint)
    {
        var client = await _api.SignInAsync("van@test.local");

        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task An_admin_still_reaches_every_admin_endpoint(string endpoint)
    {
        var client = await _api.SignInAsync("office@test.local");

        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Signing_in_is_required(string endpoint)
    {
        var response = await _api.CreateClient().GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_salesperson_can_ask_who_they_are()
    {
        var client = await _api.SignInAsync("van@test.local");

        var me = await client.GetFromJsonAsync<CurrentUser>("/api/auth/me");

        Assert.NotNull(me);
        Assert.Equal("van@test.local", me.Email);
        Assert.Equal(Roles.Salesperson, me.Role);
    }

    [Fact]
    public async Task An_admin_creating_a_salesperson_gets_a_salesperson()
    {
        var client = await _api.SignInAsync("office@test.local");

        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "second@test.local",
            fullName = "Second Salesperson",
            password = ApiFactory.Password,
            role = Roles.Salesperson
        });

        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreatedUser>();

        Assert.Equal(Roles.Salesperson, created!.Role);

        // And the new account really is limited, not merely labelled.
        var theirClient = await _api.SignInAsync("second@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await theirClient.GetAsync("/api/admin/users")).StatusCode);
    }

    /// <summary>
    /// Salesmen do set rates (2026-09-23), but only through the sync batch, which records who did it
    /// and cannot remove a rate. The office's price endpoints stay the office's.
    /// </summary>
    [Fact]
    public async Task A_salesperson_cannot_use_the_office_price_endpoints()
    {
        var client = await _api.SignInAsync("van@test.local");
        var customerId = Guid.NewGuid();

        // Authorization runs before the controller, so made-up ids still prove the point: the
        // request never gets far enough for the ids to matter.
        var read = await client.GetAsync($"/api/sales/customers/{customerId}/prices");
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        var write = await client.PutAsJsonAsync(
            $"/api/sales/customers/{customerId}/prices/{Guid.NewGuid()}", new { unitPrice = 1m });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        var remove = await client.DeleteAsync($"/api/sales/customers/{customerId}/prices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, remove.StatusCode);
    }

    [Fact]
    public async Task A_salesperson_can_reach_their_own_endpoints()
    {
        var client = await _api.SignInAsync("van@test.local");

        var registered = await client.PostAsJsonAsync(
            "/api/mobile/devices/register", new { name = "Nokia", platform = "Android" });
        registered.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/mobile/sync/snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/mobile/day")).StatusCode);

        // The van screen is theirs too. Without a van assigned it answers 400, not 403 - the point
        // is that authorization lets them through and only the business rule stops them.
        var van = await client.GetAsync("/api/mobile/van-stock");
        Assert.NotEqual(HttpStatusCode.Forbidden, van.StatusCode);
    }

    [Fact]
    public async Task A_salesperson_cannot_put_their_own_phone_on_a_van()
    {
        var client = await _api.SignInAsync("van@test.local");

        // Which van a phone rides in is what decides where its stock movements land, so it has to
        // be the office's decision. Otherwise the narrow permission would not be narrow at all.
        var assign = await client.PostAsync(
            $"/api/fieldsales/devices/{Guid.NewGuid()}/van?locationId={Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.Forbidden, assign.StatusCode);
    }

    [Fact]
    public async Task A_salesperson_cannot_decide_what_gets_packed()
    {
        var client = await _api.SignInAsync("van@test.local");

        // They may ask for stock; they may not answer the request or read the packing board.
        var board = await client.GetAsync("/api/fieldsales/stock-requests/packing-needs");
        Assert.Equal(HttpStatusCode.Forbidden, board.StatusCode);

        var decide = await client.PostAsync(
            $"/api/fieldsales/stock-requests/{Guid.NewGuid()}/status?status=Fulfilled", null);
        Assert.Equal(HttpStatusCode.Forbidden, decide.StatusCode);
    }

    [Fact]
    public async Task A_salesperson_can_only_move_stock_onto_their_own_van()
    {
        var client = await _api.SignInAsync("van@test.local");

        // No hand-written movements at all: no production, no damage, no adjustment.
        var entry = await client.PostAsJsonAsync("/api/inventory/stock/entries", new
        {
            productId = Guid.NewGuid(), movementType = "Production", quantity = 10
        });
        Assert.Equal(HttpStatusCode.Forbidden, entry.StatusCode);

        // And not the office's van-load endpoint either, which can name any van and any direction.
        // Their one way to move stock is the submission batch, where the server decides the van
        // from the device and the direction is always warehouse to van.
        var load = await client.PostAsJsonAsync("/api/fieldsales/van-loads", new
        {
            vanLocationId = Guid.NewGuid(), direction = "Loading", lines = new[] { new { productId = Guid.NewGuid(), quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, load.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_be_reached_from_the_mobile_surface_by_a_salesperson()
    {
        var client = await _api.SignInAsync("van@test.local");

        // The one that would matter most: creating a bill by hand, at any price they like.
        var bill = await client.PostAsJsonAsync("/api/sales/invoices", new
        {
            customerId = Guid.NewGuid(),
            lines = new[] { new { productId = Guid.NewGuid(), quantity = 1, unitPrice = 1 } }
        });

        Assert.Equal(HttpStatusCode.Forbidden, bill.StatusCode);
    }

    [Fact]
    public async Task A_role_that_does_not_exist_is_refused()
    {
        var client = await _api.SignInAsync("office@test.local");

        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "third@test.local",
            fullName = "Third",
            password = ApiFactory.Password,
            role = "Owner"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record CurrentUser(Guid Id, string Email, string FullName, string Role);

    private record CreatedUser(Guid Id, string Email, string FullName, string Role, bool IsActive);
}
