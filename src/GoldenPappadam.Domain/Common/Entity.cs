namespace GoldenPappadam.Domain.Common;

/// <summary>
/// Base type for every table. Created values are set automatically by the DbContext.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; set; }

    /// <summary>UTC. Set by the DbContext on insert.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Identity user id. Null until a user is signed in.</summary>
    public Guid? CreatedBy { get; set; }
}
