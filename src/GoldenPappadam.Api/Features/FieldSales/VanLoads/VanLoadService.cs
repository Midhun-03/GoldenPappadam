using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Inventory.Stock;
using GoldenPappadam.Api.Features.OwnShop;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.FieldSales.VanLoads;

/// <summary>
/// Moving stock between the warehouse and the van, and answering the question that makes the whole
/// of phase 3 worth building: at the end of the day, does what went out match what was sold and
/// what came back?
/// </summary>
public class VanLoadService(AppDbContext db, StockService stock)
{
    public async Task<CreateVanLoadResponse> CreateAsync(
        CreateVanLoadRequest request,
        CancellationToken ct,
        Guid? deviceId = null)
    {
        var van = await FindLocationAsync(request.VanLocationId, ct);

        if (van.Kind != StockLocationKind.Van)
        {
            throw new DomainException($"'{van.Name}' is not a van.");
        }

        var warehouse = request.WarehouseLocationId is { } warehouseId
            ? await FindLocationAsync(warehouseId, ct)
            : await FindLocationAsync(KnownStockLocations.MainWarehouseId, ct);

        if (warehouse.Kind != StockLocationKind.Warehouse)
        {
            throw new DomainException($"'{warehouse.Name}' is not a warehouse.");
        }

        // Everything is checked before a single row is written, so a rejected load leaves nothing behind.
        var products = await ValidateLinesAsync(request.Lines, ct);

        var occurredAt = request.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var load = new VanLoad
        {
            VanLocationId = van.Id,
            WarehouseLocationId = warehouse.Id,
            DeviceId = deviceId,
            Direction = request.Direction,
            OccurredAt = occurredAt,
            BusinessDate = IndiaTime.ToIndiaDate(occurredAt),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lines = request.Lines
                .Select(line => new VanLoadLine { ProductId = line.ProductId, Quantity = line.Quantity })
                .ToList()
        };

        db.VanLoads.Add(load);
        await db.SaveChangesAsync(ct);

        // Loading takes stock off the warehouse and puts it on the van; a return does the reverse.
        var (source, destination) = request.Direction == VanLoadDirection.Loading
            ? (warehouse.Id, van.Id)
            : (van.Id, warehouse.Id);

        foreach (var line in load.Lines)
        {
            db.StockMovements.AddRange(
                NewMovement(line.ProductId, source, -line.Quantity, occurredAt, load.Id),
                NewMovement(line.ProductId, destination, line.Quantity, occurredAt, load.Id));
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var warnings = await WarningsForAsync(load.Lines.Select(l => l.ProductId), source, products, ct);

        return new CreateVanLoadResponse(await GetAsync(load.Id, ct), warnings);
    }

    public async Task<VanLoadDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.VanLoads.Where(v => v.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Van load");

    public async Task<IReadOnlyList<VanLoadDto>> GetAllAsync(
        Guid? vanLocationId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct) =>
        await Project(db.VanLoads
                .Where(v => vanLocationId == null || v.VanLocationId == vanLocationId)
                .Where(v => from == null || v.BusinessDate >= from)
                .Where(v => to == null || v.BusinessDate <= to)
                .OrderByDescending(v => v.BusinessDate)
                .ThenByDescending(v => v.OccurredAt))
            .ToListAsync(ct);

    /// <summary>
    /// What the van did on one business day, product by product. The arithmetic is deliberately the
    /// ledger's own: opening + loaded − sold − returned + corrections is simply the van's closing
    /// balance, so a discrepancy is not a separate calculation that could disagree with stock -
    /// it <em>is</em> the stock still sitting on a van that should be empty.
    /// </summary>
    public async Task<VanReconciliationDto> GetReconciliationAsync(
        Guid vanLocationId,
        DateOnly businessDate,
        CancellationToken ct)
    {
        var van = await FindLocationAsync(vanLocationId, ct);
        var (dayStart, dayEnd) = IndiaTime.DayRangeUtc(businessDate);

        var movements = await db.StockMovements
            .Where(m => m.LocationId == vanLocationId && m.OccurredAt < dayEnd)
            .Select(m => new
            {
                m.ProductId,
                ProductName = m.Product!.Name,
                UnitCode = m.Product!.UnitOfMeasure!.Code,
                m.MovementType,
                m.Quantity,
                m.OccurredAt
            })
            .ToListAsync(ct);

        var lines = movements
            .GroupBy(m => new { m.ProductId, m.ProductName, m.UnitCode })
            .Select(group =>
            {
                var before = group.Where(m => m.OccurredAt < dayStart).Sum(m => m.Quantity);
                var today = group.Where(m => m.OccurredAt >= dayStart).ToList();

                var loaded = today.Where(m => m.MovementType == StockMovementType.Transfer && m.Quantity > 0)
                    .Sum(m => m.Quantity);
                var returned = -today.Where(m => m.MovementType == StockMovementType.Transfer && m.Quantity < 0)
                    .Sum(m => m.Quantity);
                var sold = -today.Where(m => m.MovementType is StockMovementType.Sale or StockMovementType.SaleReversal)
                    .Sum(m => m.Quantity);
                // Fresh packets handed to a shop in place of expired ones: gone like a sale, but free.
                var replaced = -today.Where(m => m.MovementType == StockMovementType.Replacement)
                    .Sum(m => m.Quantity);
                var other = today.Where(m => m.MovementType is not (StockMovementType.Transfer
                                or StockMovementType.Sale or StockMovementType.SaleReversal
                                or StockMovementType.Replacement))
                    .Sum(m => m.Quantity);

                return new VanReconciliationLineDto(
                    group.Key.ProductId,
                    group.Key.ProductName,
                    group.Key.UnitCode,
                    before,
                    loaded,
                    sold,
                    returned,
                    other,
                    before + loaded - sold - returned - replaced + other,
                    replaced);
            })
            .Where(line => line.Loaded != 0m || line.Sold != 0m || line.Returned != 0m ||
                           line.Opening != 0m || line.Other != 0m || line.Replaced != 0m)
            .OrderBy(line => line.ProductName)
            .ToList();

        return new VanReconciliationDto(
            van.Id,
            van.Code,
            businessDate,
            lines.All(line => line.Unaccounted == 0m),
            lines);
    }

    /// <summary>
    /// What the van is holding right now, which is what the evening return should hand back.
    /// The screen uses it to fill the return in for the office rather than making them type it.
    /// </summary>
    public async Task<IReadOnlyList<VanLoadLineDto>> GetOnVanAsync(Guid vanLocationId, CancellationToken ct)
    {
        await FindLocationAsync(vanLocationId, ct);

        var rows = await db.Products
            .Select(p => new
            {
                p.Id,
                p.Name,
                UnitCode = p.UnitOfMeasure!.Code,
                Quantity = db.StockMovements
                    .Where(m => m.ProductId == p.Id && m.LocationId == vanLocationId)
                    .Sum(m => (decimal?)m.Quantity) ?? 0m
            })
            .Where(p => p.Quantity != 0m)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return rows.Select(r => new VanLoadLineDto(r.Id, r.Name, r.UnitCode, r.Quantity)).ToList();
    }

    private static StockMovement NewMovement(
        Guid productId,
        Guid locationId,
        decimal quantity,
        DateTime occurredAt,
        Guid vanLoadId) =>
        new()
        {
            ProductId = productId,
            LocationId = locationId,
            MovementType = StockMovementType.Transfer,
            Quantity = quantity,
            OccurredAt = occurredAt,
            ReferenceType = StockReferenceType.VanLoad,
            ReferenceId = vanLoadId
        };

    private async Task<Dictionary<Guid, string>> ValidateLinesAsync(
        IReadOnlyList<VanLoadLineRequest> lines,
        CancellationToken ct)
    {
        if (lines.Select(l => l.ProductId).Distinct().Count() != lines.Count)
        {
            throw new DomainException("The same product is listed twice. Combine the quantities into one line.");
        }

        var ids = lines.Select(l => l.ProductId).ToList();
        var products = await db.Products
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.IsActive, p.Kind })
            .ToListAsync(ct);

        foreach (var line in lines)
        {
            var product = products.FirstOrDefault(p => p.Id == line.ProductId)
                          ?? throw new NotFoundException("Product");

            if (!product.IsActive)
            {
                throw new DomainException($"Product '{product.Name}' is not active.");
            }

            if (product.Kind == ProductKind.Pieces)
            {
                throw ShopRates.OnlyAtTheShop(product.Name);
            }

            if (line.Quantity <= 0m)
            {
                throw new DomainException($"The quantity for '{product.Name}' must be greater than zero.");
            }
        }

        return products.ToDictionary(p => p.Id, p => p.Name);
    }

    private async Task<StockLocation> FindLocationAsync(Guid id, CancellationToken ct)
    {
        var location = await db.StockLocations.FirstOrDefaultAsync(l => l.Id == id, ct)
                       ?? throw new NotFoundException("Stock location");

        if (!location.IsActive)
        {
            throw new DomainException($"Location '{location.Name}' is not active.");
        }

        return location;
    }

    /// <summary>Short stock warns and never blocks: the packets physically moved either way.</summary>
    private async Task<IReadOnlyList<string>> WarningsForAsync(
        IEnumerable<Guid> productIds,
        Guid sourceLocationId,
        IReadOnlyDictionary<Guid, string> names,
        CancellationToken ct)
    {
        var warnings = new List<string>();

        foreach (var productId in productIds.Distinct())
        {
            var onHand = await stock.GetQuantityOnHandAsync(productId, sourceLocationId, ct);

            if (onHand < 0m)
            {
                warnings.Add($"{names[productId]}: stock where it came from is now {onHand:0.###}.");
            }
        }

        return warnings;
    }

    private static IQueryable<VanLoadDto> Project(IQueryable<VanLoad> loads) =>
        loads.Select(v => new VanLoadDto(
            v.Id,
            v.VanLocationId,
            v.VanLocation!.Code,
            v.Direction,
            v.Device!.Name,
            v.OccurredAt,
            v.BusinessDate,
            v.Notes,
            v.Lines
                .Select(l => new VanLoadLineDto(
                    l.ProductId, l.Product!.Name, l.Product!.UnitOfMeasure!.Code, l.Quantity))
                .ToList()));
}
