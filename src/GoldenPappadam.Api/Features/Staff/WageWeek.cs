using System.Globalization;

namespace GoldenPappadam.Api.Features.Staff;

/// <summary>
/// The wage week: Sunday to Saturday, paid on the Saturday, the Saturday itself included.
/// Every screen and every payment gets its dates from here, so they can never disagree.
/// </summary>
public static class WageWeek
{
    public static DateOnly StartOf(DateOnly day) => day.AddDays(-(int)day.DayOfWeek);

    public static DateOnly EndOf(DateOnly day) => StartOf(day).AddDays(6);

    public static IEnumerable<DateOnly> Days(DateOnly weekStart) => Enumerable.Range(0, 7).Select(weekStart.AddDays);

    /// <summary>"20–26 Sep 2026", or "27 Sep – 3 Oct 2026" across a month end.</summary>
    public static string Describe(DateOnly weekStart)
    {
        var end = weekStart.AddDays(6);
        var culture = CultureInfo.GetCultureInfo("en-IN");

        return weekStart.Month == end.Month
            ? $"{weekStart.Day}–{end.ToString("d MMM yyyy", culture)}"
            : $"{weekStart.ToString("d MMM", culture)} – {end.ToString("d MMM yyyy", culture)}";
    }
}
