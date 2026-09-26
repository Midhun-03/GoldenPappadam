using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// Unsold packets opened and packed again, when the office decides they have sat too long - into
/// the same size or another. Nothing is lost: the pieces (or grams) that go in all come out, as new
/// packets and, when they do not fill a whole packet, as loose stock. The new packets start a fresh
/// shelf life from this day; the loose left-over keeps the age of the packets it came from.
/// Saved together with its stock movements, in one transaction, at the warehouse.
/// </summary>
public class RepackEntry : Entity
{
    public Guid FromProductId { get; set; }
    public Product? FromProduct { get; set; }

    /// <summary>Packets opened.</summary>
    public decimal FromQuantity { get; set; }

    public Guid ToProductId { get; set; }
    public Product? ToProduct { get; set; }

    /// <summary>Packets made, worked out by the server so nothing is lost.</summary>
    public decimal ToQuantity { get; set; }

    /// <summary>The loose product any left-over pieces went back to. Null when nothing was left over.</summary>
    public Guid? LeftoverProductId { get; set; }
    public Product? LeftoverProduct { get; set; }

    public decimal LeftoverQuantity { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    public string? Notes { get; set; }
}
