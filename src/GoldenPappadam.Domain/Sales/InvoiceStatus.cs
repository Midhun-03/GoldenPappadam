namespace GoldenPappadam.Domain.Sales;

public enum InvoiceStatus
{
    /// <summary>
    /// Finalized: numbered, delivered and owed. The only state a new invoice starts in - the
    /// draft stage is the New Bill form, or a sale waiting in the phone's outbox, never a row here.
    /// </summary>
    Issued,

    /// <summary>Reversed. Stock went back and nothing is owed, but the bill and its number are kept.</summary>
    Cancelled
}
