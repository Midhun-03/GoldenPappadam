namespace GoldenPappadam.Api.Features.Reports;

/// <summary>How a column's values are written: the screen, the PDF and Excel each format by kind.</summary>
public enum ReportColumnKind
{
    Text,
    Money,
    Quantity,
    Count,
    Date
}

/// <summary>Total marks a column whose values are added up in the section's totals row.</summary>
public record ReportColumn(string Key, string Title, ReportColumnKind Kind, bool Total = false);

/// <summary>One figure in the report's summary strip, e.g. "Sales ₹24,904.00".</summary>
public record ReportFigure(string Label, decimal Value, ReportColumnKind Kind);

/// <summary>
/// One table. Rows are keyed by column key, so the JSON the screen reads, the PDF and the Excel
/// sheet are the same data. Totals are worked out here, once, never by any of the three outputs.
/// </summary>
public record ReportSection(
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyDictionary<string, object?>? Totals,
    string? Note = null)
{
    public static ReportSection Create(
        string title,
        IReadOnlyList<ReportColumn> columns,
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        string? note = null)
    {
        var list = rows.ToList();
        Dictionary<string, object?>? totals = null;

        if (columns.Any(c => c.Total) && list.Count > 0)
        {
            totals = columns
                .Where(c => c.Total)
                .ToDictionary(c => c.Key, c => (object?)list.Sum(r => r.TryGetValue(c.Key, out var v) ? ToDecimal(v) : 0m));
        }

        return new ReportSection(title, columns, list, totals, note);
    }

    private static decimal ToDecimal(object? value) => value switch
    {
        decimal d => d,
        int i => i,
        long l => l,
        _ => 0m
    };
}

/// <summary>
/// A finished report. Period is written for people ("23 Sep 2026" or "1 Sep – 23 Sep 2026");
/// From/To are the business dates it covers, in IST.
/// </summary>
public record ReportDocument(
    string Name,
    string Title,
    string Period,
    DateOnly From,
    DateOnly To,
    string? Subtitle,
    IReadOnlyList<ReportFigure> Summary,
    IReadOnlyList<ReportSection> Sections,
    DateTime GeneratedAt);

/// <summary>Small helper so report queries build rows without repeating dictionary syntax.</summary>
public static class Row
{
    public static IReadOnlyDictionary<string, object?> Of(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, c => c.Value);
}
