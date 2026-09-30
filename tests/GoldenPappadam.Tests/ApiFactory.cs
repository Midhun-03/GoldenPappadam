using System.Net.Http.Json;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GoldenPappadam.Infrastructure.Documents;
using GoldenPappadam.Infrastructure.Email;

namespace GoldenPappadam.Tests;

/// <summary>
/// The real application, start to finish, on a throwaway SQL Express database. Authorization is
/// wiring rather than logic - policies, schemes, roles and attributes - so the only honest way to
/// test it is to send a real request through the real pipeline and read the real status code.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Pappadam!2026";

    private readonly string _databaseName = $"GoldenPappadam_Api_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=.\\SQLEXPRESS;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _documentsPath = Path.Combine(Path.GetTempPath(), $"GoldenPappadam_Docs_{Guid.NewGuid():N}");

    /// <summary>Records what the app sent, and fails on demand, so email tests need no mail server.</summary>
    public FakeEmailSender Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:GoldenPappadam", ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvoiceDocumentStorage>();
            services.AddSingleton<IInvoiceDocumentStorage>(new LocalInvoiceDocumentStorage(_documentsPath));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
        });
    }

    /// <summary>
    /// The database has to exist before the host starts, because the role seeder runs during
    /// start-up and skips its work when it cannot reach the database.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await using (var db = NewContext())
        {
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();

        if (Directory.Exists(_documentsPath))
        {
            Directory.Delete(_documentsPath, recursive: true);
        }
    }

    public async Task<ApplicationUser> CreateUserAsync(string email, string role)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { UserName = email, Email = email, FullName = email };

        var created = await users.CreateAsync(user, Password);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

        var assigned = await users.AddToRoleAsync(user, role);
        Assert.True(assigned.Succeeded, string.Join(", ", assigned.Errors.Select(e => e.Description)));

        return user;
    }

    /// <summary>A client carrying a bearer token, the way the Flutter app will talk to the API.</summary>
    public async Task<HttpClient> SignInAsync(string email)
    {
        var client = CreateClient();
        var tokens = await RequestTokensAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

        return client;
    }

    public async Task<TokenPair> RequestTokensAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/mobile/login", new { email, password = Password });

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TokenPair>()
               ?? throw new InvalidOperationException("The login returned no tokens.");
    }

    public async Task DeactivateAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByIdAsync(userId.ToString())
                   ?? throw new InvalidOperationException("No such user.");

        user.IsActive = false;
        await users.UpdateAsync(user);
        await users.UpdateSecurityStampAsync(user);
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options, new NoUser());

    public record TokenPair(string AccessToken, string RefreshToken, long ExpiresIn);

    private sealed class NoUser : ICurrentUser
    {
        public Guid? UserId => null;

        public bool IsInRole(string role) => false;
    }

    public sealed class FakeEmailSender : IEmailSender
    {
        private readonly List<EmailMessage> _sent = [];

        /// <summary>When set, every send fails with this reason, as an unreachable mail server would.</summary>
        public string? FailWith { get; set; }

        public IReadOnlyList<EmailMessage> Sent
        {
            get
            {
                lock (_sent) return _sent.ToList();
            }
        }

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            if (FailWith is not null)
            {
                throw new EmailDeliveryException(FailWith);
            }

            lock (_sent) _sent.Add(message);

            return Task.CompletedTask;
        }
    }
}
