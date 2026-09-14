using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.Payments;

public record PaymentAllocationRequest(
    [Required] Guid InvoiceId,
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal Amount);

/// <summary>
/// Leave Allocations empty and the money is applied to the oldest unpaid bills first, which is
/// how bill-to-bill settlement works on the route. Send them to say which bill was paid.
/// </summary>
public record CreatePaymentRequest(
    [Required] Guid CustomerId,
    DateOnly? PaymentDate,
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal Amount,
    [Required] PaymentMethod Method,
    [MaxLength(100)] string? Reference,
    [MaxLength(300)] string? Notes,
    List<PaymentAllocationRequest>? Allocations);

public record PaymentAllocationDto(Guid InvoiceId, string InvoiceNumber, decimal Amount);

public record PaymentDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    DateOnly PaymentDate,
    decimal Amount,
    PaymentMethod Method,
    string? Reference,
    string? Notes,
    decimal AllocatedAmount,
    decimal UnallocatedAmount,
    IReadOnlyList<PaymentAllocationDto> Allocations);

/// <summary>CustomerBalance is what the shop still owes after this payment.</summary>
public record CreatePaymentResponse(PaymentDto Payment, decimal CustomerBalance);
