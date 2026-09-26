using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// Every bill in the period, then the same sales added up by product, by shop and by who billed
/// it. Names are the ones printed on each bill. Cancelled bills are listed on their own and left
/// out of every total.
/// </summary>
public class SalesReport(AppDbContext db)
{
    public async Task<ReportDocument> BuildAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var bills = await db.Invoices
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.InvoiceDate,
                i.InvoiceNumber,
                i.CustomerName,
                i.BranchName,
                i.DocumentType,
                i.Status,
                i.TotalAmount,
                i.RoundOff,
                i.CancellationReason,
                Paid = db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m,
                By = db.Users.Where(u => u.Id == i.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                FromPhone = db.SyncSubmissions.Any(s =>
                    s.SubmissionType == SubmissionType.Invoice && s.CreatedRecordId == i.Id)
            })
            .ToListAsync(ct);

        var issued = bills.Where(b => b.Status == InvoiceStatus.Issued).ToList();
        var cancelled = bills.Where(b => b.Status == InvoiceStatus.Cancelled).ToList();

        var byProduct = await db.InvoiceLines
            .Where(l => l.Invoice!.Status == InvoiceStatus.Issued &&
                        l.Invoice.InvoiceDate >= from && l.Invoice.InvoiceDate <= to)
            .GroupBy(l => new { l.ProductId, l.UnitCode })
            .Select(g => new
            {
                Name = db.Products.Where(p => p.Id == g.Key.ProductId).Select(p => p.Name).FirstOrDefault(),
                g.Key.UnitCode,
                Quantity = g.Sum(l => l.Quantity),
                Value = g.Sum(l => l.TaxableValue + l.CgstAmount + l.SgstAmount + l.IgstAmount + l.CessAmount)
            })
            .OrderByDescending(p => p.Value)
            .ToListAsync(ct);

        string Channel(bool fromPhone) => fromPhone ? "Phone" : "Office";

        // Product values leave out rounding to the rupee, so they should equal the bills less their
        // round-off. Bills made before 23 Sep 2026 kept their discount on the bill instead of sharing
        // it across the lines, so for those the products come to a little more; say how much.
        var productGap = byProduct.Sum(p => p.Value) - (issued.Sum(b => b.TotalAmount) - issued.Sum(b => b.RoundOff));
        var productNote = productGap == 0m
            ? "Value is after discount and includes any tax; it leaves out rounding to the rupee."
            : $"Value is after discount and includes any tax, leaving out rounding. Older bills kept their discount on " +
              $"the bill rather than its lines, so these values come to {Common.PdfStyle.Money(productGap)} more than the bills.";

        var billColumns = new[]
        {
            new ReportColumn("date", "Date", ReportColumnKind.Date),
            new ReportColumn("number", "Bill", ReportColumnKind.Text),
            new ReportColumn("customer", "Shop", ReportColumnKind.Text),
            new ReportColumn("branch", "Branch", ReportColumnKind.Text),
            new ReportColumn("kind", "Bill type", ReportColumnKind.Text),
            new ReportColumn("by", "Billed by", ReportColumnKind.Text),
            new ReportColumn("total", "Total", ReportColumnKind.Money, Total: true),
            new ReportColumn("paid", "Paid so far", ReportColumnKind.Money, Total: true),
            new ReportColumn("due", "Still due", ReportColumnKind.Money, Total: true)
        };

        var sections = new List<ReportSection>
        {
            ReportSection.Create("Bills", billColumns, issued.Select(b => Row.Of(
                ("date", b.InvoiceDate),
                ("number", b.InvoiceNumber),
                ("customer", b.CustomerName),
                ("branch", b.BranchName),
                ("kind", b.DocumentType == InvoiceDocumentType.Invoice ? "Normal" : "GST"),
                ("by", $"{b.By ?? "—"} ({Channel(b.FromPhone)})"),
                ("total", b.TotalAmount),
                ("paid", b.Paid),
                ("due", b.TotalAmount - b.Paid)))),

            ReportSection.Create(
                "By product",
                [
                    new ReportColumn("product", "Product", ReportColumnKind.Text),
                    new ReportColumn("unit", "Unit", ReportColumnKind.Text),
                    new ReportColumn("quantity", "Quantity", ReportColumnKind.Quantity),
                    new ReportColumn("value", "Value", ReportColumnKind.Money, Total: true)
                ],
                byProduct.Select(p => Row.Of(
                    ("product", p.Name), ("unit", p.UnitCode), ("quantity", p.Quantity), ("value", p.Value))),
                productNote),

            ReportSection.Create(
                "By shop",
                [
                    new ReportColumn("customer", "Shop", ReportColumnKind.Text),
                    new ReportColumn("bills", "Bills", ReportColumnKind.Count, Total: true),
                    new ReportColumn("total", "Sales", ReportColumnKind.Money, Total: true)
                ],
                issued.GroupBy(b => b.CustomerName)
                    .Select(g => new { g.Key, Count = g.Count(), Total = g.Sum(b => b.TotalAmount) })
                    .OrderByDescending(g => g.Total)
                    .Select(g => Row.Of(("customer", g.Key), ("bills", g.Count), ("total", g.Total)))),

            ReportSection.Create(
                "By who billed",
                [
                    new ReportColumn("by", "Billed by", ReportColumnKind.Text),
                    new ReportColumn("bills", "Bills", ReportColumnKind.Count, Total: true),
                    new ReportColumn("total", "Sales", ReportColumnKind.Money, Total: true)
                ],
                issued.GroupBy(b => $"{b.By ?? "—"} ({Channel(b.FromPhone)})")
                    .Select(g => new { g.Key, Count = g.Count(), Total = g.Sum(b => b.TotalAmount) })
                    .OrderByDescending(g => g.Total)
                    .Select(g => Row.Of(("by", g.Key), ("bills", g.Count), ("total", g.Total))))
        };

        if (cancelled.Count > 0)
        {
            sections.Add(ReportSection.Create(
                "Cancelled bills (not counted above)",
                [
                    new ReportColumn("date", "Date", ReportColumnKind.Date),
                    new ReportColumn("number", "Bill", ReportColumnKind.Text),
                    new ReportColumn("customer", "Shop", ReportColumnKind.Text),
                    new ReportColumn("reason", "Reason", ReportColumnKind.Text),
                    new ReportColumn("total", "Total", ReportColumnKind.Money)
                ],
                cancelled.Select(b => Row.Of(
                    ("date", b.InvoiceDate), ("number", b.InvoiceNumber), ("customer", b.CustomerName),
                    ("reason", b.CancellationReason), ("total", b.TotalAmount)))));
        }

        var sales = issued.Sum(b => b.TotalAmount);
        var paid = issued.Sum(b => b.Paid);

        return new ReportDocument(
            "sales",
            "Sales report",
            ReportPeriod.Describe(from, to),
            from,
            to,
            null,
            [
                new ReportFigure("Bills", issued.Count, ReportColumnKind.Count),
                new ReportFigure("Sales", sales, ReportColumnKind.Money),
                new ReportFigure("Paid so far", paid, ReportColumnKind.Money),
                new ReportFigure("Still due", sales - paid, ReportColumnKind.Money)
            ],
            sections,
            DateTime.UtcNow);
    }
}
