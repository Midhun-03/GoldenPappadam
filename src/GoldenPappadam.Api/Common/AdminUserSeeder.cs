using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// Creates the very first admin account, because there is no public sign-up.
/// Runs only when the user table is empty and both settings are present; put them in
/// user secrets (development) or environment variables (server), never in a committed file.
///
/// On a development machine it can also put a forgotten admin password back: set
/// <c>Bootstrap:ResetAdminPassword</c> to true and the account named by <c>Bootstrap:AdminEmail</c>
/// gets <c>Bootstrap:AdminPassword</c> at the next start. Never on a server - there the password
/// is changed from the account menu, by someone who knows the old one.
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
            if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Bootstrap:ResetAdminPassword"))
            {
                await ResetPasswordAsync(userManager, email, password, logger);
            }

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

    private static async Task ResetPasswordAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            logger.LogError("Cannot reset the password: there is no account for {Email}.", email);
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, password);

        if (!result.Succeeded)
        {
            logger.LogError("Could not reset the password for {Email}: {Errors}", email, IdentityErrors.Describe(result));
            return;
        }

        // Too many wrong guesses may have locked it; a reset should let the owner straight back in.
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogWarning(
            "Reset the password for {Email} from user secrets. Remove Bootstrap:ResetAdminPassword now, " +
            "or it happens again at every start.", email);
    }
}
