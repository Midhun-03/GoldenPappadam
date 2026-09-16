using GoldenPappadam.Domain.FieldSales;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.FieldSales.StockRequests;

/// <summary>
/// What the sales team has asked the packing unit for. Admin-only through the fallback policy: the
/// salesperson raises requests through their own endpoint, and does not decide what gets packed.
/// </summary>
[ApiController]
[Route("api/fieldsales/stock-requests")]
public class StockRequestsController(StockRequestService requests) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<StockRequestDto>> GetAll(
        DateOnly? from = null,
        DateOnly? to = null,
        StockRequestStatus? status = null,
        CancellationToken ct = default) =>
        requests.GetAllAsync(from, to, status, ct);

    [HttpGet("{id:guid}")]
    public Task<StockRequestDto> GetById(Guid id, CancellationToken ct) => requests.GetAsync(id, ct);

    /// <summary>Everything asked for, added up per product per day, for the packing table.</summary>
    [HttpGet("packing-needs")]
    public Task<IReadOnlyList<PackingNeedDto>> GetPackingNeeds(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default) =>
        requests.GetPackingNeedsAsync(from, to, ct);

    /// <summary>The office answering: packed, or not being packed.</summary>
    [HttpPost("{id:guid}/status")]
    public Task<StockRequestDto> SetStatus(Guid id, StockRequestStatus status, CancellationToken ct) =>
        requests.SetStatusAsync(id, status, ct);
}
