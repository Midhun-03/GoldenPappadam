using Microsoft.AspNetCore.Identity;

namespace GoldenPappadam.Infrastructure.Identity;

/// <summary>
/// Admin user. Guid keys keep Identity consistent with the rest of the schema.
/// No roles or permissions in phase 1: every signed-in user is an admin.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }

    public bool IsActive { get; set; } = true;
}
