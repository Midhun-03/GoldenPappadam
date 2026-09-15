using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.CustomerPrices;

/// <summary>
/// What each shop pays. The one rule worth stating plainly lives in <see cref="ResolveAsync"/>:
/// an explicit price on the bill line wins, then the shop's agreed price, then the product's own
/// selling price. Only the office can reach the first two.
/// </summary>
public class CustomerPriceService(AppDbContext db)
{
    /// <summary>
    /// The price list for one shop: every product it could be sold, with the agreed price where
    /// there is one. Inactive products are left out, because a bill cannot use them anyway.
    /// </summary>
    public async Task<IReadOnlyList<CustomerPriceDto>> GetForCustomerAsync(
        Guid customerId,
        bool agreedOnly,
        CancellationToken ct)
    {
        await EnsureCustomerExistsAsync(customerId, ct);

        var rows = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.Name,
                UnitCode = p.UnitOfMeasure!.Code,
                p.SellingPrice,
                AgreedPrice = db.CustomerPrices
                    .Where(cp => cp.CustomerId == customerId && cp.ProductId == p.Id && cp.IsActive)
                    .Select(cp => (decimal?)cp.UnitPrice)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows
            .Where(r => !agreedOnly || r.AgreedPrice != null)
            .Select(r => new CustomerPriceDto(
                r.Id,
                r.ProductCode,
                r.Name,
                r.UnitCode,
                r.SellingPrice,
                r.AgreedPrice,
                r.AgreedPrice ?? r.SellingPrice))
            .ToList();
    }

    /// <summary>
    /// Sets or changes what this shop pays. An existing arrangement is updated rather than
    /// duplicated, and one that was removed earlier comes back rather than colliding with the
    /// unique index.
    /// </summary>
    public async Task<CustomerPriceDto> SetAsync(
        Guid customerId,
        Guid productId,
        decimal unitPrice,
        CancellationToken ct)
    {
        if (unitPrice < 0m)
        {
            throw new DomainException("A price cannot be negative.");
        }

        var customer = await EnsureCustomerExistsAsync(customerId, ct);

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product");

        if (!product.IsActive)
        {
            throw new DomainException($"Product '{product.Name}' is not active.");
        }

        var existing = await db.CustomerPrices
            .FirstOrDefaultAsync(cp => cp.CustomerId == customerId && cp.ProductId == productId, ct);

        if (existing is null)
        {
            db.CustomerPrices.Add(new CustomerPrice
            {
                CustomerId = customerId,
                ProductId = productId,
                UnitPrice = unitPrice
            });
        }
        else
        {
            existing.UnitPrice = unitPrice;
            existing.IsActive = true;
        }

        await db.SaveChangesAsync(ct);

        return new CustomerPriceDto(
            product.Id,
            product.ProductCode,
            product.Name,
            await UnitCodeAsync(product.UnitOfMeasureId, ct),
            product.SellingPrice,
            unitPrice,
            unitPrice);
    }

    /// <summary>
    /// Ends the special arrangement, so the shop goes back to paying the product's own price.
    /// The row is deactivated rather than deleted, so the history of the arrangement survives.
    /// </summary>
    public async Task RemoveAsync(Guid customerId, Guid productId, CancellationToken ct)
    {
        var price = await db.CustomerPrices
                        .FirstOrDefaultAsync(cp => cp.CustomerId == customerId && cp.ProductId == productId, ct)
                    ?? throw new NotFoundException("Customer price");

        if (!price.IsActive)
        {
            throw new DomainException("This shop already pays the standard price for that product.");
        }

        price.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The agreed prices for a set of products, for billing. One query rather than one per line.
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> GetAgreedPricesAsync(
        Guid customerId,
        IEnumerable<Guid> productIds,
        CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();

        return await db.CustomerPrices
            .Where(cp => cp.CustomerId == customerId && cp.IsActive && ids.Contains(cp.ProductId))
            .ToDictionaryAsync(cp => cp.ProductId, cp => cp.UnitPrice, ct);
    }

    /// <summary>
    /// The price precedence, in one place: what the office typed on the line, else what this shop
    /// has agreed, else what the product normally sells for. Null means nobody has ever said what
    /// this product costs, which is a question for a person rather than a default.
    /// </summary>
    public static decimal? Resolve(decimal? lineOverride, decimal? agreedPrice, decimal? productPrice) =>
        lineOverride ?? agreedPrice ?? productPrice;

    private async Task<Customer> EnsureCustomerExistsAsync(Guid customerId, CancellationToken ct) =>
        await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
        ?? throw new NotFoundException("Customer");

    private async Task<string> UnitCodeAsync(Guid unitId, CancellationToken ct) =>
        await db.UnitOfMeasures.Where(u => u.Id == unitId).Select(u => u.Code).FirstAsync(ct);
}
