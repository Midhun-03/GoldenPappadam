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
        "/api/sales/invoices",
        "/api/sales/payments"
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

    [Fact]
    public async Task A_salesperson_can_neither_see_nor_set_a_price()
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
