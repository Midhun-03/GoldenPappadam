using System.Security.Claims;
using GoldenPappadam.Infrastructure.Identity;

namespace GoldenPappadam.Api.Common;

/// <summary>Reads the signed-in user's id from the request. Null until login is built.</summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public bool IsInRole(string role) => httpContextAccessor.HttpContext?.User.IsInRole(role) ?? false;
}
