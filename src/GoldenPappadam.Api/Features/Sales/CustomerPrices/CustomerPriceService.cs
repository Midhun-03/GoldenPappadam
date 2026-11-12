using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.OwnShop;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.CustomerPrices;

/// <summary>
/// What each shop pays. The one rule worth stating plainly lives in <see cref="Resolve"/>:
/// an explicit price on the bill line wins, then the shop's agreed price, then the product's own
/// selling price. Every change goes through one method, so it lands in the history exactly once.
///
/// Who may change a rate is decided here, not only by the endpoints (CLAUDE.md §4 "Rate-change
/// approval", 2026-09-30): the office changes rates directly through <see cref="SetAsync"/> and
/// <see cref="RemoveAsync"/>, which refuse a salesperson whichever way they got in. A salesperson sets
/// rates only through <see cref="SetInitialRatesAsync"/>, for a customer with no rate history yet; any
/// later change is a request, applied by <see cref="ApplyApprovedRequestAsync"/> when an admin agrees.
/// </summary>
public class CustomerPriceService(AppDbContext db, ICurrentUser currentUser)
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
    /// <summary>
    /// The office changing a rate. Refused for a salesperson: theirs is a request to the office.
    /// Zero is not a rate (owner, 2026-11-12): it means the shop has no agreed rate and pays the standard
    /// price, so zero typed over an existing rate removes it, exactly as <see cref="RemoveAsync"/> does.
    /// </summary>
    public async Task<CustomerPriceDto> SetAsync(
        Guid customerId,
        Guid productId,
        decimal unitPrice,
        CancellationToken ct)
    {
        EnsureOffice();

        if (unitPrice == 0m)
        {
            return await ClearAsync(customerId, productId, ct);
        }

        var product = await SetCoreAsync(customerId, productId, unitPrice, null, ct);
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

    /// <summary>Zero from the office: the agreed rate, if there is one, is removed and recorded as removed.</summary>
    private async Task<CustomerPriceDto> ClearAsync(Guid customerId, Guid productId, CancellationToken ct)
    {
        await EnsureCustomerExistsAsync(customerId, ct);

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product");

        var price = await db.CustomerPrices
            .FirstOrDefaultAsync(cp => cp.CustomerId == customerId && cp.ProductId == productId && cp.IsActive, ct);

        if (price is not null)
        {
            price.IsActive = false;
            RecordChange(customerId, productId, price.UnitPrice, null);
            await db.SaveChangesAsync(ct);
        }

        return new CustomerPriceDto(
            product.Id,
            product.ProductCode,
            product.Name,
            await UnitCodeAsync(product.UnitOfMeasureId, ct),
            product.SellingPrice,
            null,
            product.SellingPrice);
    }

    /// <summary>
    /// A new customer's first rates, as the salesperson who found the shop agreed them. Allowed only while
    /// the customer has no rate history at all - that is, as it is being created, in the same transaction
    /// - so it can never be used to change a rate the customer already has. Not saved here.
    /// </summary>
    public async Task SetInitialRatesAsync(
        Guid customerId,
        IReadOnlyList<(Guid ProductId, decimal UnitPrice)> rates,
        CancellationToken ct)
    {
        if (rates.Count == 0)
        {
            return;
        }

        if (await db.CustomerPriceChanges.AnyAsync(c => c.CustomerId == customerId, ct) ||
            db.ChangeTracker.Entries<CustomerPriceChange>().Any(e => e.Entity.CustomerId == customerId))
        {
            throw new DomainException(
                "This shop's rates are already set. To change one, send the office a rate-change request.");
        }

        await EnsureInitialRatesValidAsync(rates, ct);

        foreach (var (productId, unitPrice) in rates)
        {
            await SetCoreAsync(customerId, productId, unitPrice, null, ct);
        }
    }

    /// <summary>
    /// Checks a new shop's rates on their own - each product once, active, and priced above zero - so a
    /// caller can refuse them before it creates the shop, rather than half way through.
    /// </summary>
    public async Task EnsureInitialRatesValidAsync(
        IReadOnlyList<(Guid ProductId, decimal UnitPrice)> rates,
        CancellationToken ct)
    {
        if (rates.Select(r => r.ProductId).Distinct().Count() != rates.Count)
        {
            throw new DomainException("A product is on the new shop's rates twice.");
        }

        if (rates.Any(r => r.UnitPrice <= 0m))
        {
            throw new DomainException("A rate must be more than zero.");
        }

        var ids = rates.Select(r => r.ProductId).ToList();
        var active = await db.Products.CountAsync(p => ids.Contains(p.Id) && p.IsActive, ct);

        if (active != ids.Count)
        {
            throw new DomainException("One of the new shop's rates is for a product that is not sold any more.");
        }

        if (await db.Products.Where(p => ids.Contains(p.Id) && p.Kind == ProductKind.Pieces).Select(p => p.Name)
                .FirstOrDefaultAsync(ct) is { } pieces)
        {
            throw ShopRates.OnlyAtTheShop(pieces);
        }
    }

    /// <summary>
    /// Applies a request an admin has just approved: the rate becomes what was asked for, and the change
    /// is recorded with the admin as the changer and the request that led to it. Not saved here.
    /// </summary>
    public async Task ApplyApprovedRequestAsync(CustomerRateRequest request, CancellationToken ct)
    {
        EnsureOffice();
        await SetCoreAsync(request.CustomerId, request.ProductId, request.RequestedPrice, request.Id, ct);
    }

    /// <summary>
    /// The one place a rate changes. Checks the customer and product, writes the new rate and its history
    /// entry, and leaves saving to the caller so it can join the caller's transaction.
    /// </summary>
    private async Task<Product> SetCoreAsync(
        Guid customerId,
        Guid productId,
        decimal unitPrice,
        Guid? rateRequestId,
        CancellationToken ct)
    {
        // Zero is never a rate: the office's zero is handled as a removal before it gets here, and nobody
        // else - a new shop's first rates, an approved request - may remove one.
        if (unitPrice <= 0m)
        {
            throw new DomainException("A rate must be more than zero.");
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

        // A caterer's agreed rate per piece at the own shop is still inside the shop's band.
        if (product.Kind == ProductKind.Pieces)
        {
            ShopRates.EnsureAllowed(product.Name, unitPrice, product.SellingPrice, product.MinimumSellingPrice);
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

            RecordChange(customerId, productId, previous, unitPrice, rateRequestId);
        }

        return product;
    }

    /// <summary>
    /// Rates are the office's to change directly. A salesperson's route is a request (or, for a customer
    /// being created, its first rates), so refusing here closes every other door at once.
    /// </summary>
    private void EnsureOffice()
    {
        if (currentUser.IsInRole(Roles.Salesperson))
        {
            throw new DomainException(
                "A salesperson cannot change a customer's rate directly. Send the office a rate-change request.");
        }
    }

    /// <summary>
    /// Ends the special arrangement, so the shop goes back to paying the product's own price.
    /// The row is deactivated rather than deleted, so the history of the arrangement survives.
    /// </summary>
    public async Task RemoveAsync(Guid customerId, Guid productId, CancellationToken ct)
    {
        EnsureOffice();

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
                    ur.UserId == c.CreatedBy && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Salesperson)),
                c.RateRequestId == null
                    ? null
                    : db.Users.Where(u => u.Id == c.RateRequest!.CreatedBy).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
    }

    private void RecordChange(Guid customerId, Guid productId, decimal? previous, decimal? next, Guid? rateRequestId = null) =>
        db.CustomerPriceChanges.Add(new CustomerPriceChange
        {
            CustomerId = customerId,
            ProductId = productId,
            PreviousPrice = previous,
            NewPrice = next,
            RateRequestId = rateRequestId
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
    /// What a shop pays for each product now, and every rate it has had since <paramref name="since"/>
    /// - the moment a phone last heard from the office. A bill made on that phone is honest at any of
    /// them: it was priced from what the phone had been told (its last-known rate).
    ///
    /// The histories make this a small question. The rate at <paramref name="since"/> is the "previous"
    /// side of the first change after it, so every rate in the window is the current one or one side of
    /// a change inside it. While the shop had no agreed rate it paid the standard price, so the standard
    /// price's own changes count for that stretch. This errs towards accepting: a standard price is
    /// allowed for the whole window once the shop was without an agreed rate at any point in it. That
    /// is deliberate - a bill not at the current rate is flagged for the office either way.
    /// </summary>
    public async Task<Dictionary<Guid, RatesSince>> GetRatesSinceAsync(
        Guid customerId,
        IEnumerable<Guid> productIds,
        DateTime since,
        CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        var agreed = await GetAgreedPricesAsync(customerId, ids, ct);

        var standard = await db.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.SellingPrice, ct);

        var agreedChanges = await db.CustomerPriceChanges
            .Where(c => c.CustomerId == customerId && ids.Contains(c.ProductId) && c.CreatedAt > since)
            .Select(c => new { c.ProductId, c.PreviousPrice, c.NewPrice })
            .ToListAsync(ct);

        var standardChanges = await db.ProductPriceChanges
            .Where(c => ids.Contains(c.ProductId) && c.CreatedAt > since)
            .Select(c => new { c.ProductId, c.PreviousPrice, NewPrice = (decimal?)c.NewPrice })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, RatesSince>();

        foreach (var id in ids)
        {
            var agreedNow = agreed.TryGetValue(id, out var rate) ? rate : (decimal?)null;
            var standardNow = standard.GetValueOrDefault(id);
            var mine = agreedChanges.Where(c => c.ProductId == id).ToList();

            var known = new HashSet<decimal>();
            Add(known, agreedNow);

            foreach (var change in mine)
            {
                Add(known, change.PreviousPrice);
                Add(known, change.NewPrice);
            }

            if (agreedNow is null || mine.Any(c => c.PreviousPrice is null || c.NewPrice is null))
            {
                Add(known, standardNow);

                foreach (var change in standardChanges.Where(c => c.ProductId == id))
                {
                    Add(known, change.PreviousPrice);
                    Add(known, change.NewPrice);
                }
            }

            result[id] = new RatesSince(Resolve(null, agreedNow, standardNow), known);
        }

        return result;

        static void Add(HashSet<decimal> set, decimal? price)
        {
            if (price is { } value)
            {
                set.Add(value);
            }
        }
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
