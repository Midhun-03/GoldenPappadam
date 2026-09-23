using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// The business as it appears on its invoices, and how it bills. One row, with a fixed id.
/// Invoices copy what they print from here, so editing these never changes an invoice already made.
/// </summary>
public class InvoiceSettings : AuditableEntity
{
    public static readonly Guid SingletonId = new("6a1c0de2-3f5b-4c1e-9e57-1f0b7a2d4c01");

    public required string LegalName { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Null means the business is not GST-registered, or the number has not been entered yet.
    /// Bills are then plain invoices with no tax. Entering it is what switches GST on.
    /// </summary>
    public string? Gstin { get; set; }

    /// <summary>The supplier's GST state code, "32" for Kerala. Decides intra- versus inter-state.</summary>
    public required string StateCode { get; set; }

    /// <summary>
    /// The prefix of every invoice number, "GP". At most three characters, because GST allows an
    /// invoice number sixteen characters in all: GP/26-27/000125 is fifteen.
    /// </summary>
    public required string SeriesCode { get; set; }

    /// <summary>
    /// Whether the agreed rates and selling prices already include GST - the way an MRP does.
    /// Only matters once the business has a GSTIN; to be confirmed with the accountant.
    /// </summary>
    public bool PricesIncludeTax { get; set; } = true;

    /// <summary>Round the grand total to the nearest rupee and show the round-off.</summary>
    public bool RoundToNearestRupee { get; set; }

    /// <summary>"Payable on next delivery", printed under the totals.</summary>
    public string? PaymentTerms { get; set; }

    /// <summary>Bank name, account number, IFSC, UPI id - free text, printed as written.</summary>
    public string? BankDetails { get; set; }

    public string? TermsAndConditions { get; set; }
}
