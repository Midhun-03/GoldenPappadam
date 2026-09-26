using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

[ApiController]
[Route("api/inventory/stock")]
public class StockController(AppDbContext db, StockService stock, StockAgeService ages) : ControllerBase
{
    /// <summary>
    /// Current stock for every product, with the low-stock flag. Defaults to the main warehouse,
    /// which is what packing draws from and what low stock has always meant; pass a location to see
    /// the van, or allLocations to see what the business owns in total.
    /// </summary>
    [HttpGet]
    public Task<IReadOnlyList<StockOnHandDto>> GetOnHand(
        Guid? categoryId = null,
        bool lowStockOnly = false,
        bool includeInactive = false,
        Guid? locationId = null,
        bool allLocations = false,
        CancellationToken ct = default) =>
        stock.GetOnHandAsync(
            categoryId,
            lowStockOnly,
            includeInactive,
            allLocations ? null : locationId ?? KnownStockLocations.MainWarehouseId,
            ct);

    /// <summary>
    /// Every movement for one product, with a running balance. Defaults to every location, because
    /// the history of a packet is easier to follow when the transfer onto the van is in it.
    /// </summary>
    [HttpGet("{productId:guid}/movements")]
    public Task<IReadOnlyList<StockMovementDto>> GetMovements(
        Guid productId,
        DateTime? from = null,
        DateTime? to = null,
        Guid? locationId = null,
        CancellationToken ct = default) =>
        stock.GetMovementsAsync(productId, from, to, locationId, ct);

    /// <summary>Where one product's stock is: how much in the warehouse, how much on the van.</summary>
    [HttpGet("{productId:guid}/locations")]
    public Task<IReadOnlyList<LocationStockDto>> GetByLocation(Guid productId, CancellationToken ct) =>
        stock.GetByLocationAsync(productId, ct);

    /// <summary>The places stock can be.</summary>
    [HttpGet("locations")]
    public async Task<IReadOnlyList<StockLocationDto>> GetLocations(
        bool includeInactive = false,
        CancellationToken ct = default) =>
        await db.StockLocations
            .Where(l => includeInactive || l.IsActive)
            .OrderBy(l => l.Kind)
            .ThenBy(l => l.Code)
            .Select(l => new StockLocationDto(l.Id, l.Code, l.Name, l.Kind, l.IsActive))
            .ToListAsync(ct);

    /// <summary>Opening stock, production output or damage.</summary>
    [HttpPost("entries")]
    public Task<StockEntryResponse> AddEntry(CreateStockEntryRequest request, CancellationToken ct) =>
        stock.AddEntryAsync(request, ct);

    /// <summary>Correction after a physical count, at the warehouse or on the van.</summary>
    [HttpPost("adjustments")]
    public Task<StockEntryResponse> Adjust(AdjustStockRequest request, CancellationToken ct) =>
        stock.AdjustToCountAsync(request, ct);

    /// <summary>How old the stock is, per product and place, for products with a shelf life.</summary>
    [HttpGet("age")]
    public Task<IReadOnlyList<StockAgeRowDto>> GetAge(DateOnly? asOf = null, CancellationToken ct = default) =>
        ages.GetAsync(asOf ?? IndiaTime.Today(), ct);

    /// <summary>Writes off what has expired in one place as damage. Only ever on a person's say-so.</summary>
    [HttpPost("age/write-off")]
    public Task<StockEntryResponse> WriteOffExpired(WriteOffExpiredRequest request, CancellationToken ct) =>
        ages.WriteOffExpiredAsync(request.ProductId, request.LocationId, ct);
}
