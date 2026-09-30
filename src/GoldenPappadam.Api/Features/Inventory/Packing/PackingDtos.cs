using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Inventory.Packing;

/// <summary>
/// What the source loses is not sent: the server works it out from the product (CLAUDE.md §4 "Packing
/// conversion"). ClientRequestId is made by the screen when it opens, so a second Save packs once.
/// </summary>
public record CreatePackingRequest(
    [Required] Guid PackedProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal PacksProduced,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes,
    Guid? ClientRequestId = null);

public record PackingPreviewRequest(
    [Required] Guid PackedProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal PacksProduced);

/// <summary>
/// A packing worked out: what one pack holds, what the source loses, and the warehouse before and after.
/// PiecesPerPack, PiecesPerKg and PiecesUsed are set for a count-based packet only. Shortfall is the
/// reason it cannot be packed, or null when it can.
/// </summary>
public record PackingPlanDto(
    Guid PackedProductId,
    string PackedProductName,
    decimal PacksProduced,
    string PackedUnitCode,
    Guid SourceProductId,
    string SourceProductName,
    string SourceUnitCode,
    int? PiecesPerPack,
    decimal? PiecesPerKg,
    decimal SourcePerPack,
    decimal SourceUsed,
    decimal? PiecesUsed,
    decimal SourceOnHandBefore,
    decimal SourceOnHandAfter,
    string? Shortfall);

/// <summary>Warning is kept for older screens and is always null now: packing never goes below zero.</summary>
public record PackingResponse(
    Guid PackingEntryId,
    Guid PackedProductId,
    decimal PacksProduced,
    decimal PackedQuantityOnHand,
    Guid SourceProductId,
    decimal SourceQuantityUsed,
    decimal SourceQuantityOnHand,
    string? Warning,
    PackingPlanDto Plan);

/// <summary>
/// The conversion fields and SourceOnHandBefore/After are null on entries made before 2026-09-30, which
/// recorded only what was typed.
/// </summary>
public record PackingEntryDto(
    Guid Id,
    DateTime OccurredAt,
    Guid PackedProductId,
    string PackedProductName,
    decimal PacksProduced,
    Guid SourceProductId,
    string SourceProductName,
    decimal SourceQuantityUsed,
    string? Notes,
    string SourceUnitCode,
    int? PiecesPerPack,
    decimal? PiecesPerKg,
    decimal? SourcePerPack,
    decimal? SourceOnHandBefore,
    decimal? SourceOnHandAfter);
