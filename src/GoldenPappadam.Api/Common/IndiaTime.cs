namespace GoldenPappadam.Api.Common;

/// <summary>
/// The business runs on Indian Standard Time while the database stores UTC. Every "today"
/// and every day-based report goes through here, so a sale entered at 2 am IST lands on the
/// right day instead of the previous one.
/// </summary>
public static class IndiaTime
{
    private static readonly TimeZoneInfo Zone = Find();

    public static DateOnly Today() => DateOnly.FromDateTime(Now());

    public static DateTime Now() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    /// <summary>
    /// The UTC window covering one IST business day, for tables that store a timestamp rather than
    /// a date. Half-open: start is inclusive, end is exclusive.
    /// </summary>
    public static (DateTime Start, DateTime End) DayRangeUtc(DateOnly day)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone);

        return (start, start.AddDays(1));
    }

    /// <summary>The IST day a UTC timestamp falls on.</summary>
    public static DateOnly ToIndiaDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Zone));

    private static TimeZoneInfo Find()
    {
        // Windows and Linux name the same zone differently.
        foreach (var id in new[] { "India Standard Time", "Asia/Kolkata" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the other spelling.
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromMinutes(330), "IST", "IST");
    }
}
