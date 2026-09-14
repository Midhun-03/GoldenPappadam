using GoldenPappadam.Api.Common;
using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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

        return new CurrentUserDto(user.Id, user.Email!, user.FullName);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();

        return NoContent();
    }

    /// <summary>Who is signed in. The React app calls this on start-up to decide what to show.</summary>
    [HttpGet("me")]
    public async Task<CurrentUserDto> Me()
    {
        var user = await userManager.GetUserAsync(User)
                   ?? throw new NotFoundException("Signed-in user");

        return new CurrentUserDto(user.Id, user.Email!, user.FullName);
    }

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
}
