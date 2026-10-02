using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.OwnShop.Sales;

/// <summary>Admin-only through the fallback policy: nothing here is open to a salesperson.</summary>
[ApiController]
[Route("api/own-shop/sales")]
public class ShopSalesController(ShopSaleService sales) : ControllerBase
{
    /// <summary>Newest first. from and to are IST business days.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ShopSaleListItemDto>> GetAll(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? customerId = null,
        Guid? productId = null,
        CancellationToken ct = default) =>
        sales.GetListAsync(from, to, customerId, productId, ct);

    [HttpGet("{id:guid}")]
    public Task<ShopSaleDetailDto> GetById(Guid id, CancellationToken ct) => sales.GetAsync(id, ct);

    /// <summary>A sale over the counter, paid in full: the pieces leave the shop's stock with it.</summary>
    [HttpPost]
    public Task<ShopSaleDetailDto> Create(CreateShopSaleRequest request, CancellationToken ct) =>
        sales.CreateAsync(request, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<ShopSaleDetailDto> Cancel(Guid id, CancelShopSaleRequest request, CancellationToken ct) =>
        sales.CancelAsync(id, request.Reason, ct);
}
