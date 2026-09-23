using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>UnitPrice is optional: leave it out to charge the shop's agreed rate, else the product's price.</summary>
public record InvoiceLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity,
    decimal? UnitPrice);

/// <summary>
/// LocationId is where the goods came from, and is optional: leaving it out means the main
/// warehouse, which is what every bill meant before the van carried its own stock.
/// BranchId names which shop of a multi-branch customer this bill is for: required when that
/// customer has branches, and rejected otherwise so a bill can never point at the wrong shop.
/// No totals are accepted: the server works every amount out itself.
/// </summary>
public record CreateInvoiceRequest(
    [Required] Guid CustomerId,
    DateOnly? InvoiceDate,
    decimal DiscountAmount,
    [MaxLength(300)] string? Notes,
    [Required, MinLength(1)] List<InvoiceLineRequest> Lines,
    Guid? LocationId = null,
    Guid? BranchId = null);

public record CancelInvoiceRequest([Required, MaxLength(300)] string Reason);

/// <summary>Recipient is optional: leave it out to use the customer's email address.</summary>
public record EmailInvoiceRequest([MaxLength(256), EmailAddress] string? Recipient);

public record InvoiceLineDto(
    Guid Id,
    Guid ProductId,
    string Description,
    string UnitCode,
    string? HsnCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal DiscountAmount,
    TaxTreatment? TaxTreatment,
    decimal GstRate,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount,
    decimal Amount);

public record InvoiceListItemDto(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    Guid? BranchId,
    string? BranchName,
    DateOnly InvoiceDate,
    InvoiceStatus Status,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding,
    InvoiceDocumentType DocumentType,
    bool HasPdf,
    InvoiceEmailStatus? LastEmailStatus);

/// <summary>A supplier, customer or branch exactly as printed on the invoice.</summary>
public record InvoicePartyDto(
    string Name,
    string? Address,
    string? Phone,
    string? Gstin,
    string? StateCode);

public record InvoiceDocumentInfoDto(string FileName, long SizeBytes, string Sha256, DateTime GeneratedAt);

public record InvoiceEmailLogDto(
    Guid Id,
    DateTime AttemptedAt,
    string Recipient,
    string Subject,
    InvoiceEmailStatus Status,
    string? ErrorMessage,
    int AttemptNumber,
    string? SentByName);

/// <summary>
/// FinalizedAt/By are the invoice's creation; CancelledAt/By its only permitted update.
/// RecordedOnDevice names the phone for a sale that came in through the sales app.
/// </summary>
public record InvoiceDetailDto(
    Guid Id,
    string InvoiceNumber,
    string SeriesCode,
    string FinancialYear,
    InvoiceDocumentType DocumentType,
    Guid CustomerId,
    string CustomerName,
    Guid? BranchId,
    string? BranchName,
    DateOnly InvoiceDate,
    InvoiceStatus Status,
    InvoicePartyDto Supplier,
    InvoicePartyDto Customer,
    InvoicePartyDto? Branch,
    string? PlaceOfSupplyStateCode,
    bool IsInterState,
    bool ReverseCharge,
    bool PricesIncludeTax,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount,
    decimal RoundOff,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding,
    string? Notes,
    DateTime FinalizedAt,
    string? FinalizedByName,
    string? RecordedOnDevice,
    DateTime? CancelledAt,
    string? CancelledByName,
    string? CancellationReason,
    InvoiceDocumentInfoDto? Document,
    string? CustomerEmail,
    IReadOnlyList<InvoiceLineDto> Lines);

/// <summary>Warnings list products the bill pushed below zero. The bill is saved either way.</summary>
public record CreateInvoiceResponse(InvoiceDetailDto Invoice, IReadOnlyList<string> Warnings);

/// <summary>
/// What the bill would come to, worked out by the same code that saves it, so the New Bill screen
/// shows the customer's agreed rates and the tax exactly as the invoice will. Nothing is saved.
/// </summary>
public record InvoicePreviewDto(
    InvoiceDocumentType DocumentType,
    string? PlaceOfSupplyStateCode,
    bool IsInterState,
    bool PricesIncludeTax,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal CessAmount,
    decimal RoundOff,
    decimal TotalAmount,
    IReadOnlyList<InvoiceLineDto> Lines);
