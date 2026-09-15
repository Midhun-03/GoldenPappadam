using GoldenPappadam.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.FieldSales.VanLoads;

/// <summary>
/// The van's stock. Admin-only through the fallback policy, deliberately: the office records what
/// goes onto the van and what comes back, and the salesperson has no endpoint that moves stock.
/// </summary>
[ApiController]
[Route("api/fieldsales/van-loads")]
public class VanLoadsController(VanLoadService vanLoads) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<VanLoadDto>> GetAll(
        Guid? vanLocationId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default) =>
        vanLoads.GetAllAsync(vanLocationId, from, to, ct);

    [HttpGet("{id:guid}")]
    public Task<VanLoadDto> GetById(Guid id, CancellationToken ct) => vanLoads.GetAsync(id, ct);

    /// <summary>Loads the van in the morning, or takes back what did not sell.</summary>
    [HttpPost]
    public Task<CreateVanLoadResponse> Create(CreateVanLoadRequest request, CancellationToken ct) =>
        vanLoads.CreateAsync(request, ct);

    /// <summary>What is on the van right now, which is what the evening return hands back.</summary>
    [HttpGet("on-van/{vanLocationId:guid}")]
    public Task<IReadOnlyList<VanLoadLineDto>> GetOnVan(Guid vanLocationId, CancellationToken ct) =>
        vanLoads.GetOnVanAsync(vanLocationId, ct);

    /// <summary>
    /// The day's account for one van: loaded, sold, returned, and what is left over. Anything left
    /// over is shown, never corrected automatically - a sale nobody wrote down and a miscount look
    /// the same to the arithmetic.
    /// </summary>
    [HttpGet("reconciliation")]
    public Task<VanReconciliationDto> GetReconciliation(
        Guid vanLocationId,
        DateOnly? businessDate = null,
        CancellationToken ct = default) =>
        vanLoads.GetReconciliationAsync(vanLocationId, businessDate ?? IndiaTime.Today(), ct);
}
