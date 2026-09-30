using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.RateRequests;

/// <summary>
/// A request as the office sees it. CurrentPrice is the customer's agreed rate now (null = the standard
/// price StandardPrice), which may differ from PriceWhenRequested if the rate moved since.
/// </summary>
public record RateRequestDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid ProductId,
    string ProductName,
    string UnitCode,
    decimal? PriceWhenRequested,
    decimal? CurrentPrice,
    decimal? StandardPrice,
    decimal RequestedPrice,
    string? Reason,
    DateTime RequestedAt,
    string? RequestedBy,
    RateRequestStatus Status,
    DateTime? DecidedAt,
    string? DecidedBy,
    string? DecisionNote);

public record DecideRateRequest([MaxLength(300)] string? Note);
