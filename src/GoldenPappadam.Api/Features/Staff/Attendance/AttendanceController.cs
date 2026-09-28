using GoldenPappadam.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Staff.Attendance;

/// <summary>Admin-only, like every endpoint without an attribute (the fallback policy).</summary>
[ApiController]
[Route("api/staff")]
public class AttendanceController(AttendanceService attendance) : ControllerBase
{
    /// <summary>The register for one day (today when omitted).</summary>
    [HttpGet("attendance")]
    public Task<AttendanceSheetDto> GetSheet(DateOnly? date = null, CancellationToken ct = default) =>
        attendance.GetSheetAsync(date ?? IndiaTime.Today(), ct);

    [HttpPut("attendance")]
    public Task<SaveAttendanceResponse> Save(SaveAttendanceRequest request, CancellationToken ct) =>
        attendance.SaveAsync(request, ct);

    [HttpGet("attendance-statuses")]
    public Task<IReadOnlyList<AttendanceStatusDto>> GetStatuses(CancellationToken ct) =>
        attendance.GetStatusesAsync(ct);

    /// <summary>What a status is worth, as a fraction of a day's wage.</summary>
    [HttpPut("attendance-statuses/{id:guid}")]
    public Task<AttendanceStatusDto> UpdateStatus(Guid id, UpdateAttendanceStatusRequest request, CancellationToken ct) =>
        attendance.UpdateStatusAsync(id, request.DayFraction, ct);
}
