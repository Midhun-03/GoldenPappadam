using System.Globalization;
using GoldenPappadam.Api.Common;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>The dates a report covers: IST business days, today when none are given.</summary>
public static class ReportPeriod
{
    /// <summary>A year is plenty for any report here, and stops one request scanning the whole history.</summary>
    private const int MaxDays = 366;

    public static (DateOnly From, DateOnly To) Resolve(DateOnly? from, DateOnly? to)
    {
        var end = to ?? from ?? IndiaTime.Today();
        var start = from ?? end;

        if (start > end)
        {
            throw new DomainException("The start date is after the end date.");
        }

        if (end.DayNumber - start.DayNumber >= MaxDays)
        {
            throw new DomainException("A report can cover at most a year. Choose a shorter period.");
        }

        return (start, end);
    }

    /// <summary>"23 Sep 2026", or "1 Sep – 23 Sep 2026" for a range.</summary>
    public static string Describe(DateOnly from, DateOnly to)
    {
        static string Day(DateOnly d, bool year) =>
            d.ToString(year ? "d MMM yyyy" : "d MMM", CultureInfo.InvariantCulture);

        return from == to
            ? Day(from, true)
            : $"{Day(from, from.Year != to.Year)} – {Day(to, true)}";
    }
}
