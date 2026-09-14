namespace GoldenPappadam.Domain.Common;

/// <summary>
/// Base type for rows that can be edited after creation (master data).
/// Records that are never edited - stock movements, packing entries, payments - use <see cref="Entity"/>.
/// </summary>
public abstract class AuditableEntity : Entity
{
    /// <summary>UTC. Set by the DbContext on update.</summary>
    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }
}
