using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Settings;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.OwnShop;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.OwnShop.Sales;

/// <summary>
/// Sales over the own shop's counter, by the piece (CLAUDE.md §4 "Own shop"). Paid in full when made,
/// by walk-in and known customers alike, so a sale never touches a customer's balance.
///
/// Each line's rate starts at the customer's agreed rate per piece, else the product's rate, and the
/// office may lower it - never below the product's minimum, never above its rate (<see cref="ShopRates"/>).
/// The shop can never sell pieces it does not have: the stock check runs under a lock on the products,
/// and the sale, its number and its stock movements are saved in one transaction or not at all.
/// </summary>
public class ShopSaleService(AppDbContext db, StockService stock, CustomerPriceService prices)
{
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public async Task<ShopSaleDetailDto> CreateAsync(CreateShopSaleRequest request, CancellationToken ct)
    {
        // A second press of Save sends the same id: answer with the sale the first one made.
        if (request.ClientRequestId is { } clientRequestId &&
            await db.ShopSales.AsNoTracking().FirstOrDefaultAsync(s => s.ClientRequestId == clientRequestId, ct) is { } existing)
        {
            return await GetAsync(existing.Id, ct);
        }

        if (request.PaymentMethod == PaymentMethod.ReturnCredit)
        {
            throw new DomainException("A shop sale is paid in money. Choose how the customer paid.");
        }

        var customer = await ResolveCustomerAsync(request.CustomerId, ct);
        var lines = await BuildLinesAsync(customer?.Id, request.Lines, ct);
        var locationId = KnownStockLocations.OwnShopId;

        var sale = new ShopSale
        {
            SaleNumber = string.Empty,
            SeriesCode = string.Empty,
            FinancialYear = string.Empty,
            SaleDate = IndiaTime.Today(),
            LocationId = locationId,
            CustomerId = customer?.Id,
            CustomerName = customer?.Name,
            PaymentMethod = request.PaymentMethod,
            TotalAmount = lines.Sum(l => l.LineTotal),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            ClientRequestId = request.ClientRequestId,
            Lines = lines
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Two sales of the last pieces queue up here instead of both passing the check below.
        await stock.LockProductsAsync(lines.Select(l => l.ProductId), ct);

        foreach (var line in lines)
        {
            var onHand = await stock.GetQuantityOnHandAsync(line.ProductId, locationId, ct);

            if (line.Quantity > onHand)
            {
                throw new DomainException(
                    $"The shop has {(onHand > 0m ? $"{Quantity(onHand)} pieces" : "no pieces")} of {line.Description}; " +
                    $"this sale needs {Quantity(line.Quantity)}. Sell fewer, or receive more from the factory first.");
            }
        }

        var number = await InvoiceNumbering.ReserveAsync(db, InvoiceSettingsService.OwnShopSeries, sale.SaleDate, ct);
        sale.SaleNumber = number.InvoiceNumber;
        sale.SeriesCode = number.SeriesCode;
        sale.FinancialYear = number.FinancialYear;
        sale.SequenceNumber = number.SequenceNumber;

        db.ShopSales.Add(sale);

        var now = DateTime.UtcNow;
        db.StockMovements.AddRange(lines.Select(line => new StockMovement
        {
            ProductId = line.ProductId,
            LocationId = locationId,
            MovementType = StockMovementType.Sale,
            Quantity = -line.Quantity,
            OccurredAt = now,
            ReferenceType = StockReferenceType.ShopSale,
            ReferenceId = sale.Id
        }));

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (request.ClientRequestId is { } id && IsDuplicate(exception))
        {
            // The same Save raced itself; the one that got in first is the sale.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();

            var first = await db.ShopSales.AsNoTracking().FirstOrDefaultAsync(s => s.ClientRequestId == id, ct)
                        ?? throw new DomainException("The sale could not be saved. Try again.");
            return await GetAsync(first.Id, ct);
        }

        return await GetAsync(sale.Id, ct);
    }

    /// <summary>
    /// A sale recorded in error. The pieces go back on the shop's shelf; the sale and its number are kept.
    /// Handing the money back is done at the counter, not recorded here.
    /// </summary>
    public async Task<ShopSaleDetailDto> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Say why the sale is being cancelled.");
        }

        var sale = await db.ShopSales.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id, ct)
                   ?? throw new NotFoundException("Shop sale");

        if (sale.Status == ShopSaleStatus.Cancelled)
        {
            throw new DomainException($"Sale {sale.SaleNumber} is already cancelled.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        sale.Status = ShopSaleStatus.Cancelled;
        sale.CancelledAt = DateTime.UtcNow;
        sale.CancellationReason = reason.Trim();

        db.StockMovements.AddRange(sale.Lines.Select(line => new StockMovement
        {
            ProductId = line.ProductId,
            LocationId = sale.LocationId,
            MovementType = StockMovementType.SaleReversal,
            Quantity = line.Quantity,
            OccurredAt = DateTime.UtcNow,
            ReferenceType = StockReferenceType.ShopSale,
            ReferenceId = sale.Id,
            Notes = $"Cancelled: {reason.Trim()}"
        }));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary><paramref name="from"/> and <paramref name="to"/> are IST business days.</summary>
    public async Task<IReadOnlyList<ShopSaleListItemDto>> GetListAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? customerId,
        Guid? productId,
        CancellationToken ct) =>
        await ShopSaleQueries.ProjectList(db.ShopSales
                .Where(s => from == null || s.SaleDate >= from)
                .Where(s => to == null || s.SaleDate <= to)
                .Where(s => customerId == null || s.CustomerId == customerId)
                .Where(s => productId == null || s.Lines.Any(l => l.ProductId == productId))
                .OrderByDescending(s => s.CreatedAt))
            .ToListAsync(ct);

    public async Task<ShopSaleDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        await ShopSaleQueries.ProjectDetail(db.ShopSales.Where(s => s.Id == id), db).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Shop sale");

    private async Task<Customer?> ResolveCustomerAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is not { } id)
        {
            return null;
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException("Customer");

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        return customer;
    }

    /// <summary>Checks every line and prices it; writes nothing, so a refused sale leaves nothing behind.</summary>
    private async Task<List<ShopSaleLine>> BuildLinesAsync(
        Guid? customerId,
        IReadOnlyList<ShopSaleLineRequest>? requested,
        CancellationToken ct)
    {
        if (requested is null || requested.Count == 0)
        {
            throw new DomainException("A sale needs at least one product.");
        }

        if (requested.Select(l => l.ProductId).Distinct().Count() != requested.Count)
        {
            throw new DomainException("The same product is listed twice. Combine the pieces into one line.");
        }

        var ids = requested.Select(l => l.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Kind, p.IsActive, p.SellingPrice, p.MinimumSellingPrice })
            .ToDictionaryAsync(p => p.Id, ct);

        var agreed = customerId is { } id
            ? await prices.GetAgreedPricesAsync(id, ids, ct)
            : [];

        return requested.Select((line, index) =>
        {
            var product = products.GetValueOrDefault(line.ProductId) ?? throw new NotFoundException("Product");

            if (product.Kind != ProductKind.Pieces)
            {
                throw new DomainException(
                    $"'{product.Name}' is not sold by the piece at the own shop. Bill it on an invoice instead.");
            }

            if (!product.IsActive)
            {
                throw new DomainException($"Product '{product.Name}' is not active.");
            }

            if (line.Quantity <= 0m || line.Quantity != decimal.Truncate(line.Quantity))
            {
                throw new DomainException($"Enter a whole number of pieces of {product.Name}, more than zero.");
            }

            var defaultRate = ShopRates.DefaultOf(product.Name, product.SellingPrice);
            var rate = CustomerPriceService.Resolve(line.UnitPrice, agreed.TryGetValue(product.Id, out var a) ? a : null, defaultRate)!.Value;
            ShopRates.EnsureAllowed(product.Name, rate, product.SellingPrice, product.MinimumSellingPrice);

            return new ShopSaleLine
            {
                LineNumber = index + 1,
                ProductId = product.Id,
                Description = product.Name,
                Quantity = line.Quantity,
                UnitPrice = rate,
                DefaultPrice = defaultRate,
                MinimumPrice = ShopRates.MinimumOf(defaultRate, product.MinimumSellingPrice),
                LineTotal = GstCalculator.Round(line.Quantity * rate)
            };
        }).ToList();
    }

    private static bool IsDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number);

    private static string Quantity(decimal value) => PdfStyle.Quantity(value);
}
