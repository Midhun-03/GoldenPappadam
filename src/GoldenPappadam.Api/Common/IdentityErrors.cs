using Microsoft.AspNetCore.Identity;

namespace GoldenPappadam.Api.Common;

public static class IdentityErrors
{
    /// <summary>Turns Identity's error list into one readable sentence for the client.</summary>
    public static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
