using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>One attempt to email an invoice. CreatedAt is when it was attempted; never edited.</summary>
public class InvoiceEmailLog : Entity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public required string Recipient { get; set; }

    public required string Subject { get; set; }

    public InvoiceEmailStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>1 for the first attempt on this invoice, 2 for the first retry, and so on.</summary>
    public int AttemptNumber { get; set; }
}
