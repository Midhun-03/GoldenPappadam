using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Reports;

/// <summary>
/// Who owes what on a given day, and for how long. Each shop's unpaid bills are placed by age, so
/// the money that has been out longest is the first thing seen.
///
/// Money received but never matched to a bill (paid "on account") is applied here the way a person
/// would: to the before-system balance first, then to the oldest unpaid bills. Otherwise a shop that
/// paid its opening balance in cash would show that balance as still owing, and a bill made after an
/// advance payment would look overdue although the money was already in hand.
///
/// Every row adds up: what remains of the before-system balance + the aged bills - money paid ahead
/// = what the shop owes, the same balance its account statement shows. Only what had happened by the
/// chosen day counts, so an old day's report reads as it would have that evening.
/// </summary>
public class OutstandingReport(AppDbContext db)
{
    /// <summary>Upper bound in days for each age band; the last band takes everything older.</summary>
    private static readonly (string Key, string Title, int UpTo)[] Bands =
    [
        ("d0", "0–7 days", 7),
        ("d8", "8–15 days", 15),
        ("d16", "16–30 days", 30),
        ("d31", "31–60 days", 60),
        ("d61", "Over 60 days", int.MaxValue)
    ];

    public async Task<ReportDocument> BuildAsync(DateOnly asOf, CancellationToken ct)
    {
        var customers = await db.Customers
            .Select(c => new { c.Id, c.Name, c.Phone, c.OpeningBalance })
            .ToListAsync(ct);

        var bills = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Issued && i.InvoiceDate <= asOf)
            .Select(i => new
            {
                i.CustomerId,
                i.InvoiceDate,
                i.TotalAmount,
                Paid = db.PaymentAllocations
                    .Where(a => a.InvoiceId == i.Id && a.Payment!.PaymentDate <= asOf)
                    .Sum(a => (decimal?)a.Amount) ?? 0m
            })
            .ToListAsync(ct);

        var payments = await db.Payments
            .Where(p => p.PaymentDate <= asOf)
            .Select(p => new
            {
                p.CustomerId,
                p.Amount,
                Allocated = p.Allocations.Sum(a => (decimal?)a.Amount) ?? 0m
            })
            .ToListAsync(ct);

        var billsByCustomer = bills.ToLookup(b => b.CustomerId);
        var paymentsByCustomer = payments.ToLookup(p => p.CustomerId);

        var rows = new List<IReadOnlyDictionary<string, object?>>();

        foreach (var customer in customers.OrderBy(c => c.Name))
        {
            var onAccount = paymentsByCustomer[customer.Id].Sum(p => p.Amount - p.Allocated);
            var balance = customer.OpeningBalance
                          + billsByCustomer[customer.Id].Sum(b => b.TotalAmount - b.Paid)
                          - onAccount;

            // The before-system balance is the oldest debt, so unmatched money pays it first...
            var opening = customer.OpeningBalance;
            var spare = onAccount;
            var toOpening = Math.Clamp(spare, 0m, Math.Max(opening, 0m));
            opening -= toOpening;
            spare -= toOpening;

            // ...then the bills, oldest first.
            var unpaid = new List<(DateOnly InvoiceDate, decimal Due)>();
            foreach (var bill in billsByCustomer[customer.Id].OrderBy(b => b.InvoiceDate))
            {
                var due = bill.TotalAmount - bill.Paid;
                var covered = Math.Clamp(spare, 0m, Math.Max(due, 0m));
                spare -= covered;

                if (due - covered > 0m)
                {
                    unpaid.Add((bill.InvoiceDate, due - covered));
                }
            }

            if (balance == 0m && unpaid.Count == 0 && opening == 0m)
            {
                continue;
            }

            var cells = new List<(string, object?)>
            {
                ("customer", customer.Name),
                ("phone", customer.Phone),
                ("opening", opening == 0m ? null : opening)
            };

            var lower = 0;
            foreach (var band in Bands)
            {
                var inBand = unpaid
                    .Where(b => asOf.DayNumber - b.InvoiceDate.DayNumber >= lower &&
                                asOf.DayNumber - b.InvoiceDate.DayNumber <= band.UpTo)
                    .Sum(b => b.Due);
                cells.Add((band.Key, inBand == 0m ? null : inBand));
                lower = band.UpTo == int.MaxValue ? lower : band.UpTo + 1;
            }

            cells.Add(("onAccount", spare == 0m ? null : -spare));
            cells.Add(("balance", balance));
            cells.Add(("oldest", unpaid.Count == 0 ? null : unpaid.Min(b => b.InvoiceDate)));

            rows.Add(Row.Of([.. cells]));
        }

        rows = rows.OrderByDescending(r => (decimal)r["balance"]!).ToList();

        var columns = new List<ReportColumn>
        {
            new("customer", "Shop", ReportColumnKind.Text),
            new("phone", "Phone", ReportColumnKind.Text),
            new("opening", "Before system", ReportColumnKind.Money, Total: true)
        };
        columns.AddRange(Bands.Select(b => new ReportColumn(b.Key, b.Title, ReportColumnKind.Money, Total: true)));
        columns.Add(new ReportColumn("onAccount", "Paid ahead", ReportColumnKind.Money, Total: true));
        columns.Add(new ReportColumn("balance", "Owes", ReportColumnKind.Money, Total: true));
        columns.Add(new ReportColumn("oldest", "Oldest unpaid bill", ReportColumnKind.Date));

        decimal Sum(string key) => rows.Sum(r => r.TryGetValue(key, out var v) && v is decimal d ? d : 0m);

        var owed = rows.Where(r => (decimal)r["balance"]! > 0m).ToList();

        return new ReportDocument(
            "outstanding",
            "Outstanding credit",
            $"As on {ReportPeriod.Describe(asOf, asOf)}",
            asOf,
            asOf,
            null,
            [
                new ReportFigure("Total owed", owed.Sum(r => (decimal)r["balance"]!), ReportColumnKind.Money),
                new ReportFigure("Shops owing", owed.Count, ReportColumnKind.Count),
                new ReportFigure("Over 30 days", Sum("d31") + Sum("d61"), ReportColumnKind.Money)
            ],
            [
                ReportSection.Create(
                    "By shop",
                    columns,
                    rows,
                    "Age is counted from each bill's date. Money received without naming a bill is set against the before-system balance first, then the oldest bills; \"Paid ahead\" is what is left over.")
            ],
            DateTime.UtcNow);
    }
}
