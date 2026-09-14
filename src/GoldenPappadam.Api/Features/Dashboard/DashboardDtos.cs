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
