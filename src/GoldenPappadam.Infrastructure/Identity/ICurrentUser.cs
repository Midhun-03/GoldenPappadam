namespace GoldenPappadam.Infrastructure.Identity;

/// <summary>
/// Who is signed in, for the CreatedBy / UpdatedBy audit fields.
/// Implemented by the API from the HTTP context; returns null until login is built.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
