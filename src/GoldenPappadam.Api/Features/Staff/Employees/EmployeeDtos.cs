using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Staff.Employees;

/// <summary>
/// CurrentDailyWage is the rate in effect today; a raise already entered for a later date shows as
/// the upcoming one, so the office can see it is coming.
/// </summary>
public record EmployeeDto(
    Guid Id,
    string Name,
    string? Designation,
    string? Phone,
    string? Address,
    DateOnly? JoinedOn,
    bool IsActive,
    decimal? CurrentDailyWage,
    DateOnly? CurrentWageFrom,
    decimal? UpcomingDailyWage,
    DateOnly? UpcomingWageFrom,
    DateTime CreatedAt);

/// <summary>
/// DailyWage and WageEffectiveFrom are used only when the employee is created: after that a wage is
/// changed through its own endpoint, which keeps the history, never by editing the employee.
/// </summary>
public record SaveEmployeeRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? Designation,
    [MaxLength(20)] string? Phone,
    [MaxLength(300)] string? Address,
    DateOnly? JoinedOn,
    decimal? DailyWage = null,
    DateOnly? WageEffectiveFrom = null);

public record AddWageRateRequest(decimal DailyWage, DateOnly EffectiveFrom);

/// <summary>
/// One entry in the wage history. IsSuperseded marks a rate replaced by a later entry with the same
/// start date - a correction, kept because nothing is ever overwritten.
/// </summary>
public record WageRateDto(
    Guid Id,
    decimal DailyWage,
    DateOnly EffectiveFrom,
    DateTime SetAt,
    string? SetByName,
    bool IsCurrent,
    bool IsUpcoming,
    bool IsSuperseded);
