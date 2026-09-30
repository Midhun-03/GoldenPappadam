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
    /// Source stock consumed. Calculated from the product's conversion since 2026-09-30 (packets ×
    /// pieces ÷ pieces per kg, or packets × weight); entries made before that recorded what the office
    /// typed, and keep it.
    /// </summary>
    public decimal SourceQuantityUsed { get; set; }

    // ---- the conversion as it was when packed, so a later change to a product never rewrites history.
    // Null on entries made before 2026-09-30, which never recorded them.

    /// <summary>Pieces per packet, for a count-based packet.</summary>
    public int? PiecesPerPack { get; set; }

    /// <summary>The loose variety's pieces per kg, for a count-based packet.</summary>
    public decimal? PiecesPerKg { get; set; }

    /// <summary>Source stock one pack used, in the source's unit: 0.100 kg for 20 pieces at 200/kg.</summary>
    public decimal? SourcePerPack { get; set; }

    /// <summary>Source stock in the warehouse just before packing. After = before − used.</summary>
    public decimal? SourceOnHandBefore { get; set; }

    /// <summary>
    /// Made by the packing screen when it opens, so pressing Save twice packs once. Null on older entries.
    /// </summary>
    public Guid? ClientRequestId { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    public string? Notes { get; set; }
}
