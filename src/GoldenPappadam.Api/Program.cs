using System.Text.Json.Serialization;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Dashboard;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using GoldenPappadam.Api.Features.Inventory.Packing;
using GoldenPappadam.Api.Features.Inventory.Products;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Defaults to LocalDB in appsettings.json; a server overrides it with an environment variable.
// Failing here beats a confusing "ConnectionString has not been initialized" on the first request.
var connectionString = builder.Configuration.GetConnectionString("GoldenPappadam")
                       ?? throw new InvalidOperationException(
                           "Connection string 'GoldenPappadam' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Two ways in: the React panel uses the cookie, the Flutter app uses a bearer token. Identity's own
// bearer scheme gives us access and refresh tokens without hand-rolling any JWT code, and the
// cookie setup below is untouched, so the admin panel is unaffected.
var authentication = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);

authentication.AddIdentityCookies();

authentication.AddBearerToken(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromHours(1);

    // A salesperson can be out of signal for days; the phone keeps working offline either way,
    // but this is how long it can go without having to type a password again.
    options.RefreshTokenExpiration = TimeSpan.FromDays(30);
});

// This is an API, so an expired or missing session must answer 401, never redirect to a login page.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "goldenpappadam.auth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// An endpoint with no authorization attribute is admin-only. That is what makes every controller
// written in phase 1 safe against a salesperson token without editing any of them, and it means a
// new endpoint is closed until someone deliberately opens it.
// A bare [Authorize] means "anyone signed in", which only the shared auth endpoints use.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(Policies.Admin())
    .SetDefaultPolicy(Policies.SignedIn())
    .AddPolicy(Policies.AdminOnly, Policies.Admin())
    .AddPolicy(Policies.FieldSales, Policies.Field());

builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<PackingService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CustomerPriceService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<VanLoadService>();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Not in development: the React dev server proxies plain HTTP to this API, and a redirect to
// HTTPS would break those calls (and the session cookie with them). Production still redirects.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await RoleSeeder.SeedAsync(app);
await AdminUserSeeder.SeedAsync(app);

app.Run();

/// <summary>Exposed so the integration tests can spin up the real application.</summary>
public partial class Program;
