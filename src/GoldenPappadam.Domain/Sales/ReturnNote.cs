using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// Expired or damaged packets a shop gave back, and what the shop got for them. Returned packets are
/// never resold, so a return never adds to stock: the lines are the record of the loss. A
/// replacement sends fresh stock out; a credit is recorded as a payment of method
/// <see cref="PaymentMethod.ReturnCredit"/>, so it reduces what the shop owes and settles its oldest
/// bills exactly as money would.
///
/// Like an invoice, it is a document: numbered, never deleted, and only changed by being settled
/// (from Pending, once) or cancelled - see <see cref="PropertiesEditableAfterRecording"/>.
/// </summary>
public class ReturnNote : AuditableEntity
{
    public static readonly IReadOnlySet<string> PropertiesEditableAfterRecording = new HashSet<string>
    {
        nameof(Settlement),
        nameof(CreditAmount),
        nameof(CreditPaymentId),
        nameof(SettledAt),
        nameof(Status),
        nameof(CancelledAt),
        nameof(CancellationReason),
        nameof(UpdatedAt),
        nameof(UpdatedBy)
    };

    /// <summary>"RN/26-27/000001", from the same locked counter as invoices, in its own series.</summary>
    public required string ReturnNumber { get; set; }
    public required string SeriesCode { get; set; }
    public required string FinancialYear { get; set; }
    public int SequenceNumber { get; set; }

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>For a multi-branch customer, which shop gave the packets back - same rule as a bill.</summary>
    public Guid? BranchId { get; set; }
    public CustomerBranch? Branch { get; set; }

    /// <summary>The names as they were when the return was recorded.</summary>
    public required string CustomerName { get; set; }
    public string? BranchName { get; set; }

    /// <summary>The business date in IST.</summary>
    public DateOnly ReturnDate { get; set; }

    public ReturnStatus Status { get; set; } = ReturnStatus.Recorded;

    public ReturnSettlement Settlement { get; set; }

    /// <summary>What the returned packets were worth at the shop's rate: the sum of the lines.</summary>
    public decimal Value { get; set; }

    /// <summary>Taken off the shop's account. Zero unless settled as a credit.</summary>
    public decimal CreditAmount { get; set; }

    /// <summary>The <see cref="PaymentMethod.ReturnCredit"/> payment that carries the credit.</summary>
    public Guid? CreditPaymentId { get; set; }
    public Payment? CreditPayment { get; set; }

    /// <summary>UTC. When a pending return was settled; the creation time for one settled at once.</summary>
    public DateTime? SettledAt { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public List<ReturnNoteLine> Lines { get; set; } = [];
}
