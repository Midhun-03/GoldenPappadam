using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.OwnShop.Transfers;

/// <summary>Admin-only through the fallback policy: the own shop is the office's for now.</summary>
[ApiController]
[Route("api/own-shop/transfers")]
public class ShopTransfersController(ShopTransferService transfers) : ControllerBase
{
    /// <summary>Loose pappadam from the factory to the shop: kg down there, pieces up here.</summary>
    [HttpPost]
    public Task<ShopTransferDto> Create(CreateShopTransferRequest request, CancellationToken ct) =>
        transfers.CreateAsync(request, ct);

    /// <summary>What a transfer would do, worked out by the same code that saves it.</summary>
    [HttpPost("preview")]
    public Task<ShopTransferPlanDto> Preview(ShopTransferPreviewRequest request, CancellationToken ct) =>
        transfers.PreviewAsync(request, ct);

    [HttpGet]
    public Task<IReadOnlyList<ShopTransferDto>> GetHistory(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        transfers.GetHistoryAsync(from, to, ct);
}
