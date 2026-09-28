using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Staff;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Staff.Attendance;

/// <summary>
/// The daily register: pick a date, mark everyone, save once. Built for speed, because it is filled
/// in every working day.
/// </summary>
public class AttendanceService(AppDbContext db)
{
    public async Task<IReadOnlyList<AttendanceStatusDto>> GetStatusesAsync(CancellationToken ct) =>
        await db.AttendanceStatuses
            .OrderBy(s => s.SortOrder)
            .Select(s => new AttendanceStatusDto(s.Id, s.Code, s.Name, s.DayFraction, s.SortOrder))
            .ToListAsync(ct);

    /// <summary>
    /// Changes what a status is worth. Weeks already paid keep the fraction they were paid at, and a
    /// paid half day is not "corrected" by this - only a change of status is.
    /// </summary>
    public async Task<AttendanceStatusDto> UpdateStatusAsync(Guid id, decimal dayFraction, CancellationToken ct)
    {
        if (dayFraction is < 0m or > 1m)
        {
            throw new DomainException("A day's value must be between 0 and 1.");
        }

        var status = await db.AttendanceStatuses.FirstOrDefaultAsync(s => s.Id == id, ct)
                     ?? throw new NotFoundException("Attendance status");

        status.DayFraction = dayFraction;
        await db.SaveChangesAsync(ct);

        return new AttendanceStatusDto(status.Id, status.Code, status.Name, status.DayFraction, status.SortOrder);
    }

    public async Task<AttendanceSheetDto> GetSheetAsync(DateOnly date, CancellationToken ct)
    {
        var weekStart = WageWeek.StartOf(date);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e =>
                (e.IsActive && (e.JoinedOn == null || e.JoinedOn <= date)) ||
                db.AttendanceRecords.Any(a => a.EmployeeId == e.Id && a.WorkDate == date))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        var ids = employees.Select(e => e.Id).ToList();

        var records = await db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.WorkDate == date && ids.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId, ct);

        var paid = await db.WagePayments
            .Where(p => ids.Contains(p.EmployeeId) && p.PeriodStart == weekStart && p.Status == WagePaymentStatus.Paid)
            .Select(p => p.EmployeeId)
            .ToListAsync(ct);

        var userIds = records.Values
            .Select(r => r.UpdatedBy ?? r.CreatedBy)
            .Where(u => u != null)
            .Select(u => u!.Value)
            .Distinct()
            .ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var rows = employees
            .Select(e =>
            {
                records.TryGetValue(e.Id, out var record);
                var changedBy = record?.UpdatedBy ?? record?.CreatedBy;

                return new AttendanceRowDto(
                    e.Id,
                    e.Name,
                    e.Designation,
                    e.IsActive,
                    record?.AttendanceStatusId,
                    paid.Contains(e.Id),
                    record is null ? null : record.UpdatedAt ?? record.CreatedAt,
                    changedBy is { } by ? names.GetValueOrDefault(by) : null);
            })
            .ToList();

        return new AttendanceSheetDto(
            date,
            weekStart,
            weekStart.AddDays(6),
            WageWeek.Describe(weekStart),
            date > IndiaTime.Today(),
            await GetStatusesAsync(ct),
            rows);
    }

    /// <summary>
    /// Saves the day's register in one go. Only rows that actually changed are written. A change in a
    /// week already paid is allowed - that payment is never altered, and the difference turns up as a
    /// correction on the employee's next unpaid week - and the response says so.
    /// </summary>
    public async Task<SaveAttendanceResponse> SaveAsync(SaveAttendanceRequest request, CancellationToken ct)
    {
        var date = request.Date;

        if (date > IndiaTime.Today())
        {
            throw new DomainException("Attendance cannot be marked for a day that has not happened yet.");
        }

        var employeeIds = request.Entries.Select(e => e.EmployeeId).ToList();

        if (employeeIds.Distinct().Count() != employeeIds.Count)
        {
            throw new DomainException("An employee appears twice in the register.");
        }

        var employees = await db.Employees.Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        if (employees.Count != employeeIds.Count)
        {
            throw new NotFoundException("Employee");
        }

        var statusIds = await db.AttendanceStatuses.Select(s => s.Id).ToListAsync(ct);
        var records = await db.AttendanceRecords
            .Where(a => a.WorkDate == date && employeeIds.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId, ct);

        var weekStart = WageWeek.StartOf(date);
        var paid = await db.WagePayments
            .Where(p => employeeIds.Contains(p.EmployeeId) && p.PeriodStart == weekStart && p.Status == WagePaymentStatus.Paid)
            .Select(p => p.EmployeeId)
            .ToListAsync(ct);

        var warnings = new List<string>();
        var changed = 0;

        // Validate every entry before touching anything, so a refused register saves nothing.
        foreach (var entry in request.Entries)
        {
            var employee = employees[entry.EmployeeId];
            records.TryGetValue(entry.EmployeeId, out var record);

            if (entry.StatusId is { } statusId && !statusIds.Contains(statusId))
            {
                throw new NotFoundException("Attendance status");
            }

            if (record is null && entry.StatusId is not null)
            {
                if (!employee.IsActive)
                {
                    throw new DomainException($"{employee.Name} is inactive. Activate them to mark attendance.");
                }

                if (employee.JoinedOn is { } joined && date < joined)
                {
                    throw new DomainException($"{employee.Name} joined on {joined:d MMM yyyy}, after this day.");
                }
            }
        }

        foreach (var entry in request.Entries)
        {
            records.TryGetValue(entry.EmployeeId, out var record);

            if (record?.AttendanceStatusId == entry.StatusId)
            {
                continue;
            }

            if (entry.StatusId is not { } statusId)
            {
                db.AttendanceRecords.Remove(record!);
            }
            else if (record is null)
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    EmployeeId = entry.EmployeeId,
                    WorkDate = date,
                    AttendanceStatusId = statusId
                });
            }
            else
            {
                record.AttendanceStatusId = statusId;
            }

            changed++;

            if (paid.Contains(entry.EmployeeId))
            {
                warnings.Add(
                    $"{employees[entry.EmployeeId].Name}'s wages for {WageWeek.Describe(weekStart)} are already paid. " +
                    "The difference will be settled in their next unpaid week.");
            }
        }

        await db.SaveChangesAsync(ct);

        return new SaveAttendanceResponse(await GetSheetAsync(date, ct), changed, warnings);
    }
}
