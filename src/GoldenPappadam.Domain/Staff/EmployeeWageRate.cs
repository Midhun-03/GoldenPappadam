using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// One daily wage and the day it started. A raise adds a row and never overwrites one, so a past
/// day is always paid at the rate that was in effect on it. Immutable.
///
/// Two rows with the same start date mean the later one corrected the earlier; the newest wins.
/// A rate can never start inside a week that is already paid (see <c>WageRateService</c>), which is
/// what keeps a correction from reaching back into money already handed over.
/// </summary>
public class EmployeeWageRate : Entity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal DailyWage { get; set; }

    public DateOnly EffectiveFrom { get; set; }
}
