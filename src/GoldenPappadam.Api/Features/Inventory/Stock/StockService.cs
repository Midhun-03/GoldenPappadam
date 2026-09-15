using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

/// <summary>
/// Stock is the sum of the movement ledger; nothing stores a running total.
/// Shortfalls warn instead of blocking, so data entry never stops a real delivery.
///
/// Every figure is <em>per location</em>. Passing null for a location means "everywhere", which is
/// what the business owns; passing a location means what is actually in that place, which is what
/// packing and loading care about. Callers say which they mean rather than inheriting a default,
/// because the two answers differ the moment the van is loaded.
/// </summary>
public class StockService(AppDbContext db)
{
    /// <summary>Movement types a user may enter by hand. Sale, Packing and Transfer come from their own documents.</summary>
    private static readonly StockMovementType[] ManualEntryTypes =
    [
        StockMovementType.Opening,
        StockMovementType.Production,
        StockMovementType.Damage
    ];

    public async Task<decimal> GetQuantityOnHandAsync(Guid productId, Guid? locationId, CancellationToken ct) =>
        await db.StockMovements
            .Where(m => m.ProductId == productId)
            .Where(m => locationId == null || m.LocationId == locationId)
            .SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;

    public async Task<IReadOnlyList<StockOnHandDto>> GetOnHandAsync(
        Guid? categoryId,
        bool lowStockOnly,
        bool includeInactive,
        Guid? locationId,
        CancellationToken ct)
    {
        await EnsureLocationExistsAsync(locationId, ct);

        // One correlated subquery per product: products are counted in tens, not millions,
        // and it keeps the low-stock rule in a single place below.
        var rows = await db.Products
            .Where(p => includeInactive || p.IsActive)
            .Where(p => categoryId == null || p.CategoryId == categoryId)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.Name,
                p.Kind,
                UnitCode = p.UnitOfMeasure!.Code,
                Quantity = db.StockMovements
                    .Where(m => m.ProductId == p.Id)
                    .Where(m => locationId == null || m.LocationId == locationId)
                    .Sum(m => (decimal?)m.Quantity) ?? 0m,
                p.LowStockThreshold,
                p.IsActive
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new StockOnHandDto(
                r.Id,
                r.ProductCode,
                r.Name,
                r.Kind,
                r.UnitCode,
                r.Quantity,
                r.LowStockThreshold,
                r.LowStockThreshold != null && r.Quantity <= r.LowStockThreshold,
                r.IsActive))
            .Where(r => !lowStockOnly || r.IsLowStock)
            .ToList();
    }

    public async Task<IReadOnlyList<StockMovementDto>> GetMovementsAsync(
        Guid productId,
        DateTime? from,
        DateTime? to,
        Guid? locationId,
        CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product");
        }

        await EnsureLocationExistsAsync(locationId, ct);

        // Everything before the window still counts towards the running balance shown in it.
        var openingBalance = from is null
            ? 0m
            : await db.StockMovements
                .Where(m => m.ProductId == productId && m.OccurredAt < from)
                .Where(m => locationId == null || m.LocationId == locationId)
                .SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;

        var movements = await db.StockMovements
            .Where(m => m.ProductId == productId)
            .Where(m => locationId == null || m.LocationId == locationId)
            .Where(m => from == null || m.OccurredAt >= from)
            .Where(m => to == null || m.OccurredAt <= to)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.OccurredAt,
                m.MovementType,
                m.Quantity,
                m.LocationId,
                LocationCode = m.Location!.Code,
                m.ReferenceType,
                m.ReferenceId,
                m.Notes
            })
            .ToListAsync(ct);

        var running = openingBalance;

        return movements
            .Select(m =>
            {
                running += m.Quantity;
                return new StockMovementDto(
                    m.Id,
                    m.OccurredAt,
                    m.MovementType,
                    m.Quantity,
                    running,
                    m.LocationId,
                    m.LocationCode,
                    m.ReferenceType,
                    m.ReferenceId,
                    m.Notes);
            })
            .ToList();
    }

    /// <summary>What each location is holding of one product. The van's own count, in other words.</summary>
    public async Task<IReadOnlyList<LocationStockDto>> GetByLocationAsync(Guid productId, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product");
        }

        var rows = await db.StockLocations
            .Where(l => l.IsActive)
            .OrderBy(l => l.Kind)
            .ThenBy(l => l.Code)
            .Select(l => new LocationStockDto(
                l.Id,
                l.Code,
                l.Name,
                l.Kind,
                db.StockMovements
                    .Where(m => m.ProductId == productId && m.LocationId == l.Id)
                    .Sum(m => (decimal?)m.Quantity) ?? 0m))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<StockEntryResponse> AddEntryAsync(CreateStockEntryRequest request, CancellationToken ct)
    {
        if (!ManualEntryTypes.Contains(request.MovementType))
        {
            throw new DomainException(
                $"{request.MovementType} movements are created by their own document, not entered by hand. " +
                $"Allowed here: {string.Join(", ", ManualEntryTypes)}.");
        }

        if (request.MovementType == StockMovementType.Damage && string.IsNullOrWhiteSpace(request.Notes))
        {
            throw new DomainException("Damage entries need a note explaining what happened.");
        }

        var product = await FindActiveProductAsync(request.ProductId, ct);
        var locationId = await ResolveLocationAsync(request.LocationId, ct);

        if (request.MovementType == StockMovementType.Opening &&
            await db.StockMovements.AnyAsync(m => m.ProductId == product.Id && m.LocationId == locationId, ct))
        {
            throw new DomainException(
                "Opening stock can only be recorded before this product has any other movement here. " +
                "Use an adjustment instead.");
        }

        if (request.MovementType == StockMovementType.Production && !await IsWarehouseAsync(locationId, ct))
        {
            throw new DomainException("Production is recorded at a warehouse, not on a van.");
        }

        var quantity = request.MovementType == StockMovementType.Damage ? -request.Quantity : request.Quantity;

        return await SaveMovementAsync(
            product.Id, locationId, request.MovementType, quantity, request.OccurredAt, request.Notes, ct);
    }

    public async Task<StockEntryResponse> AdjustToCountAsync(AdjustStockRequest request, CancellationToken ct)
    {
        var product = await FindActiveProductAsync(request.ProductId, ct);
        var locationId = await ResolveLocationAsync(request.LocationId, ct);
        var onHand = await GetQuantityOnHandAsync(product.Id, locationId, ct);
        var difference = request.CountedQuantity - onHand;

        if (difference == 0m)
        {
            throw new DomainException("The counted quantity already matches the recorded stock.");
        }

        return await SaveMovementAsync(
            product.Id, locationId, StockMovementType.Adjustment, difference, request.OccurredAt, request.Notes, ct);
    }

    private async Task<StockEntryResponse> SaveMovementAsync(
        Guid productId,
        Guid locationId,
        StockMovementType type,
        decimal quantity,
        DateTime? occurredAt,
        string? notes,
        CancellationToken ct)
    {
        var movement = new StockMovement
        {
            ProductId = productId,
            LocationId = locationId,
            MovementType = type,
            Quantity = quantity,
            OccurredAt = occurredAt?.ToUniversalTime() ?? DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);

        var onHand = await GetQuantityOnHandAsync(productId, locationId, ct);

        return new StockEntryResponse(movement.Id, productId, locationId, onHand, WarningFor(onHand));
    }

    private async Task<Product> FindActiveProductAsync(Guid productId, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product");

        if (!product.IsActive)
        {
            throw new DomainException($"Product '{product.Name}' is not active.");
        }

        return product;
    }

    /// <summary>
    /// Falls back to the main warehouse, which is where everything happened before there were
    /// locations at all, so an older client that sends nothing keeps working unchanged.
    /// </summary>
    internal async Task<Guid> ResolveLocationAsync(Guid? locationId, CancellationToken ct)
    {
        if (locationId is not { } id)
        {
            return KnownStockLocations.MainWarehouseId;
        }

        var location = await db.StockLocations.FirstOrDefaultAsync(l => l.Id == id, ct)
                       ?? throw new NotFoundException("Stock location");

        if (!location.IsActive)
        {
            throw new DomainException($"Location '{location.Name}' is not active.");
        }

        return location.Id;
    }

    private async Task EnsureLocationExistsAsync(Guid? locationId, CancellationToken ct)
    {
        if (locationId is { } id && !await db.StockLocations.AnyAsync(l => l.Id == id, ct))
        {
            throw new NotFoundException("Stock location");
        }
    }

    private async Task<bool> IsWarehouseAsync(Guid locationId, CancellationToken ct) =>
        await db.StockLocations
            .AnyAsync(l => l.Id == locationId && l.Kind == StockLocationKind.Warehouse, ct);

    internal static string? WarningFor(decimal quantityOnHand) =>
        quantityOnHand < 0
            ? $"Stock is now negative ({quantityOnHand:0.###}). Record the missing production or correct it with an adjustment."
            : null;
}
