using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

public class ProductCategory : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>Categories are deactivated, never deleted, because products refer to them.</summary>
    public bool IsActive { get; set; } = true;
}
