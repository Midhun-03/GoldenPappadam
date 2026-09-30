using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Sales.CustomerPrices;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Identity;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.RateRequests;

/// <summary>
/// A salesperson asks for a customer's rate to change; an admin approves or rejects it (CLAUDE.md §4
/// "Rate-change approval", 2026-09-30). Until it is approved the customer's rate stays as it was, and
/// bills use it. A newer request for the same customer and product replaces one still pending.
/// </summary>
public class RateRequestService(AppDbContext db, CustomerPriceService prices, ICurrentUser currentUser)
{
    /// <summary>
    /// Records a request, replacing a pending one for the same customer and product. The id is the
    /// phone's, so the salesperson can withdraw it later; asked for twice, it is recorded once.
    /// </summary>
    public async Task<CustomerRateRequest> CreateAsync(
        Guid id,
        Guid customerId,
        Guid productId,
        decimal requestedPrice,
        string? reason,
        DateTime requestedAt,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A rate-change request needs an id from the phone.");
        }

        if (await db.CustomerRateRequests.FirstOrDefaultAsync(r => r.Id == id, ct) is { } already)
        {
            return already;
        }

        if (requestedPrice <= 0m)
        {
            throw new DomainException("A rate must be more than zero.");
        }

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct)
                       ?? throw new NotFoundException("Customer");
        if (!customer.IsActive)
        {
            throw new DomainException($"Customer '{customer.Name}' was deactivated by the office.");
        }

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product");
        if (!product.IsActive)
        {
            throw new DomainException($"Product '{product.Name}' is not active.");
        }

        var current = await db.CustomerPrices
            .Where(cp => cp.CustomerId == customerId && cp.ProductId == productId && cp.IsActive)
            .Select(cp => (decimal?)cp.UnitPrice)
            .FirstOrDefaultAsync(ct);

        var request = new CustomerRateRequest
        {
            Id = id,
            CustomerId = customerId,
            ProductId = productId,
            PriceWhenRequested = current,
            RequestedPrice = requestedPrice,
            RequestedAt = requestedAt.ToUniversalTime(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // The older pending request steps aside first: only one may be waiting per customer and product.
        var older = await db.CustomerRateRequests
            .FirstOrDefaultAsync(r => r.CustomerId == customerId && r.ProductId == productId &&
                                      r.Status == RateRequestStatus.Pending, ct);
        if (older is not null)
        {
            older.Status = RateRequestStatus.Cancelled;
            older.DecidedAt = DateTime.UtcNow;
            older.DecisionNote = "Replaced by a newer request.";
            await db.SaveChangesAsync(ct);
        }

        db.CustomerRateRequests.Add(request);
        await db.SaveChangesAsync(ct);

        if (older is not null)
        {
            older.ReplacedById = request.Id;
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);

        return request;
    }

    /// <summary>The salesperson withdraws their own request, while the office has not decided it.</summary>
    public async Task<CustomerRateRequest> CancelAsync(Guid id, CancellationToken ct)
    {
        var request = await db.CustomerRateRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new NotFoundException("Rate-change request");

        if (request.CreatedBy != currentUser.UserId)
        {
            throw new DomainException("Only the salesperson who sent a request can withdraw it.");
        }

        // Withdrawn twice is withdrawn once.
        if (request.Status == RateRequestStatus.Cancelled)
        {
            return request;
        }

        if (request.Status != RateRequestStatus.Pending)
        {
            throw new DomainException($"The office has already {request.Status.ToString().ToLowerInvariant()} this request.");
        }

        request.Status = RateRequestStatus.Cancelled;
        request.DecidedBy = currentUser.UserId;
        request.DecidedAt = DateTime.UtcNow;
        request.DecisionNote = "Withdrawn by the salesperson.";
        await db.SaveChangesAsync(ct);

        return request;
    }

    /// <summary>
    /// The office agrees: the customer's rate becomes the requested one, from the next bill, recorded in
    /// the rate history with this admin and the request. Rate and decision are saved together.
    /// </summary>
    public async Task<RateRequestDto> ApproveAsync(Guid id, string? note, CancellationToken ct)
    {
        var request = await PendingAsync(id, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await prices.ApplyApprovedRequestAsync(request, ct);
        Decide(request, RateRequestStatus.Approved, note);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>The office says no: the rate stays as it is.</summary>
    public async Task<RateRequestDto> RejectAsync(Guid id, string? note, CancellationToken ct)
    {
        var request = await PendingAsync(id, ct);

        Decide(request, RateRequestStatus.Rejected, note);
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<RateRequestDto> GetAsync(Guid id, CancellationToken ct) =>
        await RateRequestQueries.Project(db.CustomerRateRequests.Where(r => r.Id == id), db).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Rate-change request");

    private async Task<CustomerRateRequest> PendingAsync(Guid id, CancellationToken ct)
    {
        var request = await db.CustomerRateRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new NotFoundException("Rate-change request");

        return request.Status == RateRequestStatus.Pending
            ? request
            : throw new DomainException($"This request is already {request.Status.ToString().ToLowerInvariant()}.");
    }

    private void Decide(CustomerRateRequest request, RateRequestStatus status, string? note)
    {
        request.Status = status;
        request.DecidedBy = currentUser.UserId;
        request.DecidedAt = DateTime.UtcNow;
        request.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
