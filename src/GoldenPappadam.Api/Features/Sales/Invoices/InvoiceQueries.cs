using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>
/// Names shown for an invoice are the ones printed on it (its snapshot), not the customer's
/// current name, so an old bill reads the same after a shop is renamed.
/// </summary>
public static class InvoiceQueries
{
    /// <summary>Filter and order the invoice query before calling this: SQL cannot order by a projected record.</summary>
    public static IQueryable<InvoiceListItemDto> ProjectList(IQueryable<Invoice> invoices, AppDbContext db) =>
        invoices.Select(i => new InvoiceListItemDto(
            i.Id,
            i.InvoiceNumber,
            i.CustomerId,
            i.CustomerName,
            i.BranchId,
            i.BranchName,
            i.InvoiceDate,
            i.Status,
            i.TotalAmount,
            db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m,
            i.Status == InvoiceStatus.Cancelled
                ? 0m
                : i.TotalAmount - (db.PaymentAllocations
                    .Where(a => a.InvoiceId == i.Id)
                    .Sum(a => (decimal?)a.Amount) ?? 0m),
            i.DocumentType,
            db.InvoiceDocuments.Any(d => d.InvoiceId == i.Id),
            db.InvoiceEmailLogs
                .Where(l => l.InvoiceId == i.Id)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => (InvoiceEmailStatus?)l.Status)
                .FirstOrDefault()));

    public static IQueryable<InvoiceDetailDto> ProjectDetail(IQueryable<Invoice> invoices, AppDbContext db) =>
        invoices.Select(i => new InvoiceDetailDto(
            i.Id,
            i.InvoiceNumber,
            i.SeriesCode,
            i.FinancialYear,
            i.DocumentType,
            i.CustomerId,
            i.CustomerName,
            i.BranchId,
            i.BranchName,
            i.InvoiceDate,
            i.Status,
            new InvoicePartyDto(i.SupplierName, i.SupplierAddress, null, i.SupplierGstin, i.SupplierStateCode),
            new InvoicePartyDto(i.CustomerName, i.CustomerAddress, i.CustomerPhone, i.CustomerGstin, i.CustomerStateCode),
            i.BranchName == null
                ? null
                : new InvoicePartyDto(i.BranchName, i.BranchAddress, i.BranchPhone, i.BranchGstin, i.BranchStateCode),
            i.PlaceOfSupplyStateCode,
            i.IsInterState,
            i.ReverseCharge,
            i.PricesIncludeTax,
            i.SubTotal,
            i.DiscountAmount,
            i.TaxableAmount,
            i.CgstAmount,
            i.SgstAmount,
            i.IgstAmount,
            i.CessAmount,
            i.RoundOff,
            i.TotalAmount,
            db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m,
            i.Status == InvoiceStatus.Cancelled
                ? 0m
                : i.TotalAmount - (db.PaymentAllocations
                    .Where(a => a.InvoiceId == i.Id)
                    .Sum(a => (decimal?)a.Amount) ?? 0m),
            i.Notes,
            i.CreatedAt,
            db.Users.Where(u => u.Id == i.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            db.SyncSubmissions
                .Where(s => s.SubmissionType == SubmissionType.Invoice && s.CreatedRecordId == i.Id)
                .Select(s => s.Device!.Name)
                .FirstOrDefault(),
            i.CancelledAt,
            i.Status == InvoiceStatus.Cancelled
                ? db.Users.Where(u => u.Id == i.UpdatedBy).Select(u => u.FullName).FirstOrDefault()
                : null,
            i.CancellationReason,
            db.InvoiceDocuments
                .Where(d => d.InvoiceId == i.Id)
                .Select(d => new InvoiceDocumentInfoDto(d.FileName, d.SizeBytes, d.Sha256, d.CreatedAt))
                .FirstOrDefault(),
            i.Customer!.Email,
            i.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new InvoiceLineDto(
                    l.Id,
                    l.ProductId,
                    l.Description,
                    l.UnitCode,
                    l.HsnCode,
                    l.Quantity,
                    l.UnitPrice,
                    l.LineTotal,
                    l.DiscountAmount,
                    l.TaxTreatment,
                    l.GstRate,
                    l.TaxableValue,
                    l.CgstAmount,
                    l.SgstAmount,
                    l.IgstAmount,
                    l.CessAmount,
                    l.TaxableValue + l.CgstAmount + l.SgstAmount + l.IgstAmount + l.CessAmount))
                .ToList()));
}
