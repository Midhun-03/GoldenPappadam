using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Sales.Settings;

/// <summary>
/// GstEnabled is not stored: it is simply whether a GSTIN is on file. NextInvoiceNumber is what
/// the next bill dated today would be called - shown, never reserved.
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
    DateTime? UpdatedAt);

public record SaveInvoiceSettingsRequest(
    [Required, MaxLength(150)] string LegalName,
    [MaxLength(300)] string? Address,
    [MaxLength(20)] string? Phone,
    [MaxLength(256), EmailAddress] string? Email,
    [MaxLength(15)] string? Gstin,
    [Required, StringLength(2, MinimumLength = 2)] string StateCode,
    [Required, StringLength(3, MinimumLength = 1)] string SeriesCode,
    bool PricesIncludeTax,
    bool RoundToNearestRupee,
    [MaxLength(200)] string? PaymentTerms,
    [MaxLength(500)] string? BankDetails,
    [MaxLength(1000)] string? TermsAndConditions);

public record StateDto(string Code, string Name);
