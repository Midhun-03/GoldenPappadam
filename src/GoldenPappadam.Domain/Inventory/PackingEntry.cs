using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// One packing operation: source stock consumed, packs produced.
/// Saved together with the two stock movements it causes, in one transaction.
/// </summary>
public class PackingEntry : Entity
{
    public Guid PackedProductId { get; set; }
    public Product? PackedProduct { get; set; }

    /// <summary>
    /// Copied from the packed product when the entry is saved, so history stays correct
    /// if the product's source is ever changed.
    /// </summary>
    public Guid SourceProductId { get; set; }
    public Product? SourceProduct { get; set; }

    public decimal PacksProduced { get; set; }

    /// <summary>
    /// Source stock actually consumed, which may exceed PacksProduced * SourceQuantityPerPack
    /// because of packing loss. The screen suggests the theoretical figure; the user can correct it.
    /// </summary>
    public decimal SourceQuantityUsed { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    public string? Notes { get; set; }
}
