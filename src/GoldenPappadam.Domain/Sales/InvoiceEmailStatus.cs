namespace GoldenPappadam.Domain.Sales;

public enum InvoiceEmailStatus
{
    Sent,

    /// <summary>The mail server refused or could not be reached. The invoice itself is unaffected.</summary>
    Failed
}
