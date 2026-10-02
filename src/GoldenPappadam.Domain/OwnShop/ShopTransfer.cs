using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.OwnShop;

/// <summary>
/// Loose pappadam sent from the factory to the own shop. The factory counts it in kg, the shop sells it
/// by the piece, so the transfer converts as it moves: 30 kg of the standard pappadam at 200 pieces per
/// kg arrives as 6,000 pieces. Saved together with its two <see cref="StockMovementType.ShopTransfer"/>
/// movements - kg out of the warehouse, pieces into the shop - in one transaction.
///
/// Immutable, and it keeps the conversion it used, so a later change to a variety's pieces per kg
/// never rewrites what the shop received.
/// </summary>
public class ShopTransfer : Entity
{
    /// <summary>The loose variety, counted in kg, that left the factory.</summary>
    public Guid SourceProductId { get; set; }
    public Product? SourceProduct { get; set; }

    /// <summary>The same variety counted in pieces, as the shop holds it.</summary>
    public Guid PiecesProductId { get; set; }
    public Product? PiecesProduct { get; set; }

    public Guid FromLocationId { get; set; }
    public StockLocation? FromLocation { get; set; }

    public Guid ToLocationId { get; set; }
    public StockLocation? ToLocation { get; set; }

    /// <summary>What the factory weighed out and recorded.</summary>
    public decimal QuantityKg { get; set; }

    /// <summary>The variety's pieces per kg when it was sent.</summary>
    public decimal PiecesPerKg { get; set; }

    /// <summary>Kg × pieces per kg, rounded to the nearest whole piece (owner, 2026-09-30).</summary>
    public decimal PiecesReceived { get; set; }

    /// <summary>The factory's loose stock just before. After = before − kg.</summary>
    public decimal SourceOnHandBefore { get; set; }

    /// <summary>The shop's pieces just before. After = before + pieces.</summary>
    public decimal ShopOnHandBefore { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>Made by the screen when it opens, so pressing Save twice sends it once.</summary>
    public Guid? ClientRequestId { get; set; }

    public string? Notes { get; set; }
}
