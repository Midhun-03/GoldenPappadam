using GoldenPappadam.Domain.Staff;

namespace GoldenPappadam.Api.Features.Staff;

/// <summary>
/// Which daily wage applies on a day: the latest one that had started by then, and of two that
/// started the same day, the one entered last (it corrected the other). The one rule, in one place.
/// </summary>
public static class WageRates
{
    public static EmployeeWageRate? InEffectOn(IEnumerable<EmployeeWageRate> rates, DateOnly day) =>
        rates
            .Where(r => r.EffectiveFrom <= day)
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault();

    /// <summary>The next rate that starts after <paramref name="day"/>, if one has been entered.</summary>
    public static EmployeeWageRate? UpcomingAfter(IEnumerable<EmployeeWageRate> rates, DateOnly day) =>
        rates
            .Where(r => r.EffectiveFrom > day)
            .OrderBy(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault();

    /// <summary>Money for part of a day, rounded to the paisa the way every wage figure is.</summary>
    public static decimal Pay(decimal dayFraction, decimal dailyWage) =>
        Math.Round(dayFraction * dailyWage, 2, MidpointRounding.AwayFromZero);
}
