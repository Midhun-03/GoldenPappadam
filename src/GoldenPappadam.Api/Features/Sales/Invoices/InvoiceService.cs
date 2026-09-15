using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

public class InvoiceService(AppDbContext db, StockService stock, CustomerPriceService prices)
{
    private const int NumberRetryAttempts = 3;
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public async Task<CreateInvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0)
        {
            throw new DomainException("An invoice needs at least one line.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                       ?? throw new NotFoundException("Customer");

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        var invoiceDate = request.InvoiceDate ?? IndiaTime.Today();

        // Where the goods physically came from: the warehouse for a counter sale, the van for a
        // delivery on the route. Defaults to the warehouse, which is what every bill meant before
        // the van kept its own stock.
        var locationId = await stock.ResolveLocationAsync(request.LocationId, ct);
        var lines = await BuildLinesAsync(customer.Id, request.Lines, ct);

        var subTotal = decimal.Round(lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);

        if (request.DiscountAmount < 0m)
        {
            throw new DomainException("A discount cannot be negative.");
        }

        if (request.DiscountAmount > subTotal)
        {
            throw new DomainException("The discount cannot be more than the bill.");
        }

        var invoice = await SaveInvoiceAsync(request, customer, invoiceDate, lines, subTotal, locationId, ct);
        var detail = await GetDetailAsync(invoice.Id, ct);
        var warnings = await WarningsForAsync(lines.Select(l => l.ProductId).Distinct(), locationId, ct);

        return new CreateInvoiceResponse(detail, warnings);
    }

    public async Task<InvoiceDetailDto> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct)
                      ?? throw new NotFoundException("Invoice");

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new DomainException($"Invoice {invoice.InvoiceNumber} is already cancelled.");
        }

        var allocated = await db.PaymentAllocations
            .Where(a => a.InvoiceId == id)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        if (allocated > 0m)
        {
            throw new DomainException(
                $"Payments totalling {allocated:0.00} are applied to invoice {invoice.InvoiceNumber}. " +
                "Sort the payment out first, then cancel the bill.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelledAt = DateTime.UtcNow;
        invoice.CancellationReason = reason.Trim();

        // Put the stock back where it actually came from. Mirroring the original movements rather
        // than rebuilding from the lines is what keeps a van sale going back onto the van.
        var sold = await db.StockMovements
            .Where(m => m.ReferenceType == StockReferenceType.Invoice &&
                        m.ReferenceId == invoice.Id &&
                        m.MovementType == StockMovementType.Sale)
            .ToListAsync(ct);

        db.StockMovements.AddRange(sold.Select(movement => new StockMovement
        {
            ProductId = movement.ProductId,
            LocationId = movement.LocationId,
            MovementType = StockMovementType.SaleReversal,
            Quantity = -movement.Quantity,
            OccurredAt = DateTime.UtcNow,
            ReferenceType = StockReferenceType.Invoice,
            ReferenceId = invoice.Id,
            Notes = $"Cancelled: {reason.Trim()}"
        }));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetDetailAsync(id, ct);
    }

    public async Task<InvoiceDetailDto> GetDetailAsync(Guid id, CancellationToken ct) =>
        await InvoiceQueries.ProjectDetail(db.Invoices.Where(i => i.Id == id), db).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Invoice");

    private async Task<List<InvoiceLine>> BuildLinesAsync(
        Guid customerId,
        IReadOnlyList<InvoiceLineRequest> requested,
        CancellationToken ct)
    {
        var productIds = requested.Select(l => l.ProductId).Distinct().ToList();
        var agreedPrices = await prices.GetAgreedPricesAsync(customerId, productIds, ct);
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.IsActive,
                p.SellingPrice,
                UnitCode = p.UnitOfMeasure!.Code
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var lines = new List<InvoiceLine>();

        foreach (var line in requested)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                throw new NotFoundException("Product");
            }

            if (!product.IsActive)
            {
                throw new DomainException($"Product '{product.Name}' is not active.");
            }

            if (line.Quantity <= 0m)
            {
                throw new DomainException($"The quantity for '{product.Name}' must be greater than zero.");
            }

            // What the office typed, else what this shop has agreed, else the product's own price.
            var agreed = agreedPrices.TryGetValue(product.Id, out var shopPrice) ? shopPrice : (decimal?)null;

            var unitPrice = CustomerPriceService.Resolve(line.UnitPrice, agreed, product.SellingPrice)
                ?? throw new DomainException(
                    $"'{product.Name}' has no selling price. Enter a price on the line, agree one with " +
                    "this customer, or set one on the product.");

            if (unitPrice < 0m)
            {
                throw new DomainException($"The price for '{product.Name}' cannot be negative.");
            }

            lines.Add(new InvoiceLine
            {
                ProductId = product.Id,
                Description = product.Name,
                UnitCode = product.UnitCode,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                LineTotal = decimal.Round(line.Quantity * unitPrice, 2, MidpointRounding.AwayFromZero)
            });
        }

        return lines;
    }

    private async Task<Invoice> SaveInvoiceAsync(
        CreateInvoiceRequest request,
        Customer customer,
        DateOnly invoiceDate,
        List<InvoiceLine> lines,
        decimal subTotal,
        Guid locationId,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var invoice = new Invoice
            {
                InvoiceNumber = await NextInvoiceNumberAsync(invoiceDate, ct),
                CustomerId = customer.Id,
                InvoiceDate = invoiceDate,
                Status = InvoiceStatus.Issued,
                SubTotal = subTotal,
                DiscountAmount = request.DiscountAmount,
                TotalAmount = subTotal - request.DiscountAmount,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Lines = lines
            };

            db.Invoices.Add(invoice);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsDuplicateInvoiceNumber(exception) && attempt < NumberRetryAttempts)
            {
                // Another bill took that number in the meantime. Roll back and take the next one.
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                continue;
            }

            db.StockMovements.AddRange(lines.Select(line => new StockMovement
            {
                ProductId = line.ProductId,
                LocationId = locationId,
                MovementType = StockMovementType.Sale,
                Quantity = -line.Quantity,
                OccurredAt = DateTime.UtcNow,
                ReferenceType = StockReferenceType.Invoice,
                ReferenceId = invoice.Id
            }));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return invoice;
        }
    }

    /// <summary>
    /// INV-&lt;financial year&gt;-&lt;5 digits&gt;. The Indian financial year starts on 1 April, so a bill
    /// dated 2026-03-31 belongs to FY 2025 and one dated 2026-04-01 to FY 2026.
    /// </summary>
    private async Task<string> NextInvoiceNumberAsync(DateOnly invoiceDate, CancellationToken ct)
    {
        var financialYear = invoiceDate.Month >= 4 ? invoiceDate.Year : invoiceDate.Year - 1;
        var prefix = $"INV-{financialYear}-";

        var lastNumber = await db.Invoices
            .Where(i => i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);

        var next = lastNumber is null ? 1 : int.Parse(lastNumber[prefix.Length..]) + 1;

        return prefix + next.ToString("D5");
    }

    private static bool IsDuplicateInvoiceNumber(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && UniqueViolationErrors.Contains(sql.Number);

    private async Task<IReadOnlyList<string>> WarningsForAsync(
        IEnumerable<Guid> productIds,
        Guid locationId,
        CancellationToken ct)
    {
        var warnings = new List<string>();

        foreach (var productId in productIds)
        {
            var onHand = await stock.GetQuantityOnHandAsync(productId, locationId, ct);

            if (onHand < 0m)
            {
                var name = await db.Products.Where(p => p.Id == productId).Select(p => p.Name).FirstAsync(ct);
                warnings.Add($"{name}: stock is now {onHand:0.###}. Record the missing production or adjust it.");
            }
        }

        return warnings;
    }
}
