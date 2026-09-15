using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Mobile;

// ---------- registration ----------

public record RegisterDeviceRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(30)] string Platform);

public record DeviceDto(Guid Id, string Name, string Platform, Guid? LocationId, string? LocationCode);

// ---------- the snapshot the phone works from ----------

public record SnapshotCustomerDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Address,
    decimal Balance);

public record SnapshotProductDto(
    Guid Id,
    string ProductCode,
    string Name,
    string UnitCode,
    decimal? DefaultPrice);

public record SnapshotPriceDto(Guid CustomerId, Guid ProductId, decimal UnitPrice);

/// <summary>
/// Everything the phone needs to work with no signal at all. A full snapshot rather than a delta:
/// there are tens of shops and tens of products, and a delta protocol would be more moving parts
/// than the payload is worth.
///
/// <paramref name="PricesAsOf"/> is the stamp the phone sends back with a sale, so the server can
/// tell whether a price changed while the phone was away.
/// </summary>
public record SnapshotDto(
    DateTime ServerTime,
    DateTime PricesAsOf,
    Guid? VanLocationId,
    IReadOnlyList<SnapshotCustomerDto> Customers,
    IReadOnlyList<SnapshotProductDto> Products,
    IReadOnlyList<SnapshotPriceDto> Prices,
    IReadOnlyList<string> PaymentMethods);

// ---------- what the phone sends back ----------

/// <summary>
/// UnitPrice is what the salesperson's app actually charged, not a price they chose: the app shows
/// it read-only and has no way to edit it. It is sent because the sale already happened at that
/// price, and rewriting history would make the bill disagree with what the shop was told.
/// </summary>
public record MobileSaleLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal UnitPrice);

public record MobileSaleRequest(
    [Required] Guid CustomerId,
    [Required, MinLength(1)] List<MobileSaleLineRequest> Lines,
    DateTime PricesAsOf,
    [MaxLength(300)] string? Notes);

public record MobilePaymentRequest(
    [Required] Guid CustomerId,
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")] decimal Amount,
    [Required] PaymentMethod Method,
    [MaxLength(100)] string? Reference,
    [MaxLength(300)] string? Notes);

/// <summary>
/// The stop itself. The sale and the payment are named by their own client ids, so the phone can
/// send all three in one batch without knowing any server id yet.
/// </summary>
public record MobileVisitRequest(
    [Required] Guid CustomerId,
    [Required] VisitOutcome Outcome,
    Guid? SaleClientRequestId,
    Guid? PaymentClientRequestId,
    [MaxLength(300)] string? Notes);

/// <summary>
/// One thing the salesperson did. ClientRequestId is generated on the device when they save and is
/// never regenerated, which is what makes a retry safe.
/// </summary>
public record SubmissionItemRequest(
    [Required] Guid ClientRequestId,
    [Required] SubmissionType Type,
    DateTime RecordedAt,
    MobileSaleRequest? Sale,
    MobilePaymentRequest? Payment,
    MobileVisitRequest? Visit);

public record SubmissionBatchRequest(
    [Required] Guid DeviceId,
    [Required, MinLength(1)] List<SubmissionItemRequest> Items);

public enum SubmissionOutcome
{
    /// <summary>Created. The phone can mark it synced.</summary>
    Accepted,

    /// <summary>Already here from an earlier attempt. Also synced - not an error.</summary>
    AlreadyAccepted,

    /// <summary>A business rule refused it. The phone keeps it and shows the salesperson.</summary>
    Rejected
}

public record SubmissionResultDto(
    Guid ClientRequestId,
    SubmissionOutcome Outcome,
    Guid? RecordId,
    string? Error,
    bool PriceMismatch,
    IReadOnlyList<string> Warnings);

public record SubmissionBatchResponse(DateTime ServerTime, IReadOnlyList<SubmissionResultDto> Results);

// ---------- the salesperson's own day ----------

public record MobileDaySaleDto(
    Guid InvoiceId,
    string InvoiceNumber,
    string CustomerName,
    DateTime RecordedAt,
    decimal TotalAmount);

public record MobileDayDto(
    DateOnly BusinessDate,
    decimal TotalSales,
    int SaleCount,
    int ShopsVisited,
    decimal CashCollected,
    IReadOnlyList<MobileDaySaleDto> Sales);
