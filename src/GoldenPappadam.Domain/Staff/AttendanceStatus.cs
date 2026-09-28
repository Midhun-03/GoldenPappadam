using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// Present, Half day, Absent, Leave - and how much of a day's wage each one earns. The fraction is
/// data, not code, so the business can change it; a change applies only to weeks not yet paid,
/// because a wage payment keeps the fraction it paid at.
/// </summary>
public class AttendanceStatus : AuditableEntity
{
    /// <summary>Stable identifier for code and tests, e.g. "HalfDay". Never shown.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>1 for a full day, 0.5 for half, 0 for unpaid.</summary>
    public decimal DayFraction { get; set; }

    public int SortOrder { get; set; }
}
