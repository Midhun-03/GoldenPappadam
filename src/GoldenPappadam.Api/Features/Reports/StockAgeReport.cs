using GoldenPappadam.Api.Features.Inventory.Stock;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// How old the stock is in each place, for printing or Excel - the same figures as the Stock age
/// screen, which is where the office repacks and writes off.
/// </summary>
public class StockAgeReport(StockAgeService ages)
{
    public async Task<ReportDocument> BuildAsync(DateOnly asOf, CancellationToken ct)
    {
        var rows = await ages.GetAsync(asOf, ct);

        var columns = new[]
        {
            new ReportColumn("product", "Product", ReportColumnKind.Text),
            new ReportColumn("fresh", "Fresh", ReportColumnKind.Quantity),
            new ReportColumn("repack", "Worth repacking", ReportColumnKind.Quantity),
            new ReportColumn("ageing", "Ageing", ReportColumnKind.Quantity),
            new ReportColumn("expiring", "Expiring soon", ReportColumnKind.Quantity),
            new ReportColumn("expired", "Expired", ReportColumnKind.Quantity),
            new ReportColumn("total", "In stock", ReportColumnKind.Quantity),
            new ReportColumn("oldest", "Oldest packed", ReportColumnKind.Date)
        };

        static decimal? Show(decimal value) => value == 0m ? null : value;

        // The bands depend on the shelf life; say what they are for each one in use.
        var bands = string.Join(" ", rows
            .Select(r => r.ShelfLifeDays)
            .Distinct()
            .Order()
            .Select(life => AgeBands.For(life))
            .Select(b => $"For a {b.ShelfLife}-day shelf life: fresh {b.FreshLabel}, worth repacking {b.RepackLabel}, " +
                         $"ageing {b.AgeingLabel}, expiring soon {b.ExpiringLabel}, expired after {b.ShelfLife} days."));

        var sections = rows
            .GroupBy(r => r.LocationName)
            .Select(group => ReportSection.Create(
                group.Key,
                columns,
                group.Select(r => Row.Of(
                    ("product", $"{r.ProductName} ({r.UnitCode})"),
                    ("fresh", Show(r.Fresh)),
                    ("repack", Show(r.RepackWindow)),
                    ("ageing", Show(r.Ageing)),
                    ("expiring", Show(r.ExpiringSoon)),
                    ("expired", Show(r.Expired)),
                    ("total", r.Total),
                    ("oldest", r.OldestPackedOn))),
                bands.Length == 0 ? null : $"Age is counted from packing. {bands}"))
            .ToList();

        return new ReportDocument(
            "stock-age",
            "Stock age",
            $"As on {ReportPeriod.Describe(asOf, asOf)}",
            asOf,
            asOf,
            null,
            [
                new ReportFigure("Products to repack", rows.Count(r => r.RepackWindow > 0m), ReportColumnKind.Count),
                new ReportFigure($"Expiring within {AgeBands.SoonDays} days", rows.Count(r => r.ExpiringWithinDays > 0m), ReportColumnKind.Count),
                new ReportFigure("Products with expired stock", rows.Count(r => r.Expired > 0m), ReportColumnKind.Count)
            ],
            sections,
            DateTime.UtcNow);
    }
}
