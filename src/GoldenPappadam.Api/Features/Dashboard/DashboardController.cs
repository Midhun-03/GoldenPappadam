using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Dashboard;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> GetSummary(CancellationToken ct) => dashboard.GetSummaryAsync(ct);

    /// <summary>
    /// Sales per product between two business days, for the dashboard's product and category
    /// charts. The invoice list carries no lines, so these totals cannot be built on the client.
    /// </summary>
    [HttpGet("product-sales")]
    public Task<IReadOnlyList<ProductSalesDto>> GetProductSales(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct) => dashboard.GetProductSalesAsync(from, to, ct);
}
