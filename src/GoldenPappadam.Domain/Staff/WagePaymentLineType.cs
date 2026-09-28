namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// What a line on a wage payment is for. Overtime, bonuses, advances and deductions are expected to
/// become further values here, each an ordinary line with an amount.
/// </summary>
public enum WagePaymentLineType
{
    /// <summary>One day of the paid week, with the status, fraction and rate it was paid at.</summary>
    Attendance,

    /// <summary>
    /// A day in an earlier, already-paid week whose attendance was corrected afterwards. Plus if the
    /// employee was underpaid, minus if overpaid.
    /// </summary>
    Correction,

    /// <summary>An earlier payment's overpayment that was too large to recover in its own week.</summary>
    CarriedBalance
}
