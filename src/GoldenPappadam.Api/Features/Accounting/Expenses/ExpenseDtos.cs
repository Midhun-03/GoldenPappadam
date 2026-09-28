using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Accounting.Expenses;

/// <summary>
/// IsSystem marks Employee wages: only wage payments put expenses in it, so it cannot be picked,
/// renamed or deactivated by hand.
/// </summary>
public record ExpenseCategoryDto(Guid Id, string Name, bool IsActive, bool IsSystem);

public record SaveExpenseCategoryRequest([Required, MaxLength(100)] string Name);

/// <summary>
/// A wage expense (IsWageExpense) belongs to its wage payment and cannot be edited or cancelled here.
/// VersionCount is how many earlier versions an edit or cancellation left behind.
/// </summary>
public record ExpenseDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    PaymentMethod? PaymentMethod,
    string? Reference,
    ExpenseStatus Status,
    bool IsWageExpense,
    Guid? WagePaymentId,
    Guid? EmployeeId,
    DateTime CreatedAt,
    string? CreatedByName,
    DateTime? UpdatedAt,
    string? UpdatedByName,
    int VersionCount);

/// <summary>Reason is optional for an edit and saved with the version it replaces.</summary>
public record SaveExpenseRequest(
    Guid CategoryId,
    DateOnly ExpenseDate,
    decimal Amount,
    [MaxLength(300)] string? Description,
    PaymentMethod? PaymentMethod,
    [MaxLength(100)] string? Reference,
    [MaxLength(300)] string? Reason = null);

public record CancelExpenseRequest([Required, MaxLength(300)] string Reason);

/// <summary>
/// One version of an expense. The last one is the expense as it stands; the ones before it are what
/// each edit or cancellation replaced, with who made that change and when.
/// </summary>
public record ExpenseVersionDto(
    string Change,
    string CategoryName,
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    PaymentMethod? PaymentMethod,
    string? Reference,
    DateTime ChangedAt,
    string? ChangedByName,
    string? Reason);

public record ExpenseCategoryTotalDto(Guid CategoryId, string Name, decimal Total, int Count);

/// <summary>Totals per category for a period, cancelled expenses left out.</summary>
public record ExpenseSummaryDto(
    DateOnly? From,
    DateOnly? To,
    decimal Total,
    int Count,
    IReadOnlyList<ExpenseCategoryTotalDto> Categories);
