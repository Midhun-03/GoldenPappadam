using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.FieldSales.Day;

/// <summary>
/// RecordedAt is when the salesperson saved it on the phone; ReceivedAt is when it reached the
/// server. The gap between them is the visible shape of a phone that was out of signal.
/// </summary>
public record FieldSaleRowDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Products,
    decimal TotalAmount,
    decimal AmountPaid,
    string Salesperson,
    string DeviceName,
    DateTime RecordedAt,
    DateTime ReceivedAt,
    bool PriceMismatch,
    InvoiceStatus Status);

public record FieldVisitRowDto(
    Guid CustomerId,
    string CustomerName,
    VisitOutcome Outcome,
    DateTime VisitedAt,
    string Salesperson,
    string? Notes);

public record FieldSalesDayDto(
    DateOnly BusinessDate,
    decimal TotalSales,
    int SaleCount,
    int ShopsVisited,
    decimal CashCollected,
    decimal CreditSales,
    decimal OutstandingCreatedToday,
    int PriceMismatchCount,
    /// <summary>
    /// The longest gap between recording and arrival among what has come in. Anything still on a
    /// phone with no signal cannot appear here at all - that is the point of showing this.
    /// </summary>
    int SlowestSyncMinutes,
    IReadOnlyList<FieldSaleRowDto> Sales,
    IReadOnlyList<FieldVisitRowDto> Visits);

/// <summary>
/// What the salesperson did today, as the office sees it. Everything here comes from what has
/// actually synchronised.
/// </summary>
public class FieldSalesDayService(AppDbContext db)
{
    public async Task<FieldSalesDayDto> GetAsync(DateOnly businessDate, CancellationToken ct)
    {
        var (dayStart, dayEnd) = IndiaTime.DayRangeUtc(businessDate);

        // The line descriptions come back as a list and are joined in memory: SQL has no
        // string.Join, and one row per bill is a handful of strings either way.
        var rows = await db.SyncSubmissions
            .Where(s => s.SubmissionType == SubmissionType.Invoice)
            .Where(s => s.RecordedAt >= dayStart && s.RecordedAt < dayEnd)
            .Join(db.Invoices, s => s.CreatedRecordId, i => i.Id, (s, i) => new { Submission = s, Invoice = i })
            .OrderByDescending(x => x.Submission.RecordedAt)
            .Select(x => new
            {
                x.Invoice.Id,
                x.Invoice.InvoiceNumber,
                x.Invoice.CustomerId,
                CustomerName = x.Invoice.Customer!.Name,
                Products = x.Invoice.Lines.Select(l => l.Quantity + " " + l.Description).ToList(),
                x.Invoice.TotalAmount,
                AmountPaid = db.PaymentAllocations
                    .Where(a => a.InvoiceId == x.Invoice.Id)
                    .Sum(a => (decimal?)a.Amount) ?? 0m,
                Salesperson = db.Users
                    .Where(u => u.Id == x.Submission.Device!.UserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
                DeviceName = x.Submission.Device!.Name,
                x.Submission.RecordedAt,
                x.Submission.ReceivedAt,
                x.Submission.PriceMismatch,
                x.Invoice.Status
            })
            .ToListAsync(ct);

        var sales = rows
            .Select(r => new FieldSaleRowDto(
                r.Id,
                r.InvoiceNumber,
                r.CustomerId,
                r.CustomerName,
                string.Join(", ", r.Products),
                r.TotalAmount,
                r.AmountPaid,
                r.Salesperson ?? "Unknown",
                r.DeviceName,
                r.RecordedAt,
                r.ReceivedAt,
                r.PriceMismatch,
                r.Status))
            .ToList();

        var visits = await db.ShopVisits
            .Where(v => v.VisitedAt >= dayStart && v.VisitedAt < dayEnd)
            .OrderByDescending(v => v.VisitedAt)
            .Select(v => new FieldVisitRowDto(
                v.CustomerId,
                v.Customer!.Name,
                v.Outcome,
                v.VisitedAt,
                db.Users.Where(u => u.Id == v.Device!.UserId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                v.Notes))
            .ToListAsync(ct);

        var cashCollected = await db.SyncSubmissions
            .Where(s => s.SubmissionType == SubmissionType.Payment)
            .Where(s => s.RecordedAt >= dayStart && s.RecordedAt < dayEnd)
            .Join(db.Payments, s => s.CreatedRecordId, p => p.Id, (_, p) => p.Amount)
            .SumAsync(amount => (decimal?)amount, ct) ?? 0m;

        var issued = sales.Where(s => s.Status == InvoiceStatus.Issued).ToList();

        // A shop counts as visited whether or not it bought anything - which is the number the
        // business has never been able to see before.
        var shopsVisited = visits.Select(v => v.CustomerId)
            .Concat(issued.Select(s => s.CustomerId))
            .Distinct()
            .Count();

        return new FieldSalesDayDto(
            businessDate,
            issued.Sum(s => s.TotalAmount),
            issued.Count,
            shopsVisited,
            cashCollected,
            // Sold on credit: the value of bills that were not settled on the spot.
            issued.Where(s => s.AmountPaid < s.TotalAmount).Sum(s => s.TotalAmount),
            // What the shops still owe from today's bills.
            issued.Sum(s => s.TotalAmount - s.AmountPaid),
            issued.Count(s => s.PriceMismatch),
            sales.Count == 0 ? 0 : (int)sales.Max(s => (s.ReceivedAt - s.RecordedAt).TotalMinutes),
            sales,
            visits);
    }
}
