using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.FieldSales;

namespace GoldenPappadam.Api.Features.FieldSales.VanLoads;

public record VanLoadLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity);

/// <summary>
/// Loading the van, or taking back what did not sell. WarehouseLocationId is optional and means
/// the main warehouse.
/// </summary>
public record CreateVanLoadRequest(
    [Required] Guid VanLocationId,
    [Required] VanLoadDirection Direction,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes,
    [Required, MinLength(1)] List<VanLoadLineRequest> Lines,
    Guid? WarehouseLocationId = null);

public record VanLoadLineDto(Guid ProductId, string ProductName, string UnitCode, decimal Quantity);

public record VanLoadDto(
    Guid Id,
    Guid VanLocationId,
    string VanCode,
    VanLoadDirection Direction,
    DateTime OccurredAt,
    DateOnly BusinessDate,
    string? Notes,
    IReadOnlyList<VanLoadLineDto> Lines);

/// <summary>Warnings name anything the move pushed below zero. The move is recorded either way.</summary>
public record CreateVanLoadResponse(VanLoadDto VanLoad, IReadOnlyList<string> Warnings);

/// <summary>
/// One product's day on the van. <paramref name="Unaccounted"/> is what the van is still holding
/// after the evening return: it should be zero, and when it is not, somebody needs to say why.
/// It is deliberately not corrected automatically - a sale nobody recorded and a miscount look
/// identical to the arithmetic, and only a person can tell them apart.
/// </summary>
public record VanReconciliationLineDto(
    Guid ProductId,
    string ProductName,
    string UnitCode,
    decimal Opening,
    decimal Loaded,
    decimal Sold,
    decimal Returned,
    decimal Other,
    decimal Unaccounted);

public record VanReconciliationDto(
    Guid VanLocationId,
    string VanCode,
    DateOnly BusinessDate,
    bool IsSettled,
    IReadOnlyList<VanReconciliationLineDto> Lines);
