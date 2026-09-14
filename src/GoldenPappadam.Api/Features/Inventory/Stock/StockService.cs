using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

/// <summary>
/// Stock is the sum of the movement ledger; nothing stores a running total.
/// Shortfalls warn instead of blocking, so data entry never stops a real delivery.
/// </summary>
public class StockService(AppDbContext db)
{
    /// <summary>Movement types a user may enter by hand. Sale and Packing come from their own documents.</summary>
    private static readonly StockMovementType[] ManualEntryTypes =
    [
        StockMovementType.Opening,
        StockMovementType.Production,
        StockMovementType.Damage
    ];

    public async Task<decimal> GetQuantityOnHandAsync(Guid productId, CancellationToken ct) =>
        await db.StockMovements
            .Where(m => m.ProductId == productId)
            .SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;

    public async Task<IReadOnlyList<StockOnHandDto>> GetOnHandAsync(
        Guid? categoryId,
        bool lowStockOnly,
        bool includeInactive,
        CancellationToken ct)
    {
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
        CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            throw new NotFoundException("Product");
        }

        // Everything before the window still counts towards the running balance shown in it.
        var openingBalance = from is null
            ? 0m
            : await db.StockMovements
                .Where(m => m.ProductId == productId && m.OccurredAt < from)
                .SumAsync(m => (decimal?)m.Quantity, ct) ?? 0m;

        var movements = await db.StockMovements
            .Where(m => m.ProductId == productId)
            .Where(m => from == null || m.OccurredAt >= from)
            .Where(m => to == null || m.OccurredAt <= to)
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id, m.OccurredAt, m.MovementType, m.Quantity, m.ReferenceType, m.ReferenceId, m.Notes
            })
            .ToListAsync(ct);

        var running = openingBalance;

        return movements
            .Select(m =>
            {
                running += m.Quantity;
                return new StockMovementDto(
                    m.Id, m.OccurredAt, m.MovementType, m.Quantity, running, m.ReferenceType, m.ReferenceId, m.Notes);
            })
            .ToList();
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

        if (request.MovementType == StockMovementType.Opening &&
            await db.StockMovements.AnyAsync(m => m.ProductId == product.Id, ct))
        {
            throw new DomainException(
                "Opening stock can only be recorded before this product has any other movement. " +
                "Use an adjustment instead.");
        }

        var quantity = request.MovementType == StockMovementType.Damage ? -request.Quantity : request.Quantity;

        return await SaveMovementAsync(product.Id, request.MovementType, quantity, request.OccurredAt, request.Notes, ct);
    }

    public async Task<StockEntryResponse> AdjustToCountAsync(AdjustStockRequest request, CancellationToken ct)
    {
        var product = await FindActiveProductAsync(request.ProductId, ct);
        var onHand = await GetQuantityOnHandAsync(product.Id, ct);
        var difference = request.CountedQuantity - onHand;

        if (difference == 0m)
        {
            throw new DomainException("The counted quantity already matches the recorded stock.");
        }

        return await SaveMovementAsync(
            product.Id, StockMovementType.Adjustment, difference, request.OccurredAt, request.Notes, ct);
    }

    private async Task<StockEntryResponse> SaveMovementAsync(
        Guid productId,
        StockMovementType type,
        decimal quantity,
        DateTime? occurredAt,
        string? notes,
        CancellationToken ct)
    {
        var movement = new StockMovement
        {
            ProductId = productId,
            MovementType = type,
            Quantity = quantity,
            OccurredAt = occurredAt?.ToUniversalTime() ?? DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);

        var onHand = await GetQuantityOnHandAsync(productId, ct);

        return new StockEntryResponse(movement.Id, productId, onHand, WarningFor(onHand));
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

    internal static string? WarningFor(decimal quantityOnHand) =>
        quantityOnHand < 0
            ? $"Stock is now negative ({quantityOnHand:0.###}). Record the missing production or correct it with an adjustment."
            : null;
}
