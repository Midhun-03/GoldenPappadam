using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// A finalized bill for goods delivered - an accounting document. It is born finalized: there is
/// no saved draft, because a draft that holds a number leaves gaps when abandoned and one that
/// does not is just the New Bill form. Auditable only because it can be cancelled; the DbContext
/// refuses any other change (see <see cref="PropertiesEditableAfterFinalization"/>).
///
/// Everything printed on it - customer, branch, supplier, prices, tax - is copied here when it is
/// finalized, so renaming a shop or changing a rate later can never change an old invoice.
///
/// Audit: CreatedAt/CreatedBy are when and by whom it was finalized, since the two are the same
/// moment. Cancelling is the only update ever allowed, so UpdatedAt/UpdatedBy are when and by whom
/// it was cancelled - a separate CancelledBy column would only repeat them.
/// </summary>
public class Invoice : AuditableEntity
{
    /// <summary>
    /// The only properties that may change once an invoice exists. Everything else is the
    /// document itself; a mistake in it is corrected by cancelling and issuing a new one.
    /// </summary>
    public static readonly IReadOnlySet<string> PropertiesEditableAfterFinalization = new HashSet<string>
    {
        nameof(Status),
        nameof(CancelledAt),
        nameof(CancellationReason),
        nameof(UpdatedAt),
        nameof(UpdatedBy)
    };

    /// <summary>"GP/26-27/000125". Built from the three fields below, which are what is unique.</summary>
    public required string InvoiceNumber { get; set; }

    /// <summary>The numbering series, "GP". Several series can run side by side later.</summary>
    public required string SeriesCode { get; set; }

    /// <summary>Indian financial year, "2026-27". Numbering restarts with each one.</summary>
    public required string FinancialYear { get; set; }

    /// <summary>Position in the series for that financial year, handed out by the database.</summary>
    public int SequenceNumber { get; set; }

    public InvoiceDocumentType DocumentType { get; set; }

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

    // ---- the supplier, as printed ----

    public required string SupplierName { get; set; }
    public string? SupplierAddress { get; set; }

    /// <summary>Null when the business had no GSTIN on file, which is what makes this a plain Invoice.</summary>
    public string? SupplierGstin { get; set; }
    public required string SupplierStateCode { get; set; }

    // ---- the customer and branch, as printed ----

    public required string CustomerName { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerGstin { get; set; }
    public string? CustomerStateCode { get; set; }

    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string? BranchPhone { get; set; }
    public string? BranchGstin { get; set; }
    public string? BranchStateCode { get; set; }

    // ---- tax basis ----

    /// <summary>Where the goods were delivered. Null only when no tax was charged.</summary>
    public string? PlaceOfSupplyStateCode { get; set; }

    /// <summary>True means IGST; false means CGST + SGST.</summary>
    public bool IsInterState { get; set; }

    /// <summary>Always false for a pappadam sale; kept because the invoice must state it.</summary>
    public bool ReverseCharge { get; set; }

    /// <summary>Whether the rates on the lines already included GST when this bill was made.</summary>
    public bool PricesIncludeTax { get; set; }

    // ---- amounts ----

    /// <summary>Sum of the line amounts (quantity × rate), before discount.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Bill-level discount. Zero when unused. Shared across the lines to reduce their taxable value.</summary>
    public decimal DiscountAmount { get; set; }

    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }

    /// <summary>Rounding to the rupee, when the business rounds. Between -0.50 and +0.50.</summary>
    public decimal RoundOff { get; set; }

    /// <summary>The grand total: what the customer owes for this bill.</summary>
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
}
