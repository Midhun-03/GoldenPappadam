using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;

namespace GoldenPappadam.Api.Features.Sales.RateRequests;

/// <summary>Filter and order before projecting: SQL cannot order by a projected record.</summary>
public static class RateRequestQueries
{
    public static IQueryable<RateRequestDto> Project(IQueryable<CustomerRateRequest> requests, AppDbContext db) =>
        requests.Select(r => new RateRequestDto(
            r.Id,
            r.CustomerId,
            r.Customer!.Name,
            r.ProductId,
            r.Product!.Name,
            r.Product.UnitOfMeasure!.Code,
            r.PriceWhenRequested,
            db.CustomerPrices
                .Where(cp => cp.CustomerId == r.CustomerId && cp.ProductId == r.ProductId && cp.IsActive)
                .Select(cp => (decimal?)cp.UnitPrice)
                .FirstOrDefault(),
            r.Product.SellingPrice,
            r.RequestedPrice,
            r.Reason,
            r.RequestedAt,
            db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
            r.Status,
            r.DecidedAt,
            db.Users.Where(u => u.Id == r.DecidedBy).Select(u => u.FullName).FirstOrDefault(),
            r.DecisionNote));
}
