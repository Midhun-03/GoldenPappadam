using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.OwnShop.Transfers;

/// <summary>
/// Loose pappadam sent to the own shop, as the factory records it: which variety, and how many kg.
/// The pieces it becomes are worked out by the server from the variety's pieces per kg, never sent.
/// ClientRequestId is made by the screen when it opens, so a second Save records it once.
/// </summary>
public record CreateShopTransferRequest(
    [Required] Guid SourceProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal QuantityKg,
    DateTime? OccurredAt,
    [MaxLength(300)] string? Notes,
    Guid? ClientRequestId = null);

public record ShopTransferPreviewRequest(
    [Required] Guid SourceProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal QuantityKg);

/// <summary>
/// A transfer worked out: kg × pieces per kg, rounded to the nearest whole piece, and both ends before
/// and after. Shortfall is why it cannot be sent, or null when it can.
/// </summary>
public record ShopTransferPlanDto(
    Guid SourceProductId,
    string SourceProductName,
    Guid PiecesProductId,
    string PiecesProductName,
    decimal QuantityKg,
    decimal PiecesPerKg,
    decimal Pieces,
    decimal FactoryOnHandBefore,
    decimal FactoryOnHandAfter,
    decimal ShopOnHandBefore,
    decimal ShopOnHandAfter,
    string? Shortfall);

public record ShopTransferDto(
    Guid Id,
    DateTime OccurredAt,
    Guid SourceProductId,
    string SourceProductName,
    Guid PiecesProductId,
    string PiecesProductName,
    string FromLocationName,
    string ToLocationName,
    decimal QuantityKg,
    decimal PiecesPerKg,
    decimal PiecesReceived,
    decimal SourceOnHandBefore,
    decimal SourceOnHandAfter,
    decimal ShopOnHandBefore,
    decimal ShopOnHandAfter,
    string? CreatedByName,
    string? Notes);
