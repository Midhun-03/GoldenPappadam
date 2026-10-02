using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.OwnShop.Stock;

/// <summary>
/// What the own shop has to sell: each pieces product with its pieces on the shelf, its rate band, and
/// the factory's loose kg it is sent from. Admin-only through the fallback policy.
/// </summary>
[ApiController]
[Route("api/own-shop/stock")]
public class ShopStockController(AppDbContext db, CustomerPriceService prices) : ControllerBase
{
    /// <summary>
    /// customerId fills Rate with that customer's agreed rate per piece where there is one - what a sale
    /// to them starts at. Without it, Rate is the standard rate.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<ShopStockDto>> Get(Guid? customerId = null, bool includeInactive = false, CancellationToken ct = default)
    {
        var rows = await db.Products
            .Where(p => p.Kind == ProductKind.Pieces && (includeInactive || p.IsActive))
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.Name,
                SourceProductId = p.SourceProductId!.Value,
                SourceProductName = p.SourceProduct!.Name,
                p.SourceProduct.PiecesPerKg,
                ShopPieces = db.StockMovements
                    .Where(m => m.ProductId == p.Id && m.LocationId == KnownStockLocations.OwnShopId)
                    .Sum(m => (decimal?)m.Quantity) ?? 0m,
                FactoryKg = db.StockMovements
                    .Where(m => m.ProductId == p.SourceProductId && m.LocationId == KnownStockLocations.MainWarehouseId)
                    .Sum(m => (decimal?)m.Quantity) ?? 0m,
                p.SellingPrice,
                p.MinimumSellingPrice,
                p.LowStockThreshold,
                p.IsActive
            })
            .ToListAsync(ct);

        var agreed = customerId is { } id
            ? await prices.GetAgreedPricesAsync(id, rows.Select(r => r.Id), ct)
            : [];

        return rows
            .Select(r => new ShopStockDto(
                r.Id,
                r.ProductCode,
                r.Name,
                r.SourceProductId,
                r.SourceProductName,
                r.PiecesPerKg,
                r.ShopPieces,
                r.FactoryKg,
                r.SellingPrice,
                r.SellingPrice is { } rate ? ShopRates.MinimumOf(rate, r.MinimumSellingPrice) : null,
                CustomerPriceService.Resolve(null, agreed.TryGetValue(r.Id, out var a) ? a : null, r.SellingPrice),
                r.LowStockThreshold,
                r.LowStockThreshold != null && r.ShopPieces <= r.LowStockThreshold,
                r.IsActive))
            .ToList();
    }
}
