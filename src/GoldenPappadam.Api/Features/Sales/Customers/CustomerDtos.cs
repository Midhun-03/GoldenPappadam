using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Sales.Customers;

public record CustomerDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Address,
    decimal OpeningBalance,
    decimal Balance,
    string? Notes,
    bool IsActive,
    bool HasMultipleBranches,
    int ActiveBranchCount,
    string? CreatedByName,
    bool AddedBySalesperson,
    string? Email,
    string? Gstin,
    string? StateCode,
    bool IsGstRegistered);

/// <summary>
/// IsGstRegistered is the "GST customer" tick box: set, the GSTIN is required and the shop gets GST
/// bills; clear, it gets normal bills and must have no GSTIN. Only the GSTIN is stored - having one is
/// what being registered means, so the two can never disagree.
/// </summary>
public record SaveCustomerRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? ContactPerson,
    [MaxLength(20)] string? Phone,
    [MaxLength(300)] string? Address,
    decimal OpeningBalance,
    [MaxLength(300)] string? Notes,
    bool HasMultipleBranches,
    [MaxLength(256), EmailAddress] string? Email = null,
    [MaxLength(15)] string? Gstin = null,
    [StringLength(2, MinimumLength = 2)] string? StateCode = null,
    bool IsGstRegistered = false);

/// <summary>One row of the customer's account: what was billed, what was paid, what is left.</summary>
public record LedgerEntryDto(
    DateOnly Date,
    string EntryType,
    string Reference,
    string? Description,
    decimal Billed,
    decimal Paid,
    decimal Balance);

/// <summary>A bill with money still outstanding, for allocating a payment by hand.</summary>
public record OutstandingInvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding);
