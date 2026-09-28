using GoldenPappadam.Domain.Staff;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Staff.Wages;

/// <summary>
/// Works out what each employee is owed for one Sunday-to-Saturday week. The screen and the payment
/// both come from here, so what the office is shown is exactly what gets recorded.
///
/// Three things make up a week:
/// <list type="bullet">
/// <item>its seven days: each day's fraction × the daily wage in effect that day;</item>
/// <item>corrections: days in earlier, already-paid weeks whose attendance changed after they were
/// paid - the paid week is never touched, the difference is settled here;</item>
/// <item>a carried balance: an earlier overpayment too large to recover in its own week.</item>
/// </list>
/// </summary>
public class WageCalculator(AppDbContext db)
{
    public const string NotRecorded = "Not recorded";

    /// <param name="anyDayInWeek">Any date; the week is the Sunday-to-Saturday containing it.</param>
    /// <param name="employeeIds">Only these employees; null for everyone the week concerns.</param>
    public async Task<IReadOnlyList<EmployeeWeek>> CalculateAsync(
        DateOnly anyDayInWeek,
        IReadOnlyCollection<Guid>? employeeIds,
        CancellationToken ct)
    {
        var weekStart = WageWeek.StartOf(anyDayInWeek);
        var weekEnd = weekStart.AddDays(6);

        var statuses = await db.AttendanceStatuses.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);

        var employeeQuery = db.Employees.AsNoTracking().Include(e => e.WageRates).AsQueryable();
        employeeQuery = employeeIds is not null
            ? employeeQuery.Where(e => employeeIds.Contains(e.Id))
            // Active staff, plus anyone since deactivated who worked or was paid that week.
            : employeeQuery.Where(e =>
                e.IsActive ||
                db.AttendanceRecords.Any(a => a.EmployeeId == e.Id && a.WorkDate >= weekStart && a.WorkDate <= weekEnd) ||
                db.WagePayments.Any(p =>
                    p.EmployeeId == e.Id && p.PeriodStart == weekStart && p.Status == WagePaymentStatus.Paid));

        var employees = await employeeQuery.OrderBy(e => e.Name).ToListAsync(ct);
        var ids = employees.Select(e => e.Id).ToList();

        var payments = await db.WagePayments
            .AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => ids.Contains(p.EmployeeId) && p.Status == WagePaymentStatus.Paid)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        // This week, and every earlier day that has been paid, since any of those may need a correction.
        var from = payments.Count > 0 ? Min(payments.Min(p => p.PeriodStart), weekStart) : weekStart;
        var attendance = await db.AttendanceRecords
            .AsNoTracking()
            .Where(a => ids.Contains(a.EmployeeId) && a.WorkDate >= from && a.WorkDate <= weekEnd)
            .ToDictionaryAsync(a => (a.EmployeeId, a.WorkDate), ct);

        return employees
            .Select(employee => CalculateOne(
                employee,
                weekStart,
                statuses,
                attendance,
                payments.Where(p => p.EmployeeId == employee.Id).ToList()))
            .ToList();
    }

    private static EmployeeWeek CalculateOne(
        Employee employee,
        DateOnly weekStart,
        IReadOnlyDictionary<Guid, AttendanceStatus> statuses,
        IReadOnlyDictionary<(Guid, DateOnly), AttendanceRecord> attendance,
        IReadOnlyList<WagePayment> payments)
    {
        var days = WageWeek.Days(weekStart)
            .Select(date =>
            {
                var status = attendance.TryGetValue((employee.Id, date), out var record)
                    ? statuses[record.AttendanceStatusId]
                    : null;
                var fraction = status?.DayFraction ?? 0m;
                var rate = WageRates.InEffectOn(employee.WageRates, date)?.DailyWage;

                return new WageDay(
                    date,
                    status?.Id,
                    status?.Name ?? NotRecorded,
                    fraction,
                    rate,
                    rate is { } wage ? WageRates.Pay(fraction, wage) : 0m);
            })
            .ToList();

        var paidDays = PaidDays(payments);
        var pending = PendingCorrections(employee.Id, paidDays, statuses, attendance);

        var consumedCarries = payments
            .SelectMany(p => p.Lines)
            .Where(l => l.LineType == WagePaymentLineType.CarriedBalance && l.SourceWagePaymentId != null)
            .Select(l => l.SourceWagePaymentId!.Value)
            .ToHashSet();

        var carried = payments
            .Where(p => p.CarriedForward < 0m && p.PeriodStart < weekStart && !consumedCarries.Contains(p.Id))
            .Select(p => new WageCarried(p.Id, p.PeriodEnd, p.CarriedForward))
            .ToList();

        return new EmployeeWeek(
            employee,
            weekStart,
            days,
            // Only earlier weeks' corrections are settled here; a week never settles itself.
            pending.Where(c => c.Date < weekStart).ToList(),
            carried,
            payments.FirstOrDefault(p => p.PeriodStart == weekStart),
            pending.Where(c => c.Date >= weekStart && c.Date <= weekStart.AddDays(6)).ToList());
    }

    /// <summary>
    /// What each paid day currently stands at: the original day line, moved on by any correction
    /// paid since. The rate stays the one the day was first paid at.
    /// </summary>
    private static Dictionary<DateOnly, PaidDay> PaidDays(IEnumerable<WagePayment> payments)
    {
        var paid = new Dictionary<DateOnly, PaidDay>();

        foreach (var payment in payments)
        {
            foreach (var line in payment.Lines.Where(l => l.LineType == WagePaymentLineType.Attendance))
            {
                paid[line.WorkDate] = new PaidDay(
                    payment.Id, line.AttendanceStatusId, line.StatusName, line.DayFraction, line.DailyWage, line.Amount);
            }
        }

        // Corrections after the originals, in the order they were paid.
        foreach (var payment in payments)
        {
            foreach (var line in payment.Lines.Where(l => l.LineType == WagePaymentLineType.Correction))
            {
                if (paid.TryGetValue(line.WorkDate, out var day))
                {
                    paid[line.WorkDate] = day with
                    {
                        StatusId = line.AttendanceStatusId,
                        StatusName = line.StatusName,
                        DayFraction = line.DayFraction,
                        Amount = day.Amount + line.Amount
                    };
                }
            }
        }

        return paid;
    }

    /// <summary>Paid days whose attendance now says something different from what was paid.</summary>
    private static List<WageCorrection> PendingCorrections(
        Guid employeeId,
        Dictionary<DateOnly, PaidDay> paidDays,
        IReadOnlyDictionary<Guid, AttendanceStatus> statuses,
        IReadOnlyDictionary<(Guid, DateOnly), AttendanceRecord> attendance)
    {
        var corrections = new List<WageCorrection>();

        foreach (var (date, paid) in paidDays.OrderBy(d => d.Key))
        {
            var status = attendance.TryGetValue((employeeId, date), out var record)
                ? statuses[record.AttendanceStatusId]
                : null;

            // Compared by status, not fraction: changing what "Half day" is worth later must not
            // rewrite the half days already paid.
            if (status?.Id == paid.StatusId)
            {
                continue;
            }

            var fraction = status?.DayFraction ?? 0m;
            var amount = WageRates.Pay(fraction, paid.DailyWage) - paid.Amount;

            // Absent to Leave: a different word, the same money.
            if (amount == 0m && fraction == paid.DayFraction)
            {
                continue;
            }

            corrections.Add(new WageCorrection(
                date,
                paid.SourcePaymentId,
                paid.StatusName,
                paid.DayFraction,
                status?.Id,
                status?.Name ?? NotRecorded,
                fraction,
                paid.DailyWage,
                amount));
        }

        return corrections;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    private sealed record PaidDay(
        Guid SourcePaymentId,
        Guid? StatusId,
        string? StatusName,
        decimal DayFraction,
        decimal DailyWage,
        decimal Amount);
}

public sealed record WageDay(
    DateOnly Date,
    Guid? StatusId,
    string StatusName,
    decimal DayFraction,
    decimal? DailyWage,
    decimal Amount);

public sealed record WageCorrection(
    DateOnly Date,
    Guid SourcePaymentId,
    string? PreviousStatusName,
    decimal PreviousDayFraction,
    Guid? StatusId,
    string StatusName,
    decimal DayFraction,
    decimal DailyWage,
    decimal Amount);

public sealed record WageCarried(Guid SourcePaymentId, DateOnly SourceWeekEnd, decimal Amount);

/// <summary>One employee's week, worked out. <see cref="Payment"/> is set when it has been paid.</summary>
public sealed record EmployeeWeek(
    Employee Employee,
    DateOnly WeekStart,
    IReadOnlyList<WageDay> Days,
    IReadOnlyList<WageCorrection> Corrections,
    IReadOnlyList<WageCarried> Carried,
    WagePayment? Payment,
    IReadOnlyList<WageCorrection> ChangedSincePaid)
{
    public DateOnly WeekEnd => WeekStart.AddDays(6);

    public decimal DaysWorked => Days.Sum(d => d.DayFraction);

    public decimal WorkAmount => Days.Sum(d => d.Amount);

    public decimal AdjustmentAmount => Corrections.Sum(c => c.Amount) + Carried.Sum(c => c.Amount);

    public decimal Total => WorkAmount + AdjustmentAmount;

    /// <summary>What to hand over. Never negative.</summary>
    public decimal Payable => Math.Max(Total, 0m);

    /// <summary>An overpayment larger than this week's pay, carried on to the next.</summary>
    public decimal CarriedForward => Math.Min(Total, 0m);

    /// <summary>Days worked with no daily wage set for them - the week cannot be paid until one is.</summary>
    public IReadOnlyList<DateOnly> MissingWageDates =>
        Days.Where(d => d.DayFraction > 0m && d.DailyWage is null).Select(d => d.Date).ToList();

    public bool IsPaid => Payment is not null;

    /// <summary>Nothing worked and nothing to settle: there is no payment to make.</summary>
    public bool HasNothingToPay => !IsPaid && DaysWorked == 0m && Corrections.Count == 0;
}
