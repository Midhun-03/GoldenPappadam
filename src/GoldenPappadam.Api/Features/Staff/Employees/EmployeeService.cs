using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Staff;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Staff.Employees;

public class EmployeeService(AppDbContext db)
{
    public async Task<IReadOnlyList<EmployeeDto>> GetAllAsync(string? search, bool includeInactive, CancellationToken ct)
    {
        var employees = await db.Employees
            .AsNoTracking()
            .Include(e => e.WageRates)
            .Where(e => includeInactive || e.IsActive)
            .Where(e => search == null || e.Name.Contains(search) || e.Designation!.Contains(search) || e.Phone!.Contains(search))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        var today = IndiaTime.Today();

        return employees.Select(e => ToDto(e, today)).ToList();
    }

    public async Task<EmployeeDto> GetAsync(Guid id, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.WageRates).FirstOrDefaultAsync(e => e.Id == id, ct)
                       ?? throw new NotFoundException("Employee");

        return ToDto(employee, IndiaTime.Today());
    }

    /// <summary>A new employee starts with a wage: without one there is nothing to pay them at.</summary>
    public async Task<EmployeeDto> CreateAsync(SaveEmployeeRequest request, CancellationToken ct)
    {
        if (request.DailyWage is not > 0m)
        {
            throw new DomainException("Enter the daily wage.");
        }

        var employee = new Employee
        {
            Name = request.Name.Trim(),
            Designation = Clean(request.Designation),
            Phone = Clean(request.Phone),
            Address = Clean(request.Address),
            JoinedOn = request.JoinedOn
        };

        employee.WageRates.Add(new EmployeeWageRate
        {
            DailyWage = request.DailyWage.Value,
            EffectiveFrom = request.WageEffectiveFrom ?? request.JoinedOn ?? IndiaTime.Today()
        });

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        return await GetAsync(employee.Id, ct);
    }

    /// <summary>Details only. The wage is changed through <see cref="AddWageRateAsync"/>, which keeps history.</summary>
    public async Task<EmployeeDto> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken ct)
    {
        var employee = await FindAsync(id, ct);

        employee.Name = request.Name.Trim();
        employee.Designation = Clean(request.Designation);
        employee.Phone = Clean(request.Phone);
        employee.Address = Clean(request.Address);
        employee.JoinedOn = request.JoinedOn;

        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>Deactivated, never deleted: attendance, wage history and payments all stay.</summary>
    public async Task<EmployeeDto> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var employee = await FindAsync(id, ct);
        employee.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<WageRateDto>> GetWageRatesAsync(Guid employeeId, CancellationToken ct)
    {
        await FindAsync(employeeId, ct);

        var rates = await db.EmployeeWageRates
            .AsNoTracking()
            .Where(r => r.EmployeeId == employeeId)
            .ToListAsync(ct);

        var userIds = rates.Where(r => r.CreatedBy != null).Select(r => r.CreatedBy!.Value).Distinct().ToList();
        var names = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var today = IndiaTime.Today();
        var current = WageRates.InEffectOn(rates, today);

        return rates
            .OrderByDescending(r => r.EffectiveFrom)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new WageRateDto(
                r.Id,
                r.DailyWage,
                r.EffectiveFrom,
                r.CreatedAt,
                r.CreatedBy is { } by ? names.GetValueOrDefault(by) : null,
                r.Id == current?.Id,
                r.EffectiveFrom > today,
                rates.Any(other => other.EffectiveFrom == r.EffectiveFrom && other.CreatedAt > r.CreatedAt)))
            .ToList();
    }

    /// <summary>
    /// A new wage from a date. It never replaces the old row, so earlier days keep the rate they had.
    /// It may not start inside a week already paid: that money has been handed over at the old rate,
    /// and a raise that reached back into it would make the records disagree with what was paid.
    /// </summary>
    public async Task<IReadOnlyList<WageRateDto>> AddWageRateAsync(Guid employeeId, AddWageRateRequest request, CancellationToken ct)
    {
        var employee = await FindAsync(employeeId, ct);

        if (request.DailyWage <= 0m)
        {
            throw new DomainException("A daily wage must be more than zero.");
        }

        var paidUpTo = await db.WagePayments
            .Where(p => p.EmployeeId == employeeId && p.Status == WagePaymentStatus.Paid)
            .MaxAsync(p => (DateOnly?)p.PeriodEnd, ct);

        if (paidUpTo is { } lastPaid && request.EffectiveFrom <= lastPaid)
        {
            throw new DomainException(
                $"{employee.Name}'s wages are paid up to {lastPaid:d MMM yyyy}. A new wage can start from " +
                $"{lastPaid.AddDays(1):d MMM yyyy} at the earliest; weeks already paid keep the rate they were paid at.");
        }

        // The same wage from the same day again (a double tap) is not a change, and must not add noise.
        var sameDay = await db.EmployeeWageRates
            .Where(r => r.EmployeeId == employeeId && r.EffectiveFrom == request.EffectiveFrom)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (sameDay?.DailyWage != request.DailyWage)
        {
            db.EmployeeWageRates.Add(new EmployeeWageRate
            {
                EmployeeId = employeeId,
                DailyWage = request.DailyWage,
                EffectiveFrom = request.EffectiveFrom
            });
            await db.SaveChangesAsync(ct);
        }

        return await GetWageRatesAsync(employeeId, ct);
    }

    private static EmployeeDto ToDto(Employee e, DateOnly today)
    {
        var current = WageRates.InEffectOn(e.WageRates, today);
        var upcoming = WageRates.UpcomingAfter(e.WageRates, today);

        return new EmployeeDto(
            e.Id,
            e.Name,
            e.Designation,
            e.Phone,
            e.Address,
            e.JoinedOn,
            e.IsActive,
            current?.DailyWage,
            current?.EffectiveFrom,
            upcoming?.DailyWage,
            upcoming?.EffectiveFrom,
            e.CreatedAt);
    }

    private async Task<Employee> FindAsync(Guid id, CancellationToken ct) =>
        await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Employee");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
