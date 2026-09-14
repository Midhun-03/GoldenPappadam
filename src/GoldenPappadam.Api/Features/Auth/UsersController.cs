using System.Security.Claims;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Auth;

/// <summary>
/// The handful of admin accounts. Phase 1 has no roles: every signed-in user is an admin
/// and can add another one.
/// </summary>
[ApiController]
[Route("api/admin/users")]
public class UsersController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> GetAll(CancellationToken ct) =>
        await userManager.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.Email!, u.FullName, u.IsActive))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<UserDto> Create(CreateUserRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new DomainException(IdentityErrors.Describe(result));
        }

        return new UserDto(user.Id, user.Email!, user.FullName, user.IsActive);
    }

    /// <summary>Users are deactivated, never deleted, because their id sits in audit fields.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<UserDto> SetActive(Guid id, bool isActive)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException("User");

        if (!isActive && id == CurrentUserId())
        {
            throw new DomainException("You cannot deactivate your own account.");
        }

        if (!isActive && await userManager.Users.CountAsync(u => u.IsActive) <= 1)
        {
            throw new DomainException("At least one active admin account must remain.");
        }

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new DomainException(IdentityErrors.Describe(result));
        }

        // Ends any session the deactivated user still has open.
        await userManager.UpdateSecurityStampAsync(user);

        return new UserDto(user.Id, user.Email!, user.FullName, user.IsActive);
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
