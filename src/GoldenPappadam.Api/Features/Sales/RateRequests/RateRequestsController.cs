using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.RateRequests;

/// <summary>
/// Admin-only through the fallback policy: deciding a rate is the office's. The salesperson sends and
/// withdraws requests through the sync batch, never here.
/// </summary>
[ApiController]
[Route("api/sales/rate-requests")]
public class RateRequestsController(AppDbContext db, RateRequestService requests) : ControllerBase
{
    /// <summary>Pending first, oldest waiting first; then the rest, newest first.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<RateRequestDto>> GetAll(
        RateRequestStatus? status = null,
        Guid? customerId = null,
        CancellationToken ct = default) =>
        await RateRequestQueries.Project(
                db.CustomerRateRequests
                    .Where(r => status == null || r.Status == status)
                    .Where(r => customerId == null || r.CustomerId == customerId)
                    .OrderBy(r => r.Status == RateRequestStatus.Pending ? 0 : 1)
                    .ThenBy(r => r.Status == RateRequestStatus.Pending ? r.CreatedAt : DateTime.MaxValue)
                    .ThenByDescending(r => r.DecidedAt)
                    .Take(300),
                db)
            .ToListAsync(ct);

    [HttpPost("{id:guid}/approve")]
    public Task<RateRequestDto> Approve(Guid id, DecideRateRequest request, CancellationToken ct) =>
        requests.ApproveAsync(id, request.Note, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<RateRequestDto> Reject(Guid id, DecideRateRequest request, CancellationToken ct) =>
        requests.RejectAsync(id, request.Note, ct);
}
