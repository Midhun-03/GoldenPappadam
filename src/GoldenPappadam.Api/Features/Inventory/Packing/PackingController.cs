using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Inventory.Packing;

[ApiController]
[Route("api/inventory/packing")]
public class PackingController(PackingService packing) : ControllerBase
{
    /// <summary>Record one packing operation: source stock down, packs up.</summary>
    [HttpPost]
    public Task<PackingResponse> Create(CreatePackingRequest request, CancellationToken ct) =>
        packing.CreateAsync(request, ct);

    /// <summary>What a packing would use and leave, worked out by the same code that saves it.</summary>
    [HttpPost("preview")]
    public Task<PackingPlanDto> Preview(PackingPreviewRequest request, CancellationToken ct) =>
        packing.PreviewAsync(request, ct);

    [HttpGet]
    public Task<IReadOnlyList<PackingEntryDto>> GetHistory(
        DateTime? from = null,
        DateTime? to = null,
        Guid? packedProductId = null,
        CancellationToken ct = default) =>
        packing.GetHistoryAsync(from, to, packedProductId, ct);
}
