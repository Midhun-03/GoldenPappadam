using System.Net;
using System.Net.Http.Json;
using GoldenPappadam.Infrastructure.Identity;

namespace GoldenPappadam.Tests;

/// <summary>
/// The token flow the phone depends on. A salesperson may be out of signal for days, so the
/// refresh path has to work - and has to stop working the moment the account is deactivated.
/// </summary>
public class MobileAuthTests : IAsyncLifetime
{
    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new ApiFactory();
        await _api.InitializeAsync();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task Logging_in_returns_an_access_token_and_a_refresh_token()
    {
        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);

        var tokens = await _api.RequestTokensAsync(_api.CreateClient(), "van@test.local");

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.ExpiresIn > 0);
    }

    [Fact]
    public async Task A_refresh_token_buys_a_working_access_token()
    {
        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
        var client = _api.CreateClient();
        var tokens = await _api.RequestTokensAsync(client, "van@test.local");

        var response = await client.PostAsJsonAsync(
            "/api/auth/mobile/refresh", new { refreshToken = tokens.RefreshToken });

        response.EnsureSuccessStatusCode();
        var refreshed = await response.Content.ReadFromJsonAsync<ApiFactory.TokenPair>();

        client.DefaultRequestHeaders.Authorization = new("Bearer", refreshed!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Deactivating_an_account_stops_its_phone_at_the_next_refresh()
    {
        var user = await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
        var client = _api.CreateClient();
        var tokens = await _api.RequestTokensAsync(client, "van@test.local");

        await _api.DeactivateAsync(user.Id);

        var response = await client.PostAsJsonAsync(
            "/api/auth/mobile/refresh", new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_cannot_log_in()
    {
        var user = await _api.CreateUserAsync("van@test.local", Roles.Salesperson);
        await _api.DeactivateAsync(user.Id);

        var response = await _api.CreateClient().PostAsJsonAsync(
            "/api/auth/mobile/login", new { email = "van@test.local", password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_says_nothing_useful()
    {
        await _api.CreateUserAsync("van@test.local", Roles.Salesperson);

        var response = await _api.CreateClient().PostAsJsonAsync(
            "/api/auth/mobile/login", new { email = "van@test.local", password = "not the password" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email or password is incorrect", problem);
    }

    [Fact]
    public async Task A_nonsense_refresh_token_is_refused_rather_than_crashing()
    {
        var response = await _api.CreateClient().PostAsJsonAsync(
            "/api/auth/mobile/refresh", new { refreshToken = "not a token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
