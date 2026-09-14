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
    StockReferenceType? ReferenceType,
    Guid? ReferenceId,
    string? Notes);

/// <summary>Opening stock, production output, or damage. Quantity is always positive; damage is stored as negative.</summary>
public record CreateStockEntryRequest(
    [Required] Guid ProductId,
    [Required] StockMovementType MovementType,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes);

/// <summary>Correction after a physical count: the app works out the difference itself.</summary>
public record AdjustStockRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal CountedQuantity,
    DateTime? OccurredAt,
    [Required, MaxLength(300)] string Notes);

/// <summary>Warning is filled when stock went negative; the entry is still saved.</summary>
public record StockEntryResponse(Guid MovementId, Guid ProductId, decimal QuantityOnHand, string? Warning);
