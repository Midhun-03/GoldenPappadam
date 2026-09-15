using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.Invoices;

namespace GoldenPappadam.Api.Features.Dashboard;

public record CustomerBalanceDto(Guid CustomerId, string Name, decimal Balance);

public record DashboardSummaryDto(
    DateOnly Today,
    decimal TodaySales,
    int TodayInvoiceCount,
    decimal MonthSales,
    int MonthInvoiceCount,
    int ActiveCustomers,
    decimal OutstandingTotal,
    int LowStockCount,
    IReadOnlyList<InvoiceListItemDto> RecentInvoices,
    IReadOnlyList<CustomerBalanceDto> TopOutstanding,
    IReadOnlyList<StockOnHandDto> LowStockProducts);

/// <summary>
/// What one product sold over a date range. <see cref="SalesValue"/> is the sum of the line
/// totals, so it is gross of any bill-level discount and will not tie exactly to
/// <see cref="DashboardSummaryDto.MonthSales"/>. The charts label it accordingly.
/// </summary>
public record ProductSalesDto(
    Guid ProductId,
    string ProductName,
    string UnitCode,
    string CategoryName,
    decimal QuantitySold,
    decimal SalesValue);
