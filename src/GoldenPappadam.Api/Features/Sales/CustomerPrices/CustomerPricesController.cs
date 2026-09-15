using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Sales.CustomerPrices;

/// <summary>
/// What each shop pays. Admin-only through the fallback policy, and deliberately so: this is the
/// one place a price can be set, and the salesperson's app has no way to reach it.
/// </summary>
[ApiController]
[Route("api/sales/customers/{customerId:guid}/prices")]
public class CustomerPricesController(CustomerPriceService prices) : ControllerBase
{
    /// <summary>
    /// Every sellable product as this shop sees it. Pass agreedOnly to list just the arrangements
    /// rather than the whole catalogue.
    /// </summary>
    [HttpGet]
    public Task<IReadOnlyList<CustomerPriceDto>> GetAll(
        Guid customerId,
        bool agreedOnly = false,
        CancellationToken ct = default) =>
        prices.GetForCustomerAsync(customerId, agreedOnly, ct);

    [HttpPut("{productId:guid}")]
    public Task<CustomerPriceDto> Set(
        Guid customerId,
        Guid productId,
        SetCustomerPriceRequest request,
        CancellationToken ct) =>
        prices.SetAsync(customerId, productId, request.UnitPrice, ct);

    /// <summary>
    /// Ends the arrangement: the shop goes back to the product's own price. The row is kept and
    /// deactivated, so what was once agreed is still on record.
    /// </summary>
    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(Guid customerId, Guid productId, CancellationToken ct)
    {
        await prices.RemoveAsync(customerId, productId, ct);

        return NoContent();
    }
}
