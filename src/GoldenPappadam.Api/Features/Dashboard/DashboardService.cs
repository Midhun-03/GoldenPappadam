using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.Customers;
using GoldenPappadam.Api.Features.Sales.Invoices;
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

        var lowStock = await stock.GetOnHandAsync(null, lowStockOnly: true, includeInactive: false, ct);

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
}
