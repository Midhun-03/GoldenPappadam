using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// Creates the very first admin account, because there is no public sign-up.
/// Runs only when the user table is empty and both settings are present; put them in
/// user secrets (development) or environment variables (server), never in a committed file.
/// </summary>
public static class AdminUserSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        var email = app.Configuration["Bootstrap:AdminEmail"];
        var password = app.Configuration["Bootstrap:AdminPassword"];
        var fullName = app.Configuration["Bootstrap:AdminFullName"] ?? "Administrator";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AdminUserSeeder));

        if (!await RoleSeeder.CanReachDatabaseAsync(scope.ServiceProvider, logger))
        {
            return;
        }

        if (userManager.Users.Any())
        {
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName };
        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            // RoleSeeder has already run, so this account is past its backfill: say so explicitly.
            await userManager.AddToRoleAsync(user, Roles.Admin);
            logger.LogInformation("Created the first admin account for {Email}.", email);
        }
        else
        {
            logger.LogError("Could not create the first admin account: {Errors}", IdentityErrors.Describe(result));
        }
    }
}
