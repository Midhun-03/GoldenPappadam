namespace GoldenPappadam.Infrastructure.Identity;

/// <summary>
/// Who is signed in, for the CreatedBy / UpdatedBy audit fields.
/// Implemented by the API from the HTTP context; returns null until login is built.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>
    /// Whether the signed-in user has the role. Services use it where a rule belongs to the business,
    /// not only to an endpoint - a salesperson may not change a live rate, whichever way they got in.
    /// </summary>
    bool IsInRole(string role);
}
