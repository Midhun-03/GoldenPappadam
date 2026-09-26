using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Inventory.Repacking;

/// <summary>Open FromQuantity packets of FromProduct and pack them as ToProduct. The server works out the rest.</summary>
public record RepackRequest(
    [Required] Guid FromProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal FromQuantity,
    [Required] Guid ToProductId,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes);

/// <summary>
/// What a repack comes to: packets made, and any pieces (or grams) that do not fill one more packet
/// going back to the loose stock. Warning is set when the warehouse holds fewer packets than are
/// being opened - it warns, it does not block, like every other stock shortfall.
/// </summary>
public record RepackPlanDto(
    Guid FromProductId,
    string FromProductName,
    decimal FromQuantity,
    string FromUnitCode,
    Guid ToProductId,
    string ToProductName,
    decimal ToQuantity,
    string ToUnitCode,
    Guid? LeftoverProductId,
    string? LeftoverProductName,
    decimal LeftoverQuantity,
    string LooseUnitCode,
    decimal FromOnHand,
    string? Warning);

public record RepackResponse(Guid RepackEntryId, RepackPlanDto Result);

public record RepackEntryDto(
    Guid Id,
    DateTime OccurredAt,
    string FromProductName,
    decimal FromQuantity,
    string ToProductName,
    decimal ToQuantity,
    string? LeftoverProductName,
    decimal LeftoverQuantity,
    string? Notes);
