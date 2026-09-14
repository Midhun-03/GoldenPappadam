using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Payments;

public class PaymentService(AppDbContext db)
{
    public async Task<CreatePaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
                       ?? throw new NotFoundException("Customer");

        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' is not active.");
        }

        if (request.Amount <= 0m)
        {
            throw new DomainException("A payment must be greater than zero.");
        }

        var payment = new Payment
        {
            CustomerId = customer.Id,
            PaymentDate = request.PaymentDate ?? IndiaTime.Today(),
            Amount = request.Amount,
            Method = request.Method,
            Reference = Clean(request.Reference),
            Notes = Clean(request.Notes)
        };

        payment.Allocations = request.Allocations is { Count: > 0 }
            ? await BuildRequestedAllocationsAsync(customer.Id, request, ct)
            : await BuildOldestFirstAllocationsAsync(customer.Id, request.Amount, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var dto = await GetAsync(payment.Id, ct);

        return new CreatePaymentResponse(dto, await GetBalanceAsync(customer.Id, ct));
    }

    public async Task<PaymentDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.Payments.Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Payment");

    public async Task<IReadOnlyList<PaymentDto>> GetAllAsync(
        Guid? customerId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct) =>
        await Project(db.Payments
                .Where(p => customerId == null || p.CustomerId == customerId)
                .Where(p => from == null || p.PaymentDate >= from)
                .Where(p => to == null || p.PaymentDate <= to)
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.CreatedAt))
            .ToListAsync(ct);

    /// <summary>What the customer owes: opening balance plus bills, minus every payment received.</summary>
    public async Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken ct)
    {
        var opening = await db.Customers.Where(c => c.Id == customerId).Select(c => c.OpeningBalance).FirstAsync(ct);

        var billed = await db.Invoices
            .Where(i => i.CustomerId == customerId && i.Status == InvoiceStatus.Issued)
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m;

        var received = await db.Payments
            .Where(p => p.CustomerId == customerId)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return opening + billed - received;
    }

    private async Task<List<PaymentAllocation>> BuildRequestedAllocationsAsync(
        Guid customerId,
        CreatePaymentRequest request,
        CancellationToken ct)
    {
        var requested = request.Allocations!;

        if (requested.Select(a => a.InvoiceId).Distinct().Count() != requested.Count)
        {
            throw new DomainException("The same bill is listed twice. Combine the amounts into one line.");
        }

        if (requested.Sum(a => a.Amount) > request.Amount)
        {
            throw new DomainException("The amounts applied to bills add up to more than the payment.");
        }

        var allocations = new List<PaymentAllocation>();

        foreach (var line in requested)
        {
            var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == line.InvoiceId, ct)
                          ?? throw new NotFoundException("Invoice");

            if (invoice.CustomerId != customerId)
            {
                throw new DomainException($"Invoice {invoice.InvoiceNumber} belongs to a different customer.");
            }

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                throw new DomainException($"Invoice {invoice.InvoiceNumber} is cancelled.");
            }

            if (line.Amount <= 0m)
            {
                throw new DomainException("Every amount applied to a bill must be greater than zero.");
            }

            var outstanding = invoice.TotalAmount - await AllocatedToAsync(invoice.Id, ct);

            if (line.Amount > outstanding)
            {
                throw new DomainException(
                    $"Invoice {invoice.InvoiceNumber} only has {outstanding:0.00} outstanding.");
            }

            allocations.Add(new PaymentAllocation { InvoiceId = invoice.Id, Amount = line.Amount });
        }

        return allocations;
    }

    /// <summary>Applies the money to the oldest unpaid bills until it runs out.</summary>
    private async Task<List<PaymentAllocation>> BuildOldestFirstAllocationsAsync(
        Guid customerId,
        decimal amount,
        CancellationToken ct)
    {
        var invoices = await db.Invoices
            .Where(i => i.CustomerId == customerId && i.Status == InvoiceStatus.Issued)
            .OrderBy(i => i.InvoiceDate)
            .ThenBy(i => i.InvoiceNumber)
            .Select(i => new
            {
                i.Id,
                i.TotalAmount,
                Paid = db.PaymentAllocations.Where(a => a.InvoiceId == i.Id).Sum(a => (decimal?)a.Amount) ?? 0m
            })
            .ToListAsync(ct);

        var remaining = amount;
        var allocations = new List<PaymentAllocation>();

        foreach (var invoice in invoices)
        {
            if (remaining <= 0m) break;

            var outstanding = invoice.TotalAmount - invoice.Paid;
            if (outstanding <= 0m) continue;

            var applied = Math.Min(outstanding, remaining);
            allocations.Add(new PaymentAllocation { InvoiceId = invoice.Id, Amount = applied });
            remaining -= applied;
        }

        // Anything left over stays unallocated: money on account for the next bill.
        return allocations;
    }

    private async Task<decimal> AllocatedToAsync(Guid invoiceId, CancellationToken ct) =>
        await db.PaymentAllocations.Where(a => a.InvoiceId == invoiceId).SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

    private static IQueryable<PaymentDto> Project(IQueryable<Payment> payments) =>
        payments.Select(p => new PaymentDto(
            p.Id,
            p.CustomerId,
            p.Customer!.Name,
            p.PaymentDate,
            p.Amount,
            p.Method,
            p.Reference,
            p.Notes,
            p.Allocations.Sum(a => (decimal?)a.Amount) ?? 0m,
            p.Amount - (p.Allocations.Sum(a => (decimal?)a.Amount) ?? 0m),
            p.Allocations
                .Select(a => new PaymentAllocationDto(a.InvoiceId, a.Invoice!.InvoiceNumber, a.Amount))
                .ToList()));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
