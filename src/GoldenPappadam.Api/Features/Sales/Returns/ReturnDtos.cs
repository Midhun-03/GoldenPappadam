using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.Returns;

/// <summary>UnitRate is optional: leave it out to value the packets at the shop's agreed rate.</summary>
public record ReturnLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity,
    ReturnReason Reason,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? UnitRate);

/// <summary>
/// Settlement Pending records the return and leaves the decision for later. CreditAmount is only for
/// a credit, and defaults to the value of the returned packets. ReplacementLocationId is where the
/// fresh packets came from, the warehouse when left out.
/// </summary>
public record CreateReturnRequest(
    [Required] Guid CustomerId,
    Guid? BranchId,
    DateOnly? ReturnDate,
    [Required, MinLength(1)] List<ReturnLineRequest> Lines,
    ReturnSettlement Settlement,
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal? CreditAmount,
    Guid? ReplacementLocationId,
    [MaxLength(300)] string? Notes);

public record SettleReturnRequest(
    ReturnSettlement Settlement,
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal? CreditAmount,
    Guid? ReplacementLocationId);

public record CancelReturnRequest([Required, MaxLength(300)] string Reason);

public record ReturnLineDto(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    string Description,
    string UnitCode,
    decimal Quantity,
    ReturnReason Reason,
    decimal UnitRate,
    decimal Value);

public record ReturnListItemDto(
    Guid Id,
    string ReturnNumber,
    DateOnly ReturnDate,
    Guid CustomerId,
    string CustomerName,
    string? BranchName,
    ReturnStatus Status,
    ReturnSettlement Settlement,
    decimal Value,
    decimal CreditAmount);

/// <summary>ReplacementFrom names the place the fresh packets left, for a replacement.</summary>
public record ReturnDetailDto(
    Guid Id,
    string ReturnNumber,
    DateOnly ReturnDate,
    Guid CustomerId,
    string CustomerName,
    Guid? BranchId,
    string? BranchName,
    ReturnStatus Status,
    ReturnSettlement Settlement,
    decimal Value,
    decimal CreditAmount,
    string? ReplacementFrom,
    string? Notes,
    DateTime RecordedAt,
    string? RecordedByName,
    DateTime? SettledAt,
    DateTime? CancelledAt,
    string? CancelledByName,
    string? CancellationReason,
    IReadOnlyList<ReturnLineDto> Lines);

/// <summary>Warnings list products a replacement took below zero. The return is saved either way.</summary>
public record ReturnResponse(ReturnDetailDto Return, IReadOnlyList<string> Warnings);
