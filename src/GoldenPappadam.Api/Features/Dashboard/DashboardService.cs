using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Dashboard;

/// <summary>
/// The overview screen. Days and months are IST, so "today's sales" means the business day,
/// not the UTC one.
/// </summary>
public class DashboardService(AppDbContext db, StockService stock)
{
    private const int ListSize = 5;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var today = IndiaTime.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var issued = db.Invoices.Where(i => i.Status == InvoiceStatus.Issued);

        var todaySales = await issued.Where(i => i.InvoiceDate == today)
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m;
        var todayCount = await issued.CountAsync(i => i.InvoiceDate == today, ct);

        var monthSales = await issued.Where(i => i.InvoiceDate >= monthStart)
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m;
        var monthCount = await issued.CountAsync(i => i.InvoiceDate >= monthStart, ct);

        var activeCustomers = await db.Customers.CountAsync(c => c.IsActive, ct);

        var balances = await CustomerQueries.Project(db.Customers.Where(c => c.IsActive), db).ToListAsync(ct);
        var owing = balances.Where(c => c.Balance > 0m).ToList();

        var recentInvoices = await InvoiceQueries.ProjectList(
                db.Invoices.OrderByDescending(i => i.CreatedAt).Take(ListSize),
                db)
            .ToListAsync(ct);

        // The warehouse: that is what needs restocking, and van stock is already spoken for.
        var lowStock = await stock.GetOnHandAsync(
            null, lowStockOnly: true, includeInactive: false, KnownStockLocations.MainWarehouseId, ct);

        return new DashboardSummaryDto(
            today,
            todaySales,
            todayCount,
            monthSales,
            monthCount,
            activeCustomers,
            owing.Sum(c => c.Balance),
            lowStock.Count,
            recentInvoices,
            owing.OrderByDescending(c => c.Balance)
                .Take(ListSize)
                .Select(c => new CustomerBalanceDto(c.Id, c.Name, c.Balance))
                .ToList(),
            lowStock.Take(ListSize).ToList());
    }

    /// <summary>
    /// Groups invoice lines by product for a range of business days. Cancelled bills are left
    /// out, the same way the sales totals leave them out. The product's current name, unit and
    /// category are used so a rename does not split one product across two bars.
    /// </summary>
    public async Task<IReadOnlyList<ProductSalesDto>> GetProductSalesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        // Group on the product's own columns rather than joining a grouped subquery back to
        // Products, which EF cannot translate. Ordering happens in memory because the rows are
        // already projected into a record, and there is one row per product at most.
        var sold = await db.InvoiceLines
            .Where(l => l.Invoice!.Status == InvoiceStatus.Issued
                        && l.Invoice.InvoiceDate >= from
                        && l.Invoice.InvoiceDate <= to)
            .GroupBy(l => new
            {
                l.ProductId,
                ProductName = l.Product!.Name,
                UnitCode = l.Product.UnitOfMeasure!.Code,
                CategoryName = l.Product.Category!.Name
            })
            .Select(g => new ProductSalesDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.UnitCode,
                g.Key.CategoryName,
                g.Sum(l => l.Quantity),
                g.Sum(l => l.LineTotal)))
            .ToListAsync(ct);

        return sold.OrderByDescending(p => p.SalesValue).ToList();
    }
}
