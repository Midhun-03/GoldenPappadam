using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Inventory.Repacking;

/// <summary>Admin-only through the fallback policy: repacking is warehouse work.</summary>
[ApiController]
[Route("api/inventory/repacking")]
public class RepackingController(RepackingService repacking) : ControllerBase
{
    /// <summary>What the repack would make, without saving anything.</summary>
    [HttpPost("preview")]
    public Task<RepackPlanDto> Preview(RepackRequest request, CancellationToken ct) => repacking.PreviewAsync(request, ct);

    [HttpPost]
    public Task<RepackResponse> Create(RepackRequest request, CancellationToken ct) => repacking.CreateAsync(request, ct);

    [HttpGet]
    public Task<IReadOnlyList<RepackEntryDto>> GetHistory(CancellationToken ct) => repacking.GetHistoryAsync(ct);
}
