using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// Money received in the period: each payment with the bills it settled, then added up by how it
/// was paid and by who took it.
/// </summary>
public class CollectionsReport(AppDbContext db)
{
    public async Task<ReportDocument> BuildAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var payments = await db.Payments
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.PaymentDate,
                Customer = p.Customer!.Name,
                p.Method,
                p.Reference,
                p.Amount,
                By = db.Users.Where(u => u.Id == p.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                FromPhone = db.SyncSubmissions.Any(s =>
                    s.SubmissionType == SubmissionType.Payment && s.CreatedRecordId == p.Id)
            })
            .ToListAsync(ct);

        var ids = payments.Select(p => p.Id).ToList();
        var allocations = await db.PaymentAllocations
            .Where(a => ids.Contains(a.PaymentId))
            .Select(a => new { a.PaymentId, a.Invoice!.InvoiceNumber, a.Amount })
            .ToListAsync(ct);
        var settled = allocations
            .GroupBy(a => a.PaymentId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(a => a.InvoiceNumber).Order()));

        string Who(string? name, bool fromPhone) => $"{name ?? "—"} ({(fromPhone ? "Phone" : "Office")})";
        static string Method(Domain.Sales.PaymentMethod method) =>
            method == Domain.Sales.PaymentMethod.BankTransfer ? "Bank transfer" : method.ToString();

        var total = payments.Sum(p => p.Amount);

        return new ReportDocument(
            "collections",
            "Collections report",
            ReportPeriod.Describe(from, to),
            from,
            to,
            null,
            [
                new ReportFigure("Payments", payments.Count, ReportColumnKind.Count),
                new ReportFigure("Collected", total, ReportColumnKind.Money),
                // Money taken but not yet matched to a bill still reduces what the shop owes.
                new ReportFigure("Kept on account", total - allocations.Sum(a => a.Amount), ReportColumnKind.Money)
            ],
            [
                ReportSection.Create(
                    "Payments",
                    [
                        new ReportColumn("date", "Date", ReportColumnKind.Date),
                        new ReportColumn("customer", "Shop", ReportColumnKind.Text),
                        new ReportColumn("method", "Method", ReportColumnKind.Text),
                        new ReportColumn("reference", "Reference", ReportColumnKind.Text),
                        new ReportColumn("by", "Received by", ReportColumnKind.Text),
                        new ReportColumn("bills", "Bills settled", ReportColumnKind.Text),
                        new ReportColumn("amount", "Amount", ReportColumnKind.Money, Total: true)
                    ],
                    payments.Select(p => Row.Of(
                        ("date", p.PaymentDate),
                        ("customer", p.Customer),
                        ("method", Method(p.Method)),
                        ("reference", p.Reference),
                        ("by", Who(p.By, p.FromPhone)),
                        ("bills", settled.GetValueOrDefault(p.Id, "On account")),
                        ("amount", p.Amount)))),

                ReportSection.Create(
                    "By method",
                    [
                        new ReportColumn("method", "Method", ReportColumnKind.Text),
                        new ReportColumn("count", "Payments", ReportColumnKind.Count, Total: true),
                        new ReportColumn("amount", "Amount", ReportColumnKind.Money, Total: true)
                    ],
                    payments.GroupBy(p => Method(p.Method))
                        .OrderByDescending(g => g.Sum(p => p.Amount))
                        .Select(g => Row.Of(("method", g.Key), ("count", g.Count()), ("amount", g.Sum(p => p.Amount))))),

                ReportSection.Create(
                    "By who received it",
                    [
                        new ReportColumn("by", "Received by", ReportColumnKind.Text),
                        new ReportColumn("count", "Payments", ReportColumnKind.Count, Total: true),
                        new ReportColumn("amount", "Amount", ReportColumnKind.Money, Total: true)
                    ],
                    payments.GroupBy(p => Who(p.By, p.FromPhone))
                        .OrderByDescending(g => g.Sum(p => p.Amount))
                        .Select(g => Row.Of(("by", g.Key), ("count", g.Count()), ("amount", g.Sum(p => p.Amount)))))
            ],
            DateTime.UtcNow);
    }
}
