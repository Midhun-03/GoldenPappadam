using GoldenPappadam.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Mobile;

/// <summary>
/// Everything the salesperson's phone can reach, and nothing else. This is the whole of their
/// surface: there is no endpoint here that sets a price, moves stock, edits a shop or cancels a
/// bill, so those things are not hidden from the app - they do not exist for it.
/// </summary>
[ApiController]
[Route("api/mobile")]
[Authorize(Policy = Policies.FieldSales)]
public class MobileController(MobileSyncService sync) : ControllerBase
{
    /// <summary>Introduces this handset, and says which van it rides in.</summary>
    [HttpPost("devices/register")]
    public Task<DeviceDto> Register(RegisterDeviceRequest request, CancellationToken ct) =>
        sync.RegisterAsync(request, ct);

    /// <summary>Everything needed to work with no signal: shops, balances, products and prices.</summary>
    [HttpGet("sync/snapshot")]
    public Task<SnapshotDto> Snapshot(CancellationToken ct) => sync.GetSnapshotAsync(ct);

    /// <summary>
    /// Uploads what the salesperson did. Safe to send twice: an item already accepted comes back
    /// as AlreadyAccepted with the record it made the first time, never as a second bill.
    /// </summary>
    [HttpPost("sync/submissions")]
    public Task<SubmissionBatchResponse> Submit(SubmissionBatchRequest request, CancellationToken ct) =>
        sync.SubmitAsync(request, ct);

    /// <summary>The salesperson's own day, for the app's summary screen.</summary>
    [HttpGet("day")]
    public Task<MobileDayDto> Day(DateOnly? businessDate = null, CancellationToken ct = default) =>
        sync.GetDayAsync(businessDate ?? IndiaTime.Today(), ct);
}
