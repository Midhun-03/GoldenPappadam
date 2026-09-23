using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Sales.CustomerPrices;

/// <summary>
/// Every rate change, by the office or the sales team, so the office can review what salesmen set
/// and correct it. Admin-only through the fallback policy.
/// </summary>
[ApiController]
[Route("api/sales/customer-price-changes")]
public class CustomerPriceChangesController(CustomerPriceService prices) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CustomerPriceChangeDto>> GetAll(
        Guid? customerId = null,
        bool salespersonOnly = false,
        DateOnly? from = null,
        DateOnly? to = null,
        int limit = 200,
        CancellationToken ct = default) =>
        prices.GetChangesAsync(customerId, salespersonOnly, from, to, limit, ct);
}
