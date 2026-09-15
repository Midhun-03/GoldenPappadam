using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.FieldSales;

/// <summary>
/// A salesperson's phone. Answers "which device sent this", ties a sale to the van the phone rides
/// in, and lets the office cut off a handset that has been lost.
/// </summary>
public class Device : AuditableEntity
{
    /// <summary>The Identity user this phone belongs to.</summary>
    public Guid UserId { get; set; }

    public required string Name { get; set; }

    public required string Platform { get; set; }

    /// <summary>
    /// The van this phone rides in. A sale submitted from here takes its stock off that van.
    /// Null until the office says which van, in which case sales fall back to the warehouse.
    /// </summary>
    public Guid? LocationId { get; set; }
    public StockLocation? Location { get; set; }

    /// <summary>UTC. Updated on every sync, so the office can see when a phone last reported in.</summary>
    public DateTime LastSeenAt { get; set; }

    public bool IsActive { get; set; } = true;
}
