using System.Security.Claims;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Auth;

/// <summary>
/// The accounts: office staff and the people on the road. Admin-only through the fallback policy,
/// so a salesperson token cannot reach any of this.
/// </summary>
[ApiController]
[Route("api/admin/users")]
public class UsersController(AppDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> GetAll(CancellationToken ct) =>
        await db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(
                u.Id,
                u.Email!,
                u.FullName,
                db.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .FirstOrDefault() ?? Roles.Admin,
                u.IsActive))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<UserDto> Create(CreateUserRequest request)
    {
        if (!Roles.All.Contains(request.Role))
        {
            throw new DomainException($"'{request.Role}' is not a role. Use {string.Join(" or ", Roles.All)}.");
        }

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

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);

        if (!roleResult.Succeeded)
        {
            // An account with no role can do nothing at all, so do not leave one behind.
            await userManager.DeleteAsync(user);
            throw new DomainException(IdentityErrors.Describe(roleResult));
        }

        return new UserDto(user.Id, user.Email!, user.FullName, request.Role, user.IsActive);
    }

    /// <summary>Users are deactivated, never deleted, because their id sits in audit fields.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<UserDto> SetActive(Guid id, bool isActive)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
                   ?? throw new NotFoundException("User");

        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Admin;

        if (!isActive && id == CurrentUserId())
        {
            throw new DomainException("You cannot deactivate your own account.");
        }

        // Counting admins, not accounts: a system full of salespeople and no admin is a locked door.
        if (!isActive && role == Roles.Admin && await ActiveAdminCountAsync() <= 1)
        {
            throw new DomainException("At least one active admin account must remain.");
        }

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new DomainException(IdentityErrors.Describe(result));
        }

        // Ends any session the deactivated user still has open, and stops their phone at its
        // next token refresh.
        await userManager.UpdateSecurityStampAsync(user);

        return new UserDto(user.Id, user.Email!, user.FullName, role, user.IsActive);
    }

    private async Task<int> ActiveAdminCountAsync()
    {
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);

        return admins.Count(u => u.IsActive);
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
