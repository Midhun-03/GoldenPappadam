using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Sales.Settings;

/// <summary>
/// GstEnabled is not stored: it is simply whether a GSTIN is on file. NextInvoiceNumber is what
/// the next bill dated today would be called - shown, never reserved. SalesmanDiscountCapPercent is the
/// most a salesman may take off a bill from the phone, as a percentage of its subtotal; 0 switches
/// phone discounts off, and leaving it out of a save keeps what it was.
/// </summary>
public record InvoiceSettingsDto(
    string LegalName,
    string? Address,
    string? Phone,
    string? Email,
    string? Gstin,
    string StateCode,
    string SeriesCode,
    bool PricesIncludeTax,
    bool RoundToNearestRupee,
    string? PaymentTerms,
    string? BankDetails,
    string? TermsAndConditions,
    bool GstEnabled,
    string NextInvoiceNumber,
    DateTime? UpdatedAt,
    decimal SalesmanDiscountCapPercent = 20m);

public record SaveInvoiceSettingsRequest(
    [Required, MaxLength(150)] string LegalName,
    [MaxLength(300)] string? Address,
    [MaxLength(20)] string? Phone,
    [MaxLength(256), EmailAddress] string? Email,
    [MaxLength(30)] string? Gstin,
    [Required, StringLength(2, MinimumLength = 2)] string StateCode,
    [Required, StringLength(3, MinimumLength = 1)] string SeriesCode,
    bool PricesIncludeTax,
    bool RoundToNearestRupee,
    [MaxLength(200)] string? PaymentTerms,
    [MaxLength(500)] string? BankDetails,
    [MaxLength(1000)] string? TermsAndConditions,
    [Range(typeof(decimal), "0", "100")] decimal? SalesmanDiscountCapPercent = null);

public record StateDto(string Code, string Name);
