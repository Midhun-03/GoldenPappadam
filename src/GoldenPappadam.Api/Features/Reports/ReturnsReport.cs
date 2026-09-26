using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// What expiry and damage cost in the period: packets shops gave back, what each shop got for them,
/// and expired stock written off in our own warehouse and vans. Cancelled returns are left out.
/// </summary>
public class ReturnsReport(AppDbContext db)
{
    public async Task<ReportDocument> BuildAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var notes = await db.ReturnNotes
            .Where(r => r.ReturnDate >= from && r.ReturnDate <= to && r.Status == ReturnStatus.Recorded)
            .OrderBy(r => r.ReturnDate)
            .ThenBy(r => r.SequenceNumber)
            .Select(r => new
            {
                r.ReturnNumber,
                r.ReturnDate,
                Shop = r.BranchName == null ? r.CustomerName : r.CustomerName + " – " + r.BranchName,
                r.Settlement,
                r.Value,
                r.CreditAmount,
                Quantity = r.Lines.Sum(l => l.Quantity)
            })
            .ToListAsync(ct);

        var lines = await db.ReturnNoteLines
            .Where(l => db.ReturnNotes.Any(r => r.Id == l.ReturnNoteId && r.ReturnDate >= from && r.ReturnDate <= to &&
                                                r.Status == ReturnStatus.Recorded))
            .GroupBy(l => new { l.ProductId, l.Description, l.UnitCode, l.Reason })
            .Select(g => new
            {
                g.Key.Description,
                g.Key.UnitCode,
                g.Key.Reason,
                Quantity = g.Sum(l => l.Quantity),
                Value = g.Sum(l => l.Value)
            })
            .OrderBy(g => g.Description)
            .ThenBy(g => g.Reason)
            .ToListAsync(ct);

        var start = IndiaTime.DayRangeUtc(from).Start;
        var end = IndiaTime.DayRangeUtc(to).End;

        // Written off from the Stock age screen, whose notes always begin "Expired".
        var writtenOff = await db.StockMovements
            .Where(m => m.MovementType == StockMovementType.Damage && m.OccurredAt >= start && m.OccurredAt < end &&
                        m.Notes != null && m.Notes.StartsWith("Expired"))
            .GroupBy(m => new { Product = m.Product!.Name, Unit = m.Product.UnitOfMeasure!.Code, Location = m.Location!.Name })
            .Select(g => new { g.Key.Product, g.Key.Unit, g.Key.Location, Quantity = -g.Sum(m => m.Quantity) })
            .OrderBy(g => g.Location)
            .ThenBy(g => g.Product)
            .ToListAsync(ct);

        return new ReportDocument(
            "returns",
            "Returns and expiry",
            ReportPeriod.Describe(from, to),
            from,
            to,
            null,
            [
                new ReportFigure("Returns", notes.Count, ReportColumnKind.Count),
                new ReportFigure("Value returned", notes.Sum(n => n.Value), ReportColumnKind.Money),
                new ReportFigure("Credited to shops", notes.Sum(n => n.CreditAmount), ReportColumnKind.Money),
                new ReportFigure("Replaced free",
                    notes.Where(n => n.Settlement == ReturnSettlement.Replacement).Sum(n => n.Quantity), ReportColumnKind.Quantity),
                new ReportFigure("Office to decide",
                    notes.Count(n => n.Settlement == ReturnSettlement.Pending), ReportColumnKind.Count)
            ],
            [
                ReportSection.Create(
                    "Return notes",
                    [
                        new ReportColumn("number", "Return", ReportColumnKind.Text),
                        new ReportColumn("date", "Date", ReportColumnKind.Date),
                        new ReportColumn("shop", "Shop", ReportColumnKind.Text),
                        new ReportColumn("settlement", "Settled by", ReportColumnKind.Text),
                        new ReportColumn("quantity", "Quantity", ReportColumnKind.Quantity, Total: true),
                        new ReportColumn("value", "Value", ReportColumnKind.Money, Total: true),
                        new ReportColumn("credit", "Credit", ReportColumnKind.Money, Total: true)
                    ],
                    notes.Select(n => Row.Of(
                        ("number", n.ReturnNumber),
                        ("date", n.ReturnDate),
                        ("shop", n.Shop),
                        ("settlement", Settlement(n.Settlement)),
                        ("quantity", n.Quantity),
                        ("value", n.Value),
                        ("credit", n.CreditAmount)))),
                ReportSection.Create(
                    "By product and reason",
                    [
                        new ReportColumn("product", "Product", ReportColumnKind.Text),
                        new ReportColumn("reason", "Reason", ReportColumnKind.Text),
                        new ReportColumn("quantity", "Quantity", ReportColumnKind.Quantity),
                        new ReportColumn("value", "Value", ReportColumnKind.Money, Total: true)
                    ],
                    lines.Select(l => Row.Of(
                        ("product", $"{l.Description} ({l.UnitCode})"),
                        ("reason", l.Reason.ToString()),
                        ("quantity", l.Quantity),
                        ("value", l.Value)))),
                ReportSection.Create(
                    "By shop",
                    [
                        new ReportColumn("shop", "Shop", ReportColumnKind.Text),
                        new ReportColumn("count", "Returns", ReportColumnKind.Count, Total: true),
                        new ReportColumn("value", "Value", ReportColumnKind.Money, Total: true),
                        new ReportColumn("credit", "Credit", ReportColumnKind.Money, Total: true)
                    ],
                    notes.GroupBy(n => n.Shop)
                        .OrderByDescending(g => g.Sum(n => n.Value))
                        .Select(g => Row.Of(
                            ("shop", g.Key),
                            ("count", g.Count()),
                            ("value", g.Sum(n => n.Value)),
                            ("credit", g.Sum(n => n.CreditAmount))))),
                ReportSection.Create(
                    "Expired stock written off",
                    [
                        new ReportColumn("location", "Place", ReportColumnKind.Text),
                        new ReportColumn("product", "Product", ReportColumnKind.Text),
                        new ReportColumn("quantity", "Quantity", ReportColumnKind.Quantity)
                    ],
                    writtenOff.Select(w => Row.Of(
                        ("location", w.Location),
                        ("product", $"{w.Product} ({w.Unit})"),
                        ("quantity", w.Quantity))),
                    "Our own stock that expired before it was sold, written off from the Stock age screen.")
            ],
            DateTime.UtcNow);
    }

    public static string Settlement(ReturnSettlement settlement) => settlement switch
    {
        ReturnSettlement.Replacement => "Replaced free",
        ReturnSettlement.Credit => "Credit",
        ReturnSettlement.NoCompensation => "Nothing given",
        _ => "Office to decide"
    };
}
