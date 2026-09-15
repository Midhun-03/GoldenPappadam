using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// Where stock physically is. Until the van started selling there was only one answer, so stock was
/// a single number per product; now "how many 20-piece packets are there" has to say whether it
/// means the warehouse or the van.
///
/// A table rather than an enum, so a second van costs a row rather than a migration.
/// </summary>
public class StockLocation : AuditableEntity
{
    public required string Code { get; set; }

    public required string Name { get; set; }

    public StockLocationKind Kind { get; set; }

    /// <summary>Deactivated, never deleted: movements refer to it forever.</summary>
    public bool IsActive { get; set; } = true;
}
