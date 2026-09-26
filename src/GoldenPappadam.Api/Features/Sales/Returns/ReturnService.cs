using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.Sales.CustomerBranches;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Api.Features.Sales.Invoices;
using GoldenPappadam.Api.Features.Sales.Payments;
using GoldenPappadam.Api.Features.Sales.Settings;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Returns;

/// <summary>
/// Expired or damaged packets a shop gave back (owner's rules, 2026-09-25): good packets are never
/// returned, returned packets are never resold, and what the shop gets depends on the shop - a free
/// replacement, a credit, or nothing, decided by the office.
///
/// So a return never adds to stock. A replacement takes fresh stock out as a
/// <see cref="StockMovementType.Replacement"/>; a credit is a <see cref="PaymentMethod.ReturnCredit"/>
/// payment, which settles the shop's oldest bills exactly as money would. Recording, numbering and
/// either settlement happen in one transaction.
/// </summary>
public class ReturnService(
    AppDbContext db,
    StockService stock,
    CustomerPriceService prices,
    PaymentService payments)
{
    public async Task<ReturnResponse> CreateAsync(CreateReturnRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                       ?? throw new NotFoundException("Customer");

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        var branch = await BranchRule.ResolveAsync(db, customer, request.BranchId, ct);
        var lines = await BuildLinesAsync(customer.Id, request.Lines, ct);

        var note = new ReturnNote
        {
            ReturnNumber = string.Empty,
            SeriesCode = string.Empty,
            FinancialYear = string.Empty,
            CustomerId = customer.Id,
            BranchId = branch?.Id,
            CustomerName = customer.Name,
            BranchName = branch?.Name,
            ReturnDate = request.ReturnDate ?? IndiaTime.Today(),
            Settlement = ReturnSettlement.Pending,
            Value = lines.Sum(l => l.Value),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lines = lines
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var number = await InvoiceNumbering.ReserveAsync(db, InvoiceSettingsService.ReturnSeries, note.ReturnDate, ct);
        note.ReturnNumber = number.InvoiceNumber;
        note.SeriesCode = number.SeriesCode;
        note.FinancialYear = number.FinancialYear;
        note.SequenceNumber = number.SequenceNumber;

        db.ReturnNotes.Add(note);

        var touched = await SettleAsync(
            note, request.Settlement, request.CreditAmount, request.ReplacementLocationId, note.ReturnDate, ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new ReturnResponse(await GetAsync(note.Id, ct), await WarningsAsync(touched, ct));
    }

    /// <summary>Decides a pending return: replace, credit, or nothing. Only once.</summary>
    public async Task<ReturnResponse> SettleAsync(Guid id, SettleReturnRequest request, CancellationToken ct)
    {
        var note = await db.ReturnNotes.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new NotFoundException("Return");

        if (note.Status == ReturnStatus.Cancelled)
        {
            throw new DomainException($"Return {note.ReturnNumber} is cancelled.");
        }

        if (note.Settlement != ReturnSettlement.Pending)
        {
            throw new DomainException($"Return {note.ReturnNumber} is already settled.");
        }

        if (request.Settlement == ReturnSettlement.Pending)
        {
            throw new DomainException("Choose how to settle it: replacement, credit or nothing.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // A credit given today is dated today, whenever the packets came back.
        var touched = await SettleAsync(
            note, request.Settlement, request.CreditAmount, request.ReplacementLocationId, IndiaTime.Today(), ct);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new ReturnResponse(await GetAsync(note.Id, ct), await WarningsAsync(touched, ct));
    }

    /// <summary>
    /// A return recorded in error. A replacement's stock goes back where it came from; the note and
    /// its number are kept. A credit cannot be undone this way: it works like a payment, and payments
    /// are never taken back - the same reason a bill with payments on it cannot be cancelled.
    /// </summary>
    public async Task<ReturnDetailDto> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var note = await db.ReturnNotes.FirstOrDefaultAsync(r => r.Id == id, ct)
                   ?? throw new NotFoundException("Return");

        if (note.Status == ReturnStatus.Cancelled)
        {
            throw new DomainException($"Return {note.ReturnNumber} is already cancelled.");
        }

        if (note.Settlement == ReturnSettlement.Credit)
        {
            throw new DomainException(
                $"Return {note.ReturnNumber} credited the shop {note.CreditAmount:0.00}, which counts like a payment " +
                "and cannot be taken back.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        note.Status = ReturnStatus.Cancelled;
        note.CancelledAt = DateTime.UtcNow;
        note.CancellationReason = reason.Trim();

        // Mirror the replacement's own movements, so the stock goes back to the van if it came off the van.
        var replaced = await db.StockMovements
            .Where(m => m.ReferenceType == StockReferenceType.ReturnNote && m.ReferenceId == note.Id &&
                        m.MovementType == StockMovementType.Replacement && m.Quantity < 0)
            .ToListAsync(ct);

        db.StockMovements.AddRange(replaced.Select(m => new StockMovement
        {
            ProductId = m.ProductId,
            LocationId = m.LocationId,
            MovementType = StockMovementType.Replacement,
            Quantity = -m.Quantity,
            OccurredAt = DateTime.UtcNow,
            ReferenceType = StockReferenceType.ReturnNote,
            ReferenceId = note.Id,
            Notes = $"Cancelled: {reason.Trim()}"
        }));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<ReturnDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        await ReturnQueries.ProjectDetail(db.ReturnNotes.Where(r => r.Id == id), db).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Return");

    /// <summary>
    /// Applies a settlement to a note that is tracked but not yet saved. Returns the products and
    /// place a replacement took stock from, for the short-stock warnings.
    /// </summary>
    private async Task<(Guid LocationId, List<Guid> Products)?> SettleAsync(
        ReturnNote note,
        ReturnSettlement settlement,
        decimal? creditAmount,
        Guid? replacementLocationId,
        DateOnly creditDate,
        CancellationToken ct)
    {
        if (settlement != ReturnSettlement.Credit && creditAmount is not null)
        {
            throw new DomainException("A credit amount only goes with a credit.");
        }

        note.Settlement = settlement;
        note.SettledAt = settlement == ReturnSettlement.Pending ? null : DateTime.UtcNow;

        switch (settlement)
        {
            case ReturnSettlement.Credit:
            {
                var amount = creditAmount ?? note.Value;

                if (amount <= 0m)
                {
                    throw new DomainException(
                        "These packets have no rate to value them by. Enter the amount to credit the shop.");
                }

                var credit = await payments.AddReturnCreditAsync(note.CustomerId, creditDate, amount, note.ReturnNumber, ct);
                note.CreditAmount = amount;
                note.CreditPaymentId = credit.Id;
                return null;
            }

            case ReturnSettlement.Replacement:
            {
                var locationId = await stock.ResolveLocationAsync(replacementLocationId, ct);
                var inactive = await db.Products
                    .Where(p => note.Lines.Select(l => l.ProductId).Contains(p.Id) && !p.IsActive)
                    .Select(p => p.Name)
                    .FirstOrDefaultAsync(ct);

                if (inactive is not null)
                {
                    throw new DomainException($"'{inactive}' is no longer made, so it cannot be replaced. Choose credit instead.");
                }

                db.StockMovements.AddRange(note.Lines.Select(line => new StockMovement
                {
                    ProductId = line.ProductId,
                    LocationId = locationId,
                    MovementType = StockMovementType.Replacement,
                    Quantity = -line.Quantity,
                    OccurredAt = DateTime.UtcNow,
                    ReferenceType = StockReferenceType.ReturnNote,
                    ReferenceId = note.Id,
                    Notes = $"Replacing returned packets, {note.ReturnNumber}"
                }));

                return (locationId, note.Lines.Select(l => l.ProductId).Distinct().ToList());
            }

            default:
                return null;
        }
    }

    private async Task<List<ReturnNoteLine>> BuildLinesAsync(
        Guid customerId,
        IReadOnlyList<ReturnLineRequest> requested,
        CancellationToken ct)
    {
        if (requested.Count == 0)
        {
            throw new DomainException("A return needs at least one product.");
        }

        var productIds = requested.Select(l => l.ProductId).Distinct().ToList();
        var agreed = await prices.GetAgreedPricesAsync(customerId, productIds, ct);
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.SellingPrice, UnitCode = p.UnitOfMeasure!.Code })
            .ToDictionaryAsync(p => p.Id, ct);

        return requested.Select((line, index) =>
        {
            var product = products.GetValueOrDefault(line.ProductId) ?? throw new NotFoundException("Product");

            if (line.Quantity <= 0m)
            {
                throw new DomainException($"The quantity of '{product.Name}' returned must be more than zero.");
            }

            // Worth what this shop pays for it; a product with no price at all is worth nothing until
            // the office enters a rate.
            var rate = CustomerPriceService.Resolve(line.UnitRate, agreed.TryGetValue(product.Id, out var a) ? a : null, product.SellingPrice) ?? 0m;

            return new ReturnNoteLine
            {
                LineNumber = index + 1,
                ProductId = product.Id,
                Description = product.Name,
                UnitCode = product.UnitCode,
                Quantity = line.Quantity,
                Reason = line.Reason,
                UnitRate = rate,
                Value = GstCalculator.Round(line.Quantity * rate)
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<string>> WarningsAsync((Guid LocationId, List<Guid> Products)? replaced, CancellationToken ct)
    {
        if (replaced is not { } taken)
        {
            return [];
        }

        var warnings = new List<string>();

        foreach (var productId in taken.Products)
        {
            var onHand = await stock.GetQuantityOnHandAsync(productId, taken.LocationId, ct);
            if (onHand < 0m)
            {
                var name = await db.Products.Where(p => p.Id == productId).Select(p => p.Name).FirstAsync(ct);
                warnings.Add($"{name}: stock is now {onHand:0.###}. Record the missing production or adjust it.");
            }
        }

        return warnings;
    }
}
