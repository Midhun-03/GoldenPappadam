using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// One line of a wage payment. Immutable, and carries its own copy of the status, fraction and rate
/// so it can be read without any master data.
/// </summary>
public class WagePaymentLine : Entity
{
    public Guid WagePaymentId { get; set; }
    public WagePayment? WagePayment { get; set; }

    public WagePaymentLineType LineType { get; set; }

    /// <summary>The day this line is about. For a carried balance, the week-end of the payment it came from.</summary>
    public DateOnly WorkDate { get; set; }

    /// <summary>Null when the day had no attendance record.</summary>
    public Guid? AttendanceStatusId { get; set; }

    /// <summary>"Present", "Half day", or "Not recorded" - as it read when paid.</summary>
    public string? StatusName { get; set; }

    /// <summary>For a correction: the fraction the day is now worth. Otherwise the fraction paid.</summary>
    public decimal DayFraction { get; set; }

    /// <summary>For a correction, what the day had been paid as.</summary>
    public string? PreviousStatusName { get; set; }

    public decimal? PreviousDayFraction { get; set; }

    /// <summary>The rate the day was paid at. A correction uses the original day's rate, not today's.</summary>
    public decimal DailyWage { get; set; }

    public decimal Amount { get; set; }

    /// <summary>
    /// For a correction, the payment that paid the day being corrected; for a carried balance, the
    /// payment whose overpayment is carried. While this payment stands, that one cannot be cancelled.
    /// </summary>
    public Guid? SourceWagePaymentId { get; set; }
    public WagePayment? SourceWagePayment { get; set; }
}
