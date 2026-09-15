using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>UnitPrice is optional: leave it out to charge the product's current selling price.</summary>
public record InvoiceLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity,
    decimal? UnitPrice);

/// <summary>
/// LocationId is where the goods came from, and is optional: leaving it out means the main
/// warehouse, which is what every bill meant before the van carried its own stock.
/// </summary>
public record CreateInvoiceRequest(
    [Required] Guid CustomerId,
    DateOnly? InvoiceDate,
    decimal DiscountAmount,
    [MaxLength(300)] string? Notes,
    [Required, MinLength(1)] List<InvoiceLineRequest> Lines,
    Guid? LocationId = null);

public record CancelInvoiceRequest([Required, MaxLength(300)] string Reason);

public record InvoiceLineDto(
    Guid Id,
    Guid ProductId,
    string Description,
    string UnitCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record InvoiceListItemDto(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    DateOnly InvoiceDate,
    InvoiceStatus Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding);

public record InvoiceDetailDto(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    DateOnly InvoiceDate,
    InvoiceStatus Status,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding,
    string? Notes,
    DateTime? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<InvoiceLineDto> Lines);

/// <summary>Warnings list products the bill pushed below zero. The bill is saved either way.</summary>
public record CreateInvoiceResponse(InvoiceDetailDto Invoice, IReadOnlyList<string> Warnings);
