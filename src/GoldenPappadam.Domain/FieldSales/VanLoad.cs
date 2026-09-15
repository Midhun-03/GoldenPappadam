using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.FieldSales;

/// <summary>
/// Stock moving between the warehouse and a van: the morning load sheet, and the evening return of
/// whatever did not sell. Recorded by the office, never by the salesperson, who has no way to move
/// stock at all.
///
/// Immutable, like every other document that touches the ledger. A load entered wrongly is
/// corrected with a return or an adjustment, not by editing what was written down.
/// </summary>
public class VanLoad : Entity
{
    public Guid VanLocationId { get; set; }
    public StockLocation? VanLocation { get; set; }

    /// <summary>
    /// The other end of the move. Named rather than assumed, so a second warehouse later is a
    /// row rather than a rewrite.
    /// </summary>
    public Guid WarehouseLocationId { get; set; }
    public StockLocation? WarehouseLocation { get; set; }

    public VanLoadDirection Direction { get; set; }

    /// <summary>UTC.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>The business day in IST, so a load at 6 am and a return at 9 pm are one day's work.</summary>
    public DateOnly BusinessDate { get; set; }

    public string? Notes { get; set; }

    public List<VanLoadLine> Lines { get; set; } = [];
}
