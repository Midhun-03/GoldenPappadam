using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.CustomerPrices;

/// <summary>
/// What each shop pays. The one rule worth stating plainly lives in <see cref="Resolve"/>:
/// an explicit price on the bill line wins, then the shop's agreed price, then the product's own
/// selling price. The office sets agreed prices here, and so does the phone's sync - both through
/// <see cref="SetAsync"/>, which is why every change lands in the history exactly once.
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

        var previous = existing is { IsActive: true } ? existing.UnitPrice : (decimal?)null;

        // Setting the rate it already has is not a change, and must not add noise to the history.
        if (previous != unitPrice)
        {
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

            RecordChange(customerId, productId, previous, unitPrice);
            await db.SaveChangesAsync(ct);
        }

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
        RecordChange(customerId, productId, price.UnitPrice, null);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Rate changes, newest first - for one customer, or across all of them for the office's review.
    /// <paramref name="from"/> and <paramref name="to"/> are IST business days.
    /// </summary>
    public async Task<IReadOnlyList<CustomerPriceChangeDto>> GetChangesAsync(
        Guid? customerId,
        bool salespersonOnly,
        DateOnly? from,
        DateOnly? to,
        int limit,
        CancellationToken ct)
    {
        var fromUtc = from is { } fromDay ? IndiaTime.DayRangeUtc(fromDay).Start : (DateTime?)null;
        var toUtc = to is { } toDay ? IndiaTime.DayRangeUtc(toDay).End : (DateTime?)null;

        return await db.CustomerPriceChanges
            .Where(c => customerId == null || c.CustomerId == customerId)
            .Where(c => fromUtc == null || c.CreatedAt >= fromUtc)
            .Where(c => toUtc == null || c.CreatedAt < toUtc)
            .Where(c => !salespersonOnly || db.UserRoles.Any(ur =>
                ur.UserId == c.CreatedBy && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Salesperson)))
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(c => new CustomerPriceChangeDto(
                c.Id,
                c.CustomerId,
                c.Customer!.Name,
                c.ProductId,
                c.Product!.Name,
                c.Product.UnitOfMeasure!.Code,
                c.PreviousPrice,
                c.NewPrice,
                c.CreatedAt,
                db.Users.Where(u => u.Id == c.CreatedBy).Select(u => u.FullName).FirstOrDefault(),
                db.UserRoles.Any(ur =>
                    ur.UserId == c.CreatedBy && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Salesperson))))
            .ToListAsync(ct);
    }

    private void RecordChange(Guid customerId, Guid productId, decimal? previous, decimal? next) =>
        db.CustomerPriceChanges.Add(new CustomerPriceChange
        {
            CustomerId = customerId,
            ProductId = productId,
            PreviousPrice = previous,
            NewPrice = next
        });

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
