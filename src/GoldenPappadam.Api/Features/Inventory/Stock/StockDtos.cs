using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

public record StockOnHandDto(
    Guid ProductId,
    string ProductCode,
    string Name,
    ProductKind Kind,
    string UnitCode,
    decimal QuantityOnHand,
    decimal? LowStockThreshold,
    bool IsLowStock,
    bool IsActive);

public record StockMovementDto(
    Guid Id,
    DateTime OccurredAt,
    StockMovementType MovementType,
    decimal Quantity,
    decimal RunningBalance,
    Guid LocationId,
    string LocationCode,
    StockReferenceType? ReferenceType,
    Guid? ReferenceId,
    string? Notes);

/// <summary>One product's stock in one place. The van's own count, for instance.</summary>
public record LocationStockDto(
    Guid LocationId,
    string Code,
    string Name,
    StockLocationKind Kind,
    decimal QuantityOnHand);

public record StockLocationDto(Guid Id, string Code, string Name, StockLocationKind Kind, bool IsActive);

/// <summary>
/// Opening stock, production output, or damage. Quantity is always positive; damage is stored as
/// negative. LocationId is optional and means the main warehouse, which is where all of this
/// happened before the van existed.
/// </summary>
public record CreateStockEntryRequest(
    [Required] Guid ProductId,
    [Required] StockMovementType MovementType,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes,
    Guid? LocationId = null);

/// <summary>
/// Correction after a physical count: the app works out the difference itself. Counting the van at
/// the end of the day is the same operation as counting a warehouse shelf.
/// </summary>
public record AdjustStockRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal CountedQuantity,
    DateTime? OccurredAt,
    [Required, MaxLength(300)] string Notes,
    Guid? LocationId = null);

/// <summary>Warning is filled when stock went negative; the entry is still saved.</summary>
public record StockEntryResponse(
    Guid MovementId,
    Guid ProductId,
    Guid LocationId,
    decimal QuantityOnHand,
    string? Warning);

public record WriteOffExpiredRequest([Required] Guid ProductId, [Required] Guid LocationId);
