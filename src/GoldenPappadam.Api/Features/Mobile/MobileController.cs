using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.FieldSales.VanLoads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Mobile;

/// <summary>
/// Everything the salesperson's phone can reach, and nothing else. Salesmen acquire shops, so the
/// sync batch lets them create customers and branches and set customer rates (all audited). What
/// they cannot do has no route here at all: cancel a bill, deactivate anything, set an opening
/// balance, move stock other than onto their own van, or touch product master data.
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

    /// <summary>
    /// What is on this phone's van: loaded today, sold, and what is left. Read-only - the only way
    /// a salesperson changes van stock is by recording a load through the submission batch.
    /// </summary>
    [HttpGet("van-stock")]
    public Task<VanReconciliationDto> VanStock(DateOnly? businessDate = null, CancellationToken ct = default) =>
        sync.GetVanStockAsync(businessDate ?? IndiaTime.Today(), ct);

    /// <summary>The salesperson's own day, for the app's summary screen.</summary>
    [HttpGet("day")]
    public Task<MobileDayDto> Day(DateOnly? businessDate = null, CancellationToken ct = default) =>
        sync.GetDayAsync(businessDate ?? IndiaTime.Today(), ct);
}
