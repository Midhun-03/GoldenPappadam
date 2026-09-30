using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Sales.CustomerPrices;

/// <summary>
/// One product as this shop sees it. <paramref name="EffectivePrice"/> is what a bill would
/// actually charge - the agreed price if there is one, the product's own price otherwise - so the
/// screen and the phone never have to work the rule out for themselves.
/// </summary>
public record CustomerPriceDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitCode,
    decimal? DefaultPrice,
    decimal? AgreedPrice,
    decimal? EffectivePrice);

public record SetCustomerPriceRequest(
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal UnitPrice);

/// <summary>
/// One rate change. A null <paramref name="PreviousPrice"/> means the customer was on the standard
/// price before; a null <paramref name="NewPrice"/> means the agreed rate was removed.
/// </summary>
public record CustomerPriceChangeDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid ProductId,
    string ProductName,
    string UnitCode,
    decimal? PreviousPrice,
    decimal? NewPrice,
    DateTime ChangedAt,
    string? ChangedBy,
    bool ChangedBySalesperson,
    string? RequestedBy = null);
