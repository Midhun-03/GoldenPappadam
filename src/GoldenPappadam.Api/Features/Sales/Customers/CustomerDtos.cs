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
    bool AddedBySalesperson);

public record SaveCustomerRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? ContactPerson,
    [MaxLength(20)] string? Phone,
    [MaxLength(300)] string? Address,
    decimal OpeningBalance,
    [MaxLength(300)] string? Notes,
    bool HasMultipleBranches);

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
