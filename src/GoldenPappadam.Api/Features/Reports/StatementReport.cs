using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// A shop's account for a period, to hand over or send: what was owed at the start, every bill and
/// payment in between, and what is owed at the end. Built from the same ledger as the customer's
/// page, so the two can never disagree.
/// </summary>
public class StatementReport(AppDbContext db, CustomerService customers)
{
    public async Task<ReportDocument> BuildAsync(Guid customerId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException("Customer");

        var ledger = await customers.GetLedgerAsync(customerId, ct);

        // Everything before the period is summed into one "brought forward" line.
        var before = ledger.Where(e => e.Date < from).ToList();
        var during = ledger.Where(e => e.Date >= from && e.Date <= to).ToList();
        var broughtForward = before.Count > 0 ? before[^1].Balance : 0m;
        var closing = during.Count > 0 ? during[^1].Balance : broughtForward;

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            Row.Of(("date", from), ("type", "Brought forward"), ("reference", null),
                ("billed", null), ("paid", null), ("balance", broughtForward))
        };

        rows.AddRange(during.Select(e => Row.Of(
            ("date", e.Date),
            ("type", e.EntryType switch { "Invoice" => "Bill", "Opening" => "Opening balance", _ => e.EntryType }),
            ("reference", e.EntryType == "Payment" && e.Description is not null && e.Description != e.Reference
                ? $"{e.Reference} ({e.Description})"
                : e.Reference),
            ("billed", e.Billed == 0m ? null : e.Billed),
            ("paid", e.Paid == 0m ? null : e.Paid),
            ("balance", e.Balance))));

        var subtitle = string.Join("  ·  ", new[]
        {
            customer.Name,
            customer.Address,
            customer.Phone,
            customer.Gstin is null ? null : $"GSTIN {customer.Gstin}"
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new ReportDocument(
            "statement",
            "Statement of account",
            ReportPeriod.Describe(from, to),
            from,
            to,
            subtitle,
            [
                new ReportFigure("Brought forward", broughtForward, ReportColumnKind.Money),
                new ReportFigure("Billed", during.Sum(e => e.Billed), ReportColumnKind.Money),
                new ReportFigure("Paid", during.Sum(e => e.Paid), ReportColumnKind.Money),
                new ReportFigure("Owed at the end", closing, ReportColumnKind.Money)
            ],
            [
                new ReportSection(
                    "Account",
                    [
                        new ReportColumn("date", "Date", ReportColumnKind.Date),
                        new ReportColumn("type", "Entry", ReportColumnKind.Text),
                        new ReportColumn("reference", "Reference", ReportColumnKind.Text),
                        new ReportColumn("billed", "Billed", ReportColumnKind.Money),
                        new ReportColumn("paid", "Paid", ReportColumnKind.Money),
                        new ReportColumn("balance", "Balance", ReportColumnKind.Money)
                    ],
                    rows,
                    null)
            ],
            DateTime.UtcNow);
    }
}
