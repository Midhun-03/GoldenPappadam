using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>
/// Finalizes invoices. The admin panel and the sales app both come through here, so there is one
/// copy of how a bill is priced, taxed, numbered and taken off stock.
///
/// Finalizing is one transaction: reserve the number, save the invoice and its lines, write the
/// stock movements, commit. Any failure rolls all of it back, the number included, so there is
/// never a half-saved bill and never a skipped number. The PDF is made after the commit, by
/// <see cref="Documents.InvoiceDocumentService"/>, because a printing problem must never undo a sale.
/// </summary>
public class InvoiceService(AppDbContext db, StockService stock, CustomerPriceService prices)
{
    private const int SaveAttempts = 3;
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public async Task<CreateInvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        // Where the goods physically came from: the warehouse for a counter sale, the van for a
        // delivery on the route. Defaults to the warehouse, which is what every bill meant before
        // the van kept its own stock.
        var locationId = await stock.ResolveLocationAsync(request.LocationId, ct);
        var (invoice, seriesCode) = await BuildAsync(request, ct);

        await SaveAsync(invoice, seriesCode, locationId, ct);

        var detail = await GetDetailAsync(invoice.Id, ct);
        var warnings = await WarningsForAsync(invoice.Lines.Select(l => l.ProductId).Distinct(), locationId, ct);

        return new CreateInvoiceResponse(detail, warnings);
    }

    /// <summary>Works the bill out exactly as <see cref="CreateAsync"/> would, and saves nothing.</summary>
    public async Task<InvoicePreviewDto> PreviewAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        var (invoice, _) = await BuildAsync(request, ct);

        return new InvoicePreviewDto(
            invoice.DocumentType,
            invoice.PlaceOfSupplyStateCode,
            invoice.IsInterState,
            invoice.PricesIncludeTax,
            invoice.SubTotal,
            invoice.DiscountAmount,
            invoice.TaxableAmount,
            invoice.CgstAmount,
            invoice.SgstAmount,
            invoice.IgstAmount,
            invoice.CessAmount,
            invoice.RoundOff,
            invoice.TotalAmount,
            invoice.Lines.Select(l => new InvoiceLineDto(
                Guid.Empty, l.ProductId, l.Description, l.UnitCode, l.HsnCode, l.Quantity, l.UnitPrice,
                l.LineTotal, l.DiscountAmount, l.TaxTreatment, l.GstRate, l.TaxableValue, l.CgstAmount,
                l.SgstAmount, l.IgstAmount, l.CessAmount,
                l.TaxableValue + l.CgstAmount + l.SgstAmount + l.IgstAmount + l.CessAmount)).ToList());
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

        // The number stays with the cancelled invoice for ever: GST records must account for every
        // number issued, so it is never freed for reuse.
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

    /// <summary>
    /// Validates the request and works out every amount, returning an unsaved invoice with no
    /// number yet. Nothing is tracked by the context until <see cref="SaveAsync"/>.
    /// </summary>
    private async Task<(Invoice Invoice, string SeriesCode)> BuildAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        if (request.Lines.Count == 0)
        {
            throw new DomainException("An invoice needs at least one line.");
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                       ?? throw new NotFoundException("Customer");

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        if (request.DiscountAmount < 0m)
        {
            throw new DomainException("A discount cannot be negative.");
        }

        var settings = await db.InvoiceSettings.AsNoTracking().SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);
        var branch = await ResolveBranchAsync(customer, request.BranchId, ct);
        var products = await LoadProductsAsync(customer.Id, request.Lines, ct);
        var supplierRegistered = settings.Gstin is not null;

        if (supplierRegistered && products.Values.FirstOrDefault(p => p.TaxTreatment is null) is { } untreated)
        {
            throw new DomainException(
                $"'{untreated.Name}' has no GST treatment. Set it on the product (taxable, exempt, nil-rated " +
                "or non-GST, as the accountant advises) before billing it.");
        }

        var chargesTax = supplierRegistered && products.Values.Any(p => p.TaxTreatment == TaxTreatment.Taxable);
        var placeOfSupply = PlaceOfSupply(customer, branch);

        if (chargesTax && placeOfSupply is null)
        {
            throw new DomainException(branch is null
                ? $"Set the state for '{customer.Name}' first. It is the place of supply, which decides " +
                  "between CGST + SGST and IGST."
                : $"Set the state for the '{branch.Name}' branch first. A branch bill's place of supply " +
                  "is the branch, which decides between CGST + SGST and IGST.");
        }

        var isInterState = chargesTax && placeOfSupply != settings.StateCode;

        var calculation = GstCalculator.Calculate(
            request.Lines.Select(l =>
            {
                var product = products[l.ProductId];
                return new GstCalculator.LineInput(l.Quantity, product.UnitPrice(l.UnitPrice), product.TaxTreatment, product.GstRate);
            }).ToList(),
            request.DiscountAmount,
            new GstCalculator.TaxBasis(supplierRegistered, settings.PricesIncludeTax, isInterState, settings.RoundToNearestRupee));

        if (request.DiscountAmount > calculation.SubTotal)
        {
            throw new DomainException("The discount cannot be more than the bill.");
        }

        var invoice = new Invoice
        {
            // Filled in by SaveAsync, inside the transaction, from the database's counter.
            InvoiceNumber = string.Empty,
            SeriesCode = string.Empty,
            FinancialYear = string.Empty,
            DocumentType = !supplierRegistered ? InvoiceDocumentType.Invoice
                : chargesTax ? InvoiceDocumentType.TaxInvoice
                : InvoiceDocumentType.BillOfSupply,
            CustomerId = customer.Id,
            BranchId = branch?.Id,
            InvoiceDate = request.InvoiceDate ?? IndiaTime.Today(),
            Status = InvoiceStatus.Issued,

            SupplierName = settings.LegalName,
            SupplierAddress = settings.Address,
            SupplierGstin = settings.Gstin,
            SupplierStateCode = settings.StateCode,

            CustomerName = customer.Name,
            CustomerAddress = customer.Address,
            CustomerPhone = customer.Phone,
            CustomerGstin = customer.Gstin,
            CustomerStateCode = customer.StateCode,

            BranchName = branch?.Name,
            BranchAddress = branch?.Address,
            BranchPhone = branch?.Phone,
            BranchGstin = branch?.Gstin,
            BranchStateCode = branch?.StateCode,

            PlaceOfSupplyStateCode = placeOfSupply,
            IsInterState = isInterState,
            ReverseCharge = false,
            PricesIncludeTax = settings.PricesIncludeTax,

            SubTotal = calculation.SubTotal,
            DiscountAmount = calculation.DiscountAmount,
            TaxableAmount = calculation.TaxableAmount,
            CgstAmount = calculation.CgstAmount,
            SgstAmount = calculation.SgstAmount,
            IgstAmount = calculation.IgstAmount,
            CessAmount = calculation.CessAmount,
            RoundOff = calculation.RoundOff,
            TotalAmount = calculation.TotalAmount,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),

            Lines = request.Lines.Select((line, index) =>
            {
                var product = products[line.ProductId];
                var result = calculation.Lines[index];

                return new InvoiceLine
                {
                    LineNumber = index + 1,
                    ProductId = product.Id,
                    Description = product.Name,
                    UnitCode = product.UnitCode,
                    HsnCode = product.HsnCode,
                    Quantity = line.Quantity,
                    UnitPrice = product.UnitPrice(line.UnitPrice),
                    LineTotal = result.LineTotal,
                    DiscountAmount = result.DiscountAmount,
                    TaxTreatment = result.Treatment,
                    GstRate = result.GstRate,
                    TaxableValue = result.TaxableValue,
                    CgstAmount = result.CgstAmount,
                    SgstAmount = result.SgstAmount,
                    IgstAmount = result.IgstAmount,
                    CessAmount = result.CessAmount
                };
            }).ToList()
        };

        return (invoice, settings.SeriesCode);
    }

    /// <summary>
    /// Where the goods were delivered. A branch bill looks only at the branch - falling back to the
    /// parent could put Danya's Coimbatore delivery in Kerala. A GSTIN's first two digits are its
    /// state, so a registered shop with no state picked still has a known place of supply.
    /// </summary>
    private static string? PlaceOfSupply(Customer customer, CustomerBranch? branch) =>
        branch is not null
            ? branch.StateCode ?? (branch.Gstin is null ? null : Gstin.StateCodeOf(branch.Gstin))
            : customer.StateCode ?? (customer.Gstin is null ? null : Gstin.StateCodeOf(customer.Gstin));

    /// <summary>
    /// Numbers and saves the invoice in one transaction. A unique-index clash means the counter has
    /// fallen behind the invoices on file - it cannot happen while every number comes from
    /// <see cref="InvoiceNumbering"/> - so the counter is repaired and the save tried again rather
    /// than showing the person an error they cannot fix.
    /// </summary>
    private async Task SaveAsync(Invoice invoice, string seriesCode, Guid locationId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var number = await InvoiceNumbering.ReserveAsync(db, seriesCode, invoice.InvoiceDate, ct);
            invoice.InvoiceNumber = number.InvoiceNumber;
            invoice.SeriesCode = number.SeriesCode;
            invoice.FinancialYear = number.FinancialYear;
            invoice.SequenceNumber = number.SequenceNumber;

            db.Invoices.Add(invoice);

            var movements = invoice.Lines.Select(line => new StockMovement
            {
                ProductId = line.ProductId,
                LocationId = locationId,
                MovementType = StockMovementType.Sale,
                Quantity = -line.Quantity,
                OccurredAt = DateTime.UtcNow,
                ReferenceType = StockReferenceType.Invoice,
                ReferenceId = invoice.Id
            }).ToList();

            db.StockMovements.AddRange(movements);

            try
            {
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception) && attempt < SaveAttempts)
            {
                await transaction.RollbackAsync(ct);

                // Forget only what this attempt added. Clearing the whole change tracker would also
                // drop things the caller is still using - a shop a phone created earlier in the same
                // batch, the device itself - and re-adding the invoice would then insert that shop
                // a second time through the invoice's navigation.
                var lines = invoice.Lines.ToList();

                foreach (var entity in movements.Cast<object>().Concat(lines).Append(invoice).ToList())
                {
                    db.Entry(entity).State = EntityState.Detached;
                }

                // Detaching a line takes it out of the invoice's collection; put every one back so
                // the retry saves the whole bill.
                invoice.Lines = lines;

                await InvoiceNumbering.RepairAsync(db, number.SeriesCode, number.FinancialYear, ct);
            }
        }
    }

    /// <summary>
    /// A multi-branch customer must name the shop the goods are for; a plain customer must not,
    /// since it has no branch to point at. This is what keeps a bill from ever landing on the
    /// wrong Danya Supermarket branch, or on a branch that belongs to a different shop entirely.
    /// </summary>
    private async Task<CustomerBranch?> ResolveBranchAsync(Customer customer, Guid? requestedBranchId, CancellationToken ct)
    {
        if (!customer.HasMultipleBranches)
        {
            return null;
        }

        if (requestedBranchId is null)
        {
            throw new DomainException("Please select a branch before continuing.");
        }

        var branch = await db.CustomerBranches
                         .AsNoTracking()
                         .FirstOrDefaultAsync(b => b.Id == requestedBranchId && b.CustomerId == customer.Id, ct)
                     ?? throw new NotFoundException("Branch");

        if (!branch.IsActive)
        {
            throw new DomainException($"Branch '{branch.Name}' is not active.");
        }

        return branch;
    }

    private sealed record ProductForBill(
        Guid Id,
        string Name,
        string UnitCode,
        string? HsnCode,
        TaxTreatment? TaxTreatment,
        decimal? GstRate,
        decimal? AgreedPrice,
        decimal? SellingPrice)
    {
        /// <summary>What the office typed, else what this shop has agreed, else the product's own price.</summary>
        public decimal UnitPrice(decimal? typed) =>
            CustomerPriceService.Resolve(typed, AgreedPrice, SellingPrice)
            ?? throw new DomainException(
                $"'{Name}' has no selling price. Enter a price on the line, agree one with this customer, " +
                "or set one on the product.");
    }

    private async Task<Dictionary<Guid, ProductForBill>> LoadProductsAsync(
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
                p.HsnCode,
                p.TaxTreatment,
                p.GstRate,
                UnitCode = p.UnitOfMeasure!.Code
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var result = new Dictionary<Guid, ProductForBill>();

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

            var forBill = result.TryGetValue(product.Id, out var known)
                ? known
                : result[product.Id] = new ProductForBill(
                    product.Id, product.Name, product.UnitCode, product.HsnCode, product.TaxTreatment, product.GstRate,
                    agreedPrices.TryGetValue(product.Id, out var agreed) ? agreed : null, product.SellingPrice);

            if (forBill.UnitPrice(line.UnitPrice) < 0m)
            {
                throw new DomainException($"The price for '{product.Name}' cannot be negative.");
            }
        }

        return result;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
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
