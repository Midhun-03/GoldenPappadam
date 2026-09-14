using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Inventory.Packing;

/// <summary>
/// SourceQuantityUsed is what was actually consumed. Leave it out and the app uses
/// PacksProduced * SourceQuantityPerPack; send it to record packing loss.
/// </summary>
public record CreatePackingRequest(
    [Required] Guid PackedProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal PacksProduced,
    decimal? SourceQuantityUsed,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes);

public record PackingResponse(
    Guid PackingEntryId,
    Guid PackedProductId,
    decimal PacksProduced,
    decimal PackedQuantityOnHand,
    Guid SourceProductId,
    decimal SourceQuantityUsed,
    decimal SourceQuantityOnHand,
    string? Warning);

public record PackingEntryDto(
    Guid Id,
    DateTime OccurredAt,
    Guid PackedProductId,
    string PackedProductName,
    decimal PacksProduced,
    Guid SourceProductId,
    string SourceProductName,
    decimal SourceQuantityUsed,
    string? Notes);
