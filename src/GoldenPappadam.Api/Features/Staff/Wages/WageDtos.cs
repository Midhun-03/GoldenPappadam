using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Domain.Staff;

namespace GoldenPappadam.Api.Features.Staff.Wages;

/// <summary>
/// Everyone's wages for one Sunday-to-Saturday week. CanPay is false until the week's Saturday:
/// wages are paid on it, not before.
/// </summary>
public record WageWeekDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string Label,
    bool CanPay,
    IReadOnlyList<EmployeeWageDto> Employees,
    decimal PaidTotal,
    decimal PendingTotal,
    int PaidCount,
    int PendingCount);

/// <summary>
/// One employee's week. Status is Paid, Pending or NothingToPay. For a paid week every figure comes
/// from the payment as it was recorded, never recalculated; ChangedSincePaid then lists attendance
/// corrected afterwards, which the next unpaid week will settle.
/// </summary>
public record EmployeeWageDto(
    Guid EmployeeId,
    string Name,
    string? Designation,
    bool IsActive,
    string Status,
    IReadOnlyList<decimal> DailyWages,
    IReadOnlyList<WageDayDto> Days,
    IReadOnlyList<StatusCountDto> StatusCounts,
    int NotRecordedDays,
    decimal DaysWorked,
    decimal WorkAmount,
    IReadOnlyList<WageAdjustmentDto> Adjustments,
    decimal AdjustmentAmount,
    decimal Payable,
    decimal CarriedForward,
    IReadOnlyList<DateOnly> MissingWageDates,
    WagePaymentSummaryDto? Payment,
    IReadOnlyList<WageAdjustmentDto> ChangedSincePaid,
    decimal ChangedSincePaidAmount);

public record WageDayDto(
    DateOnly Date,
    Guid? StatusId,
    string StatusName,
    decimal DayFraction,
    decimal? DailyWage,
    decimal Amount);

public record StatusCountDto(string Name, int Days);

/// <summary>Kind is Correction or CarriedBalance.</summary>
public record WageAdjustmentDto(string Kind, DateOnly Date, string Description, decimal Amount);

public record WagePaymentSummaryDto(
    Guid Id,
    DateOnly PaymentDate,
    PaymentMethod Method,
    string? Reference,
    decimal Amount,
    DateTime PaidAt,
    string? PaidByName);

public record WagePaymentDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string PeriodLabel,
    DateOnly PaymentDate,
    PaymentMethod Method,
    string? Reference,
    string? Notes,
    decimal DaysWorked,
    decimal WorkAmount,
    decimal AdjustmentAmount,
    decimal Amount,
    decimal CarriedForward,
    WagePaymentStatus Status,
    DateTime PaidAt,
    string? PaidByName,
    DateTime? CancelledAt,
    string? CancelledByName,
    string? CancellationReason,
    Guid? ExpenseId,
    IReadOnlyList<WagePaymentLineDto> Lines);

public record WagePaymentLineDto(
    WagePaymentLineType LineType,
    DateOnly WorkDate,
    string? StatusName,
    decimal DayFraction,
    string? PreviousStatusName,
    decimal? PreviousDayFraction,
    decimal DailyWage,
    decimal Amount);

/// <summary>
/// Pays one or more employees for a week, all or none. ExpectedAmount is what the office was shown:
/// if attendance changed since the screen loaded, the payment is refused rather than recording a
/// figure nobody looked at.
/// </summary>
public record PayWagesRequest(
    DateOnly WeekStart,
    DateOnly? PaymentDate,
    PaymentMethod Method,
    [MaxLength(100)] string? Reference,
    [MaxLength(300)] string? Notes,
    [Required, MinLength(1)] IReadOnlyList<PayEmployeeRequest> Employees);

public record PayEmployeeRequest(Guid EmployeeId, decimal ExpectedAmount);

public record PayWagesResponse(IReadOnlyList<WagePaymentDto> Payments, decimal Total);

public record CancelWagePaymentRequest([Required, MaxLength(300)] string Reason);
