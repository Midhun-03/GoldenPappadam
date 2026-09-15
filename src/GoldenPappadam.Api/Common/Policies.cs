using GoldenPappadam.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// Two ways in, one set of rules. The React admin panel carries a session cookie; the Flutter app
/// carries a bearer token. Every policy accepts either, so nothing has to care which client it is
/// talking to.
///
/// The important part is what happens to an endpoint with no attribute at all: the fallback policy
/// is <see cref="AdminOnly"/>, so every controller written in phase 1 became admin-only without a
/// single one of them being edited. A new endpoint is admin-only until someone deliberately opens it.
/// </summary>
public static class Policies
{
    /// <summary>Office staff only.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>The salesperson's endpoints. Admins are included so the office can test them.</summary>
    public const string FieldSales = "FieldSales";

    private static readonly string[] Schemes =
        [IdentityConstants.ApplicationScheme, IdentityConstants.BearerScheme];

    /// <summary>
    /// Anyone signed in, whatever their role. Used by the handful of endpoints both clients need -
    /// who am I, sign out, change my password - and nothing else.
    /// </summary>
    public static AuthorizationPolicy SignedIn() =>
        new AuthorizationPolicyBuilder(Schemes).RequireAuthenticatedUser().Build();

    public static AuthorizationPolicy InRole(params string[] roles) =>
        new AuthorizationPolicyBuilder(Schemes).RequireAuthenticatedUser().RequireRole(roles).Build();

    public static AuthorizationPolicy Admin() => InRole(Roles.Admin);

    public static AuthorizationPolicy Field() => InRole(Roles.Salesperson, Roles.Admin);
}
