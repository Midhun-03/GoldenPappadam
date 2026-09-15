using GoldenPappadam.Api.Common;
using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GoldenPappadam.Api.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    /// <summary>
    /// Signs in and issues the session cookie. Wrong email and wrong password give the same
    /// answer on purpose, so the endpoint cannot be used to find out who has an account.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<CurrentUserDto> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.IsActive)
        {
            throw new DomainException("Email or password is incorrect.");
        }

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new DomainException("Too many failed attempts. Try again in a few minutes.");
        }

        if (!result.Succeeded)
        {
            throw new DomainException("Email or password is incorrect.");
        }

        return await DescribeAsync(user);
    }

    /// <summary>
    /// The same sign-in for the Flutter app, answering with an access token and a refresh token
    /// instead of setting a cookie. The rules are identical: it is the same SignInManager, the same
    /// lockout, and the same deliberately vague failure message.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("mobile/login")]
    public async Task<IActionResult> MobileLogin(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null || !user.IsActive)
        {
            throw new DomainException("Email or password is incorrect.");
        }

        // Everything above this line can still fail cleanly. Once the sign-in below succeeds the
        // handler has already written the token response, so nothing may throw after it.
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            throw new DomainException("Too many failed attempts. Try again in a few minutes.");
        }

        if (!result.Succeeded)
        {
            throw new DomainException("Email or password is incorrect.");
        }

        return Empty;
    }

    /// <summary>
    /// Trades a refresh token for a new pair. The security stamp is re-checked, so deactivating an
    /// account or changing its password stops the phone at its next refresh.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("mobile/refresh")]
    public async Task<IActionResult> MobileRefresh(
        RefreshRequest request,
        [FromServices] IOptionsMonitor<BearerTokenOptions> bearerOptions,
        [FromServices] TimeProvider clock)
    {
        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expiresAt ||
            clock.GetUtcNow() >= expiresAt ||
            await signInManager.ValidateSecurityStampAsync(ticket.Principal) is not { IsActive: true } user)
        {
            return Challenge(authenticationSchemes: [IdentityConstants.BearerScheme]);
        }

        return SignIn(await signInManager.CreateUserPrincipalAsync(user), IdentityConstants.BearerScheme);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();

        return NoContent();
    }

    /// <summary>Who is signed in. Both clients call this on start-up to decide what to show.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<CurrentUserDto> Me()
    {
        var user = await userManager.GetUserAsync(User)
                   ?? throw new NotFoundException("Signed-in user");

        return await DescribeAsync(user);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User)
                   ?? throw new NotFoundException("Signed-in user");

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new DomainException(IdentityErrors.Describe(result));
        }

        // Keep this session valid after the password change.
        await signInManager.RefreshSignInAsync(user);

        return NoContent();
    }

    /// <summary>
    /// The role goes to the client so it can show the right screens. It is a convenience, never a
    /// permission: the API decides what each role may do, whatever the client believes.
    /// </summary>
    private async Task<CurrentUserDto> DescribeAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        return new CurrentUserDto(user.Id, user.Email!, user.FullName, roles.FirstOrDefault() ?? Roles.Admin);
    }
}
