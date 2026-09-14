using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

public static class InvoiceQueries
{
    /// <summary>Filter and order the invoice query before calling this: SQL cannot order by a projected record.</summary>
    public static IQueryable<InvoiceListItemDto> ProjectList(IQueryable<Invoice> invoices, AppDbContext db) =>
        invoices.Select(i => new InvoiceListItemDto(
            i.Id,
            i.InvoiceNumber,
            i.CustomerId,
            i.Customer!.Name,
            i.InvoiceDate,
            i.Status,
            i.TotalAmount,
            db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m,
            i.Status == InvoiceStatus.Cancelled
                ? 0m
                : i.TotalAmount - (db.PaymentAllocations
                    .Where(a => a.InvoiceId == i.Id)
                    .Sum(a => (decimal?)a.Amount) ?? 0m)));

    public static IQueryable<InvoiceDetailDto> ProjectDetail(IQueryable<Invoice> invoices, AppDbContext db) =>
        invoices.Select(i => new InvoiceDetailDto(
            i.Id,
            i.InvoiceNumber,
            i.CustomerId,
            i.Customer!.Name,
            i.InvoiceDate,
            i.Status,
            i.SubTotal,
            i.DiscountAmount,
            i.TotalAmount,
            db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m,
            i.Status == InvoiceStatus.Cancelled
                ? 0m
                : i.TotalAmount - (db.PaymentAllocations
                    .Where(a => a.InvoiceId == i.Id)
                    .Sum(a => (decimal?)a.Amount) ?? 0m),
            i.Notes,
            i.CancelledAt,
            i.CancellationReason,
            i.Lines
                .OrderBy(l => l.CreatedAt)
                .Select(l => new InvoiceLineDto(
                    l.Id, l.ProductId, l.Description, l.UnitCode, l.Quantity, l.UnitPrice, l.LineTotal))
                .ToList()));
}
