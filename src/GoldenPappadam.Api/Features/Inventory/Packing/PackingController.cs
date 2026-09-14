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

    [HttpGet]
    public Task<IReadOnlyList<PackingEntryDto>> GetHistory(
        DateTime? from = null,
        DateTime? to = null,
        Guid? packedProductId = null,
        CancellationToken ct = default) =>
        packing.GetHistoryAsync(from, to, packedProductId, ct);
}
