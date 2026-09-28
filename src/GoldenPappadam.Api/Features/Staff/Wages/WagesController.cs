using GoldenPappadam.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Staff.Wages;

/// <summary>Admin-only, like every endpoint without an attribute (the fallback policy).</summary>
[ApiController]
[Route("api/staff")]
public class WagesController(WagePaymentService wages) : ControllerBase
{
    /// <summary>The Sunday-to-Saturday week containing <paramref name="date"/> (today when omitted).</summary>
    [HttpGet("wages/week")]
    public Task<WageWeekDto> GetWeek(DateOnly? date = null, CancellationToken ct = default) =>
        wages.GetWeekAsync(date ?? IndiaTime.Today(), ct);

    [HttpGet("wage-payments")]
    public Task<IReadOnlyList<WagePaymentDto>> GetPayments(
        Guid? employeeId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default) =>
        wages.GetAllAsync(employeeId, from, to, ct);

    [HttpGet("wage-payments/{id:guid}")]
    public Task<WagePaymentDto> GetPayment(Guid id, CancellationToken ct) => wages.GetAsync(id, ct);

    /// <summary>Marks one or more employees' wages for a week paid, and records the expense.</summary>
    [HttpPost("wage-payments")]
    public Task<PayWagesResponse> Pay(PayWagesRequest request, CancellationToken ct) => wages.PayAsync(request, ct);

    [HttpPost("wage-payments/{id:guid}/cancel")]
    public Task<WagePaymentDto> Cancel(Guid id, CancelWagePaymentRequest request, CancellationToken ct) =>
        wages.CancelAsync(id, request, ct);
}
