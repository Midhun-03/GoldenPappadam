namespace GoldenPappadam.Domain.Sales;

public enum InvoiceStatus
{
    /// <summary>Delivered and owed. The only state a new invoice starts in.</summary>
    Issued,

    /// <summary>Reversed. Stock went back and nothing is owed, but the bill itself is kept.</summary>
    Cancelled
}
