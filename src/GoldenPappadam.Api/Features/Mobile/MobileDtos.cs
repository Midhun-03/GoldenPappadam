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
    decimal Balance,
    bool HasMultipleBranches);

/// <summary>One physical shop under a multi-branch customer, e.g. Kundara under Danya Supermarket.</summary>
public record SnapshotBranchDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    string? Location,
    string? Address,
    string? Phone,
    string? ContactPerson);

public record SnapshotProductDto(
    Guid Id,
    string ProductCode,
    string Name,
    string UnitCode,
    decimal? DefaultPrice);

public record SnapshotPriceDto(Guid CustomerId, Guid ProductId, decimal UnitPrice);

/// <summary>
/// Money already received from a shop, so the salesperson can answer "when did I last collect from
/// you?" standing in the doorway with no signal. Recent only: the phone is not an archive.
/// </summary>
public record SnapshotPaymentDto(
    Guid Id,
    Guid CustomerId,
    DateOnly PaymentDate,
    DateTime RecordedAt,
    decimal Amount,
    string Method,
    string? Reference,
    string? Notes);

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
    IReadOnlyList<SnapshotPaymentDto> Payments,
    IReadOnlyList<string> PaymentMethods,
    IReadOnlyList<SnapshotBranchDto> Branches);

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

/// <summary>
/// BranchId names which shop of a multi-branch customer this sale is for. The snapshot tells the
/// phone which customers need one; a plain customer never carries one, same rule as the admin panel.
/// </summary>
public record MobileSaleRequest(
    [Required] Guid CustomerId,
    [Required, MinLength(1)] List<MobileSaleLineRequest> Lines,
    DateTime PricesAsOf,
    [MaxLength(300)] string? Notes,
    Guid? BranchId = null);

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

public record MobileVanLoadLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity);

/// <summary>
/// What the salesperson actually took from the warehouse this morning.
///
/// Deliberately carries no location and no direction: the server puts it on the van this phone
/// belongs to, moving from the main warehouse, and nothing else. That is the whole of the
/// salesperson's power over stock - they cannot adjust it, write it off, or move it anywhere else.
/// </summary>
public record MobileVanLoadRequest(
    [Required, MinLength(1)] List<MobileVanLoadLineRequest> Lines,
    [MaxLength(300)] string? Notes);

public record MobileStockRequestLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity);

/// <summary>
/// What the salesperson needs the packing unit to pack, and when. Not a customer order: nothing is
/// billed and no stock moves.
/// </summary>
public record MobileStockRequestRequest(
    DateOnly RequiredDate,
    [Required, MinLength(1)] List<MobileStockRequestLineRequest> Lines,
    [MaxLength(300)] string? Notes);

/// <summary>
/// A shop the salesperson found, or new details for one they already have. Id is generated on the
/// phone and is the customer's id for good, so a sale in the same batch can name the shop before the
/// server has seen it. Create-or-update by that id.
///
/// Deliberately absent: an opening balance (a shop the salesperson found owes nothing yet), an
/// active flag (only the office deactivates a customer), and notes - the office's own remarks, which
/// the phone never sees, so an edit from the phone must not be able to overwrite them.
/// </summary>
public record MobileCustomerRequest(
    [Required] Guid Id,
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? ContactPerson,
    [MaxLength(20)] string? Phone,
    [MaxLength(300)] string? Address,
    bool HasMultipleBranches);

/// <summary>
/// A branch under an existing customer, new or edited. Id is the phone's, create-or-update like the
/// customer. There is no active flag: closing a branch is the office's decision.
/// </summary>
public record MobileBranchRequest(
    [Required] Guid Id,
    [Required] Guid CustomerId,
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? Location,
    [MaxLength(300)] string? Address,
    [MaxLength(20)] string? Phone,
    [MaxLength(100)] string? ContactPerson);

/// <summary>
/// What a customer pays for a product from now on - at every branch. Recorded in the price history
/// with the salesperson's name. Removing a rate is not offered: that is the office's call.
/// </summary>
public record MobileCustomerPriceRequest(
    [Required] Guid CustomerId,
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal UnitPrice);

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
    MobileVisitRequest? Visit,
    MobileVanLoadRequest? VanLoad = null,
    MobileStockRequestRequest? StockRequest = null,
    MobileCustomerRequest? Customer = null,
    MobileBranchRequest? Branch = null,
    MobileCustomerPriceRequest? CustomerPrice = null);

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
    /// <summary>Delivered today and not settled on the spot.</summary>
    decimal CreditSales,
    /// <summary>Stops that bought nothing. A number the business has never been able to see.</summary>
    int NoSaleVisits,
    /// <summary>
    /// What every shop owes, as at the last sync. It ignores anything still sitting in the outbox,
    /// because until the office has it the official answer has not changed.
    /// </summary>
    decimal TotalOutstanding,
    IReadOnlyList<MobileDaySaleDto> Sales);
