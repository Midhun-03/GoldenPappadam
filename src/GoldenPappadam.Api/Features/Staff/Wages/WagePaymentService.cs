using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Domain.Staff;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Staff.Wages;

/// <summary>
/// The Saturday wage screen and what happens when the office marks wages paid: the payment is
/// recorded as a snapshot, and its Employee wages expense is created in the same transaction, once.
/// </summary>
public class WagePaymentService(AppDbContext db, WageCalculator calculator)
{
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public async Task<WageWeekDto> GetWeekAsync(DateOnly anyDayInWeek, CancellationToken ct)
    {
        var weeks = await calculator.CalculateAsync(anyDayInWeek, null, ct);
        var weekStart = WageWeek.StartOf(anyDayInWeek);

        var paidByIds = weeks
            .Where(w => w.Payment?.CreatedBy != null)
            .Select(w => w.Payment!.CreatedBy!.Value)
            .Distinct()
            .ToList();
        var names = await db.Users.Where(u => paidByIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var sortOrder = await db.AttendanceStatuses.ToDictionaryAsync(s => s.Name, s => s.SortOrder, ct);

        var rows = weeks.Select(w => ToDto(w, names, sortOrder)).ToList();

        return new WageWeekDto(
            weekStart,
            weekStart.AddDays(6),
            WageWeek.Describe(weekStart),
            IndiaTime.Today() >= weekStart.AddDays(6),
            rows,
            rows.Where(r => r.Status == "Paid").Sum(r => r.Payment!.Amount),
            rows.Where(r => r.Status == "Pending").Sum(r => r.Payable),
            rows.Count(r => r.Status == "Paid"),
            rows.Count(r => r.Status == "Pending"));
    }

    /// <summary>
    /// Pays the listed employees for the week, all or none. Each payment copies its days, rates and
    /// corrections, and each one with money in it gets exactly one Employee wages expense.
    /// </summary>
    public async Task<PayWagesResponse> PayAsync(PayWagesRequest request, CancellationToken ct)
    {
        var weekStart = WageWeek.StartOf(request.WeekStart);
        var weekEnd = weekStart.AddDays(6);
        var today = IndiaTime.Today();
        var paymentDate = request.PaymentDate ?? today;

        if (request.Method == PaymentMethod.ReturnCredit)
        {
            throw new DomainException("Wages are paid in money. Choose cash, UPI, bank transfer, cheque or other.");
        }

        if (today < weekEnd)
        {
            throw new DomainException(
                $"The week {WageWeek.Describe(weekStart)} ends on Saturday {weekEnd:d MMM}. Its wages can be paid from then.");
        }

        if (paymentDate < weekEnd || paymentDate > today)
        {
            throw new DomainException(
                $"The payment date must be between the week's Saturday ({weekEnd:d MMM yyyy}) and today.");
        }

        var employeeIds = request.Employees.Select(e => e.EmployeeId).ToList();

        if (employeeIds.Distinct().Count() != employeeIds.Count)
        {
            throw new DomainException("Each employee can be paid only once for a week.");
        }

        var weeks = await calculator.CalculateAsync(weekStart, employeeIds, ct);

        var missing = employeeIds.Except(weeks.Select(w => w.Employee.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException("Employee");
        }

        // Every employee is checked before anything is added, so one refusal records nobody.
        var payments = new List<(EmployeeWeek Week, WagePayment Payment)>();

        foreach (var item in request.Employees)
        {
            var week = weeks.Single(w => w.Employee.Id == item.EmployeeId);
            payments.Add((week, BuildPayment(week, request, paymentDate, item.ExpectedAmount)));
        }

        foreach (var (week, payment) in payments)
        {
            db.WagePayments.Add(payment);

            if (payment.Amount > 0m)
            {
                db.Expenses.Add(new Expense
                {
                    CategoryId = KnownExpenseCategories.EmployeeWagesId,
                    ExpenseDate = paymentDate,
                    Amount = payment.Amount,
                    Description = $"Wages · {week.Employee.Name} · {WageWeek.Describe(weekStart)}",
                    PaymentMethod = request.Method,
                    Reference = payment.Reference,
                    WagePayment = payment
                });
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql &&
                                                   UniqueViolationErrors.Contains(sql.Number))
        {
            // Two screens paid the same week at once; the other one got there first.
            throw new DomainException(
                $"Wages for {WageWeek.Describe(weekStart)} were just paid from somewhere else. Reload to see them.");
        }

        await transaction.CommitAsync(ct);

        var dtos = new List<WagePaymentDto>();
        foreach (var (_, payment) in payments)
        {
            dtos.Add(await GetAsync(payment.Id, ct));
        }

        return new PayWagesResponse(dtos, dtos.Sum(p => p.Amount));
    }

    /// <summary>
    /// For a payment recorded that never happened. The payment and its expense stay, marked
    /// cancelled, and the week is unpaid again. Refused while a later payment settled a correction to
    /// this one or took over its carried balance: that later payment has to be cancelled first, or
    /// the same days would be paid twice.
    /// </summary>
    public async Task<WagePaymentDto> CancelAsync(Guid id, CancelWagePaymentRequest request, CancellationToken ct)
    {
        var payment = await db.WagePayments.Include(p => p.Employee).FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Wage payment");

        if (payment.Status == WagePaymentStatus.Cancelled)
        {
            throw new DomainException("This wage payment is already cancelled.");
        }

        var dependent = await db.WagePaymentLines
            .Where(l => l.SourceWagePaymentId == id && l.WagePayment!.Status == WagePaymentStatus.Paid)
            .Select(l => l.WagePayment!)
            .FirstOrDefaultAsync(ct);

        if (dependent is not null)
        {
            throw new DomainException(
                $"The payment for {WageWeek.Describe(dependent.PeriodStart)} settled a correction to this week. " +
                "Cancel that payment first.");
        }

        var reason = request.Reason.Trim();
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.WagePaymentId == id, ct);

        payment.Status = WagePaymentStatus.Cancelled;
        payment.CancelledAt = DateTime.UtcNow;
        payment.CancellationReason = reason;

        if (expense is { Status: ExpenseStatus.Recorded })
        {
            db.ExpenseChanges.Add(new ExpenseChange
            {
                ExpenseId = expense.Id,
                ChangeType = ExpenseChangeType.Cancelled,
                CategoryId = expense.CategoryId,
                ExpenseDate = expense.ExpenseDate,
                Amount = expense.Amount,
                Description = expense.Description,
                PaymentMethod = expense.PaymentMethod,
                Reference = expense.Reference,
                Reason = $"Wage payment cancelled: {reason}"
            });
            expense.Status = ExpenseStatus.Cancelled;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<WagePaymentDto> GetAsync(Guid id, CancellationToken ct) =>
        (await ProjectAsync(db.WagePayments.Where(p => p.Id == id), ct)).FirstOrDefault()
        ?? throw new NotFoundException("Wage payment");

    public Task<IReadOnlyList<WagePaymentDto>> GetAllAsync(
        Guid? employeeId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct) =>
        ProjectAsync(
            db.WagePayments.Where(p =>
                (employeeId == null || p.EmployeeId == employeeId) &&
                (from == null || p.PaymentDate >= from) &&
                (to == null || p.PaymentDate <= to)),
            ct);

    private static WagePayment BuildPayment(EmployeeWeek week, PayWagesRequest request, DateOnly paymentDate, decimal expected)
    {
        var name = week.Employee.Name;

        if (week.IsPaid)
        {
            throw new DomainException(
                $"{name} was already paid for {WageWeek.Describe(week.WeekStart)} on {week.Payment!.PaymentDate:d MMM yyyy}.");
        }

        if (week.HasNothingToPay)
        {
            throw new DomainException($"{name} has nothing to be paid for {WageWeek.Describe(week.WeekStart)}.");
        }

        if (week.MissingWageDates.Count > 0)
        {
            throw new DomainException(
                $"{name} has no daily wage set for {week.MissingWageDates[0]:d MMM yyyy}. Set the wage, then pay.");
        }

        if (week.Payable != expected)
        {
            throw new DomainException(
                $"{name}'s wages for this week are now ₹{week.Payable:0.00}, not ₹{expected:0.00} - attendance or a " +
                "wage changed since the screen was loaded. Check the figures and pay again.");
        }

        var payment = new WagePayment
        {
            EmployeeId = week.Employee.Id,
            PeriodStart = week.WeekStart,
            PeriodEnd = week.WeekEnd,
            PaymentDate = paymentDate,
            Method = request.Method,
            Reference = Clean(request.Reference),
            Notes = Clean(request.Notes),
            DaysWorked = week.DaysWorked,
            WorkAmount = week.WorkAmount,
            AdjustmentAmount = week.AdjustmentAmount,
            Amount = week.Payable,
            CarriedForward = week.CarriedForward
        };

        payment.Lines.AddRange(week.Days.Select(d => new WagePaymentLine
        {
            LineType = WagePaymentLineType.Attendance,
            WorkDate = d.Date,
            AttendanceStatusId = d.StatusId,
            StatusName = d.StatusName,
            DayFraction = d.DayFraction,
            DailyWage = d.DailyWage ?? 0m,
            Amount = d.Amount
        }));

        payment.Lines.AddRange(week.Corrections.Select(c => new WagePaymentLine
        {
            LineType = WagePaymentLineType.Correction,
            WorkDate = c.Date,
            AttendanceStatusId = c.StatusId,
            StatusName = c.StatusName,
            DayFraction = c.DayFraction,
            PreviousStatusName = c.PreviousStatusName,
            PreviousDayFraction = c.PreviousDayFraction,
            DailyWage = c.DailyWage,
            Amount = c.Amount,
            SourceWagePaymentId = c.SourcePaymentId
        }));

        payment.Lines.AddRange(week.Carried.Select(c => new WagePaymentLine
        {
            LineType = WagePaymentLineType.CarriedBalance,
            WorkDate = c.SourceWeekEnd,
            Amount = c.Amount,
            SourceWagePaymentId = c.SourcePaymentId
        }));

        return payment;
    }

    private static EmployeeWageDto ToDto(
        EmployeeWeek week,
        IReadOnlyDictionary<Guid, string> names,
        IReadOnlyDictionary<string, int> sortOrder)
    {
        var employee = week.Employee;
        var changed = week.ChangedSincePaid.Select(DescribeCorrection).ToList();

        if (week.Payment is { } payment)
        {
            // Paid: everything from the snapshot, exactly as it was handed over.
            var dayLines = payment.Lines
                .Where(l => l.LineType == WagePaymentLineType.Attendance)
                .OrderBy(l => l.WorkDate)
                .Select(l => new WageDayDto(l.WorkDate, l.AttendanceStatusId, l.StatusName ?? WageCalculator.NotRecorded,
                    l.DayFraction, l.DailyWage, l.Amount))
                .ToList();

            var adjustments = payment.Lines
                .Where(l => l.LineType != WagePaymentLineType.Attendance)
                .OrderBy(l => l.WorkDate)
                .Select(l => l.LineType == WagePaymentLineType.Correction
                    ? DescribeCorrection(new WageCorrection(l.WorkDate, l.SourceWagePaymentId ?? Guid.Empty,
                        l.PreviousStatusName, l.PreviousDayFraction ?? 0m, l.AttendanceStatusId,
                        l.StatusName ?? WageCalculator.NotRecorded, l.DayFraction, l.DailyWage, l.Amount))
                    : DescribeCarried(new WageCarried(l.SourceWagePaymentId ?? Guid.Empty, l.WorkDate, l.Amount)))
                .ToList();

            return new EmployeeWageDto(
                employee.Id,
                employee.Name,
                employee.Designation,
                employee.IsActive,
                "Paid",
                RatesUsed(dayLines),
                dayLines,
                CountStatuses(dayLines, sortOrder),
                dayLines.Count(d => d.StatusId is null),
                payment.DaysWorked,
                payment.WorkAmount,
                adjustments,
                payment.AdjustmentAmount,
                payment.Amount,
                payment.CarriedForward,
                [],
                new WagePaymentSummaryDto(
                    payment.Id,
                    payment.PaymentDate,
                    payment.Method,
                    payment.Reference,
                    payment.Amount,
                    payment.CreatedAt,
                    payment.CreatedBy is { } by ? names.GetValueOrDefault(by) : null),
                changed,
                changed.Sum(c => c.Amount));
        }

        var days = week.Days
            .Select(d => new WageDayDto(d.Date, d.StatusId, d.StatusName, d.DayFraction, d.DailyWage, d.Amount))
            .ToList();

        return new EmployeeWageDto(
            employee.Id,
            employee.Name,
            employee.Designation,
            employee.IsActive,
            week.HasNothingToPay ? "NothingToPay" : "Pending",
            RatesUsed(days),
            days,
            CountStatuses(days, sortOrder),
            days.Count(d => d.StatusId is null),
            week.DaysWorked,
            week.WorkAmount,
            week.Corrections.Select(DescribeCorrection).Concat(week.Carried.Select(DescribeCarried)).ToList(),
            week.AdjustmentAmount,
            week.Payable,
            week.CarriedForward,
            week.MissingWageDates,
            null,
            [],
            0m);
    }

    /// <summary>The rates the worked days were paid at - one, or two when a raise fell mid-week.</summary>
    private static IReadOnlyList<decimal> RatesUsed(IEnumerable<WageDayDto> days)
    {
        var worked = days.Where(d => d.DayFraction > 0m && d.DailyWage is not null).ToList();
        var source = worked.Count > 0 ? worked : days.Where(d => d.DailyWage is not null).ToList();

        return source.Select(d => d.DailyWage!.Value).Distinct().ToList();
    }

    private static IReadOnlyList<StatusCountDto> CountStatuses(
        IEnumerable<WageDayDto> days,
        IReadOnlyDictionary<string, int> sortOrder) =>
        days
            .Where(d => d.StatusId is not null)
            .GroupBy(d => d.StatusName)
            .OrderBy(g => sortOrder.GetValueOrDefault(g.Key, int.MaxValue))
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToList();

    private static WageAdjustmentDto DescribeCorrection(WageCorrection c) =>
        new(
            "Correction",
            c.Date,
            $"{c.Date:ddd d MMM} corrected: {c.PreviousStatusName ?? WageCalculator.NotRecorded} → {c.StatusName}",
            c.Amount);

    private static WageAdjustmentDto DescribeCarried(WageCarried c) =>
        new(
            "CarriedBalance",
            c.SourceWeekEnd,
            $"Overpayment carried from {WageWeek.Describe(WageWeek.StartOf(c.SourceWeekEnd))}",
            c.Amount);

    private async Task<IReadOnlyList<WagePaymentDto>> ProjectAsync(IQueryable<WagePayment> query, CancellationToken ct)
    {
        var payments = await query
            .AsNoTracking()
            .Include(p => p.Employee)
            .Include(p => p.Lines)
            .OrderByDescending(p => p.PeriodStart)
            .ThenBy(p => p.Employee!.Name)
            .ToListAsync(ct);

        var ids = payments.Select(p => p.Id).ToList();
        var expenses = await db.Expenses
            .Where(e => e.WagePaymentId != null && ids.Contains(e.WagePaymentId.Value))
            .ToDictionaryAsync(e => e.WagePaymentId!.Value, e => e.Id, ct);

        var userIds = payments
            .SelectMany(p => new[] { p.CreatedBy, p.Status == WagePaymentStatus.Cancelled ? p.UpdatedBy : null })
            .Where(u => u != null)
            .Select(u => u!.Value)
            .Distinct()
            .ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return payments
            .Select(p => new WagePaymentDto(
                p.Id,
                p.EmployeeId,
                p.Employee!.Name,
                p.PeriodStart,
                p.PeriodEnd,
                WageWeek.Describe(p.PeriodStart),
                p.PaymentDate,
                p.Method,
                p.Reference,
                p.Notes,
                p.DaysWorked,
                p.WorkAmount,
                p.AdjustmentAmount,
                p.Amount,
                p.CarriedForward,
                p.Status,
                p.CreatedAt,
                p.CreatedBy is { } by ? names.GetValueOrDefault(by) : null,
                p.CancelledAt,
                p.Status == WagePaymentStatus.Cancelled && p.UpdatedBy is { } cancelledBy
                    ? names.GetValueOrDefault(cancelledBy)
                    : null,
                p.CancellationReason,
                expenses.TryGetValue(p.Id, out var expenseId) ? expenseId : null,
                p.Lines
                    .OrderBy(l => l.LineType)
                    .ThenBy(l => l.WorkDate)
                    .Select(l => new WagePaymentLineDto(
                        l.LineType, l.WorkDate, l.StatusName, l.DayFraction, l.PreviousStatusName,
                        l.PreviousDayFraction, l.DailyWage, l.Amount))
                    .ToList()))
            .ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
