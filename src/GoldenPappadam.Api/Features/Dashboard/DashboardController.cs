using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Dashboard;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> GetSummary(CancellationToken ct) => dashboard.GetSummaryAsync(ct);
}
