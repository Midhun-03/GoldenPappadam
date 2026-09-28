using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// One employee's wages for one Sunday-to-Saturday week, as handed over.
///
/// A snapshot: every day's status, fraction and rate is copied onto its lines, so a later raise, an
/// attendance correction or a new day value never changes what this says was paid. Cancelling is the
/// only update allowed (see <see cref="PropertiesEditableAfterPayment"/>); UpdatedAt/UpdatedBy are
/// then when and by whom it was cancelled, as on an invoice.
/// </summary>
public class WagePayment : AuditableEntity
{
    public static readonly IReadOnlySet<string> PropertiesEditableAfterPayment = new HashSet<string>
    {
        nameof(Status),
        nameof(CancelledAt),
        nameof(CancellationReason),
        nameof(UpdatedAt),
        nameof(UpdatedBy)
    };

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>The Sunday the week starts on.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>The Saturday it ends on - pay day.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>The IST date the money was handed over.</summary>
    public DateOnly PaymentDate { get; set; }

    /// <summary>Cash, UPI, bank transfer, cheque or other - never a return credit.</summary>
    public PaymentMethod Method { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    /// <summary>Sum of the day fractions of this week.</summary>
    public decimal DaysWorked { get; set; }

    /// <summary>This week's days × their rates.</summary>
    public decimal WorkAmount { get; set; }

    /// <summary>Corrections to earlier weeks plus any balance carried in. Often zero; may be negative.</summary>
    public decimal AdjustmentAmount { get; set; }

    /// <summary>What was handed over. Never negative.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Zero, or the negative remainder when earlier overpayments exceeded this week's pay. The next
    /// payment picks it up as a <see cref="WagePaymentLineType.CarriedBalance"/> line.
    /// </summary>
    public decimal CarriedForward { get; set; }

    public WagePaymentStatus Status { get; set; } = WagePaymentStatus.Paid;

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public List<WagePaymentLine> Lines { get; set; } = [];
}
