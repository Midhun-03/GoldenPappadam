using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.OwnShop;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.OwnShop.Sales;

/// <summary>
/// UnitPrice is the rate per piece the office typed; left out, the sale charges the customer's agreed
/// rate, else the product's standard rate. It must lie between the product's minimum and its standard
/// rate.
/// </summary>
public record ShopSaleLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "1", "79228162514264337593543950335")] decimal Quantity,
    decimal? UnitPrice = null);

/// <summary>
/// No CustomerId is a walk-in customer. Paid in full at the counter by PaymentMethod. ClientRequestId is
/// made by the screen when it opens, so a second Save sells once.
/// </summary>
public record CreateShopSaleRequest(
    Guid? CustomerId,
    PaymentMethod PaymentMethod,
    [Required] IReadOnlyList<ShopSaleLineRequest> Lines,
    [MaxLength(300)] string? Notes = null,
    Guid? ClientRequestId = null);

public record CancelShopSaleRequest([Required, MaxLength(300)] string Reason);

public record ShopSaleLineDto(
    int LineNumber,
    Guid ProductId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DefaultPrice,
    decimal MinimumPrice,
    decimal LineTotal);

/// <summary>BelowStandardRate: some line was sold under the product's rate, a wholesale or catering price.</summary>
public record ShopSaleListItemDto(
    Guid Id,
    string SaleNumber,
    DateOnly SaleDate,
    DateTime CreatedAt,
    Guid? CustomerId,
    string? CustomerName,
    PaymentMethod PaymentMethod,
    decimal Pieces,
    decimal TotalAmount,
    ShopSaleStatus Status,
    bool BelowStandardRate);

public record ShopSaleDetailDto(
    Guid Id,
    string SaleNumber,
    DateOnly SaleDate,
    DateTime CreatedAt,
    string LocationName,
    Guid? CustomerId,
    string? CustomerName,
    PaymentMethod PaymentMethod,
    decimal TotalAmount,
    ShopSaleStatus Status,
    string? Notes,
    string? CreatedByName,
    DateTime? CancelledAt,
    string? CancellationReason,
    string? CancelledByName,
    IReadOnlyList<ShopSaleLineDto> Lines);
