using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// The last number issued in one series for one financial year. The database increments it under
/// a row lock inside the transaction that saves the invoice, so two devices finalizing at the same
/// instant queue up for the row and can never be handed the same number. A new financial year is
/// simply a new row, created by the first invoice dated in it; nothing is ever reset by hand.
/// </summary>
public class InvoiceNumberSequence : AuditableEntity
{
    public required string SeriesCode { get; set; }

    /// <summary>"2026-27".</summary>
    public required string FinancialYear { get; set; }

    public int LastNumber { get; set; }
}
