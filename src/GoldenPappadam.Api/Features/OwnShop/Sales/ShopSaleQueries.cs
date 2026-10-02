using GoldenPappadam.Domain.OwnShop;
using GoldenPappadam.Infrastructure.Persistence;

namespace GoldenPappadam.Api.Features.OwnShop.Sales;

/// <summary>
/// Shapes shop sales into DTOs. Filter and order the query first: SQL Server cannot order by an
/// already-projected record.
/// </summary>
public static class ShopSaleQueries
{
    public static IQueryable<ShopSaleListItemDto> ProjectList(IQueryable<ShopSale> sales) =>
        sales.Select(s => new ShopSaleListItemDto(
            s.Id,
            s.SaleNumber,
            s.SaleDate,
            s.CreatedAt,
            s.CustomerId,
            s.CustomerName,
            s.PaymentMethod,
            s.Lines.Sum(l => l.Quantity),
            s.TotalAmount,
            s.Status,
            s.Lines.Any(l => l.UnitPrice < l.DefaultPrice)));

    public static IQueryable<ShopSaleDetailDto> ProjectDetail(IQueryable<ShopSale> sales, AppDbContext db) =>
        sales.Select(s => new ShopSaleDetailDto(
            s.Id,
            s.SaleNumber,
            s.SaleDate,
            s.CreatedAt,
            s.Location!.Name,
            s.CustomerId,
            s.CustomerName,
            s.PaymentMethod,
            s.TotalAmount,
            s.Status,
            s.Notes,
            db.Users.Where(u => u.Id == s.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            s.CancelledAt,
            s.CancellationReason,
            s.Status == ShopSaleStatus.Cancelled
                ? db.Users.Where(u => u.Id == s.UpdatedBy).Select(u => u.FullName).FirstOrDefault()
                : null,
            s.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new ShopSaleLineDto(
                    l.LineNumber,
                    l.ProductId,
                    l.Description,
                    l.Quantity,
                    l.UnitPrice,
                    l.DefaultPrice,
                    l.MinimumPrice,
                    l.LineTotal))
                .ToList()));
}
