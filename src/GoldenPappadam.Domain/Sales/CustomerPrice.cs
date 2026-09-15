using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// What one shop pays for one product. The same 20-piece packet is 35 for one shop and 34 for the
/// next, and that is a rule the office sets - never the salesperson, whose app has no price field
/// at all.
///
/// This is the price that <em>will</em> be charged, not a record of what <em>was</em>: the price
/// actually charged is frozen on <see cref="InvoiceLine.UnitPrice"/> when the bill is made, so
/// changing a price here never disturbs a bill that already exists.
/// </summary>
public class CustomerPrice : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Deactivated rather than deleted, so the shop simply falls back to the product's own price
    /// and the row stays available to say what the arrangement used to be.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
