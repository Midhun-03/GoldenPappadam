using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

[ApiController]
[Route("api/inventory/stock")]
public class StockController(StockService stock) : ControllerBase
{
    /// <summary>Current stock for every product, with the low-stock flag.</summary>
    [HttpGet]
    public Task<IReadOnlyList<StockOnHandDto>> GetOnHand(
        Guid? categoryId = null,
        bool lowStockOnly = false,
        bool includeInactive = false,
        CancellationToken ct = default) =>
        stock.GetOnHandAsync(categoryId, lowStockOnly, includeInactive, ct);

    /// <summary>Every movement for one product, with a running balance.</summary>
    [HttpGet("{productId:guid}/movements")]
    public Task<IReadOnlyList<StockMovementDto>> GetMovements(
        Guid productId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default) =>
        stock.GetMovementsAsync(productId, from, to, ct);

    /// <summary>Opening stock, production output or damage.</summary>
    [HttpPost("entries")]
    public Task<StockEntryResponse> AddEntry(CreateStockEntryRequest request, CancellationToken ct) =>
        stock.AddEntryAsync(request, ct);

    /// <summary>Correction after a physical count.</summary>
    [HttpPost("adjustments")]
    public Task<StockEntryResponse> Adjust(AdjustStockRequest request, CancellationToken ct) =>
        stock.AdjustToCountAsync(request, ct);
}
