using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Staff.Attendance;

public record AttendanceStatusDto(Guid Id, string Code, string Name, decimal DayFraction, int SortOrder);

/// <summary>
/// One day's register: every active employee, plus anyone since deactivated who has a record that
/// day. WeekPaid means a change now is carried to the employee's next unpaid week.
/// </summary>
public record AttendanceSheetDto(
    DateOnly Date,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string WeekLabel,
    bool IsFuture,
    IReadOnlyList<AttendanceStatusDto> Statuses,
    IReadOnlyList<AttendanceRowDto> Rows);

public record AttendanceRowDto(
    Guid EmployeeId,
    string Name,
    string? Designation,
    bool IsActive,
    Guid? StatusId,
    bool WeekPaid,
    DateTime? ChangedAt,
    string? ChangedByName);

/// <summary>A null StatusId clears the day back to "not recorded".</summary>
public record AttendanceEntryRequest(Guid EmployeeId, Guid? StatusId);

public record SaveAttendanceRequest(DateOnly Date, [Required] IReadOnlyList<AttendanceEntryRequest> Entries);

public record SaveAttendanceResponse(AttendanceSheetDto Sheet, int Changed, IReadOnlyList<string> Warnings);

public record UpdateAttendanceStatusRequest(decimal DayFraction);
