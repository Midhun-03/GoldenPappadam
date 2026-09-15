using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// Creates the two roles and gives every account that predates them the admin role.
/// Without the backfill, introducing an admin-only fallback policy would lock the owner out of
/// their own system on the next start-up, so this has to run before the first request is served.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(RoleSeeder));

        if (!await CanReachDatabaseAsync(scope.ServiceProvider, logger))
        {
            return;
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created the {Role} role.", role);
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var user in await userManager.Users.ToListAsync())
        {
            if ((await userManager.GetRolesAsync(user)).Count > 0)
            {
                continue;
            }

            // Every account that existed before phase 3 was an admin, because there was nothing else.
            await userManager.AddToRoleAsync(user, Roles.Admin);
            logger.LogInformation("Gave the existing account {Email} the {Role} role.", user.Email, Roles.Admin);
        }
    }

    /// <summary>
    /// A fresh machine has no database until <c>dotnet ef database update</c> has been run.
    /// Say so plainly instead of throwing a connection error out of start-up.
    /// </summary>
    internal static async Task<bool> CanReachDatabaseAsync(IServiceProvider services, ILogger logger)
    {
        var db = services.GetRequiredService<AppDbContext>();

        if (await db.Database.CanConnectAsync())
        {
            return true;
        }

        logger.LogWarning(
            "The database is not reachable, so roles and the first admin account were not set up. " +
            "Run 'dotnet ef database update' and start again.");

        return false;
    }
}
