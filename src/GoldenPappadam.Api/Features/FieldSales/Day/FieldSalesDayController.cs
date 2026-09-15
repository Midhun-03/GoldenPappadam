using GoldenPappadam.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.FieldSales.Day;

/// <summary>What the salesperson did today, for the office. Admin-only through the fallback policy.</summary>
[ApiController]
[Route("api/fieldsales/day")]
public class FieldSalesDayController(FieldSalesDayService day) : ControllerBase
{
    [HttpGet]
    public Task<FieldSalesDayDto> Get(DateOnly? businessDate = null, CancellationToken ct = default) =>
        day.GetAsync(businessDate ?? IndiaTime.Today(), ct);
}
