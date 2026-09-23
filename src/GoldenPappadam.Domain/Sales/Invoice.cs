using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// A bill for goods delivered. Auditable only because it can be cancelled: the service allows
/// no other change, and the lines are immutable.
/// </summary>
public class Invoice : AuditableEntity
{
    public required string InvoiceNumber { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// Which shop of a multi-branch customer this bill is for. Null for a single-location
    /// customer, and always null for one, since the branch picker never appears for them.
    /// </summary>
    public Guid? BranchId { get; set; }
    public CustomerBranch? Branch { get; set; }

    /// <summary>The business date in IST, not a timestamp.</summary>
    public DateOnly InvoiceDate { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    /// <summary>Sum of the line totals.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Bill-level discount. Zero when unused.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>SubTotal minus DiscountAmount. What the customer owes for this bill.</summary>
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
}
