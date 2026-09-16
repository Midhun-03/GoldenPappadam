using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.FieldSales;

/// <summary>
/// What the salesperson needs the packing unit to pack, and when they need it by.
///
/// This is a request for stock, not a customer order: nobody is billed, no stock moves, and
/// nothing is reserved. It exists so the packing unit knows what tomorrow looks like instead of
/// guessing - which is the job the sales team currently does by telling somebody in person.
///
/// Auditable because the status changes as the packing unit works through it. The lines never do.
/// </summary>
public class StockRequest : AuditableEntity
{
    /// <summary>The phone it came from. Null when the office entered it.</summary>
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>The business day in IST the salesperson needs it for - usually tomorrow.</summary>
    public DateOnly RequiredDate { get; set; }

    public StockRequestStatus Status { get; set; } = StockRequestStatus.Requested;

    public string? Notes { get; set; }

    public List<StockRequestLine> Lines { get; set; } = [];
}
