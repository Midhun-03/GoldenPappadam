using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Staff;
using GoldenPappadam.Api.Features.Staff.Attendance;
using GoldenPappadam.Api.Features.Staff.Employees;
using GoldenPappadam.Api.Features.Staff.Wages;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Employees, their daily wages, attendance and the Saturday wage payment. The weeks used are always
/// in the past, because a week can only be paid once its Saturday has come.
/// </summary>
public class StaffWageTests : IAsyncLifetime
{
    private static readonly Guid Present = KnownAttendanceStatuses.PresentId;
    private static readonly Guid HalfDay = KnownAttendanceStatuses.HalfDayId;
    private static readonly Guid Absent = KnownAttendanceStatuses.AbsentId;
    private static readonly Guid Leave = KnownAttendanceStatuses.LeaveId;

    private TestDatabase _database = null!;
    private EmployeeService _employees = null!;
    private AttendanceService _attendance = null!;
    private WagePaymentService _wages = null!;

    /// <summary>Four finished weeks, oldest first, each starting on a Sunday.</summary>
    private DateOnly _week1, _week2, _week3, _week4;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _employees = new EmployeeService(_database.Db);
        _attendance = new AttendanceService(_database.Db);
        _wages = new WagePaymentService(_database.Db, new WageCalculator(_database.Db));

        var thisWeek = WageWeek.StartOf(IndiaTime.Today());
        _week1 = thisWeek.AddDays(-28);
        _week2 = thisWeek.AddDays(-21);
        _week3 = thisWeek.AddDays(-14);
        _week4 = thisWeek.AddDays(-7);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task An_employee_can_be_added_edited_viewed_and_deactivated()
    {
        var ravi = await _employees.CreateAsync(
            new SaveEmployeeRequest("Ravi", "Packing", "9847000000", "Kundara", new DateOnly(2025, 6, 1), 700m, new DateOnly(2025, 6, 1)),
            default);

        Assert.Equal(700m, ravi.CurrentDailyWage);
        Assert.True(ravi.IsActive);

        var edited = await _employees.UpdateAsync(
            ravi.Id, new SaveEmployeeRequest("Ravi K", "Driver", "9847000001", "Kollam", new DateOnly(2025, 6, 1)), default);

        Assert.Equal("Ravi K", edited.Name);
        Assert.Equal("Driver", edited.Designation);
        Assert.Equal(700m, edited.CurrentDailyWage);

        await _employees.SetActiveAsync(ravi.Id, false, default);

        Assert.Empty(await _employees.GetAllAsync(null, includeInactive: false, default));
        var viewed = await _employees.GetAsync(ravi.Id, default);
        Assert.False(viewed.IsActive);
        Assert.Single(await _employees.GetAllAsync("Driver", includeInactive: true, default));
    }

    [Fact]
    public async Task An_employee_cannot_be_added_without_a_daily_wage()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _employees.CreateAsync(new SaveEmployeeRequest("Anil", null, null, null, null), default));

        Assert.Contains("daily wage", error.Message);
    }

    [Fact]
    public async Task A_wage_change_is_added_to_the_history_and_earlier_weeks_keep_the_old_rate()
    {
        // ₹650 from long ago, ₹700 from before week 1, ₹750 from week 3.
        var ravi = await CreateAsync("Ravi", 650m, _week1.AddDays(-200));
        await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(700m, _week1.AddDays(-60)), default);
        var history = await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(750m, _week3), default);

        Assert.Equal([750m, 700m, 650m], history.Select(r => r.DailyWage));
        Assert.Single(history, r => r.IsCurrent);
        Assert.Equal(750m, history.Single(r => r.IsCurrent).DailyWage);

        await MarkWeekAsync(ravi, _week1, Present, Present, Present, Present, Present, Present, Absent);
        await MarkWeekAsync(ravi, _week3, Present, Present, Present, Present, Present, Present, Absent);

        var week1 = await WeekOfAsync(ravi, _week1);
        var week3 = await WeekOfAsync(ravi, _week3);

        Assert.Equal([700m], week1.DailyWages);
        Assert.Equal(4200m, week1.Payable);
        Assert.Equal([750m], week3.DailyWages);
        Assert.Equal(4500m, week3.Payable);
    }

    [Fact]
    public async Task Entering_the_same_wage_from_the_same_day_twice_adds_nothing()
    {
        var ravi = await CreateAsync("Ravi", 700m);

        await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(750m, _week3), default);
        var history = await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(750m, _week3), default);

        Assert.Equal(2, history.Count);
        Assert.DoesNotContain(history, r => r.IsSuperseded);

        // A different figure for the same day is a correction: kept, and the newer one wins.
        history = await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(760m, _week3), default);
        Assert.Equal(3, history.Count);
        Assert.Single(history, r => r.IsSuperseded && r.DailyWage == 750m);
        await MarkAsync(ravi, _week3.AddDays(1), Present);
        Assert.Equal(760m, (await WeekOfAsync(ravi, _week3)).Payable);
    }

    [Fact]
    public async Task Each_status_counts_its_default_fraction_of_a_day()
    {
        var ravi = await CreateAsync("Ravi", 700m);

        await MarkWeekAsync(ravi, _week1, Present, HalfDay, Absent, Leave, null, null, null);

        var week = await WeekOfAsync(ravi, _week1);

        Assert.Equal(1.5m, week.DaysWorked);
        Assert.Equal(1050m, week.Payable);
        Assert.Equal([700m, 350m, 0m, 0m, 0m, 0m, 0m], week.Days.Select(d => d.Amount));
        Assert.Equal(
            ["Present:1", "Half day:1", "Absent:1", "Leave:1"],
            week.StatusCounts.Select(c => $"{c.Name}:{c.Days}"));
        Assert.Equal(3, week.NotRecordedDays);
        Assert.Equal("Pending", week.Status);
    }

    [Fact]
    public async Task Saturday_pay_covers_sunday_to_saturday_so_five_and_a_half_days_at_700_is_3850()
    {
        var ravi = await CreateAsync("Ravi", 700m);

        // Sunday off (not his turn), Monday to Friday present, Saturday a half day.
        await MarkWeekAsync(ravi, _week1, null, Present, Present, Present, Present, Present, HalfDay);

        var week = await _wages.GetWeekAsync(_week1.AddDays(3), default);
        var row = week.Employees.Single(e => e.EmployeeId == ravi);

        Assert.Equal(_week1, week.WeekStart);
        Assert.Equal(DayOfWeek.Sunday, week.WeekStart.DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, week.WeekEnd.DayOfWeek);
        Assert.Equal(5.5m, row.DaysWorked);
        Assert.Equal(3850m, row.Payable);
        Assert.True(week.CanPay);
    }

    [Fact]
    public async Task Correcting_attendance_before_the_week_is_paid_changes_the_week()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, null, null, null, null);

        await MarkAsync(ravi, _week1.AddDays(2), HalfDay);

        var week = await WeekOfAsync(ravi, _week1);
        Assert.Equal(1.5m, week.DaysWorked);
        Assert.Equal(1050m, week.Payable);
        Assert.Empty(week.Adjustments);
    }

    [Fact]
    public async Task A_mid_week_raise_pays_each_day_at_its_own_rate()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(750m, _week1.AddDays(4)), default);

        // Monday to Wednesday at 700, Thursday to Saturday at 750.
        await MarkWeekAsync(ravi, _week1, null, Present, Present, Present, Present, Present, Present);

        var week = await WeekOfAsync(ravi, _week1);

        Assert.Equal(3 * 700m + 3 * 750m, week.Payable);
        Assert.Equal([700m, 750m], week.DailyWages);
    }

    [Fact]
    public async Task Paying_records_a_snapshot_and_exactly_one_employee_wages_expense()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, Present, Present, Present, HalfDay);

        var response = await PayAsync(ravi, _week1, 3850m);

        var payment = Assert.Single(response.Payments);
        Assert.Equal(3850m, payment.Amount);
        Assert.Equal(5.5m, payment.DaysWorked);
        Assert.Equal(_week1, payment.PeriodStart);
        Assert.Equal(_week1.AddDays(6), payment.PeriodEnd);
        Assert.Equal(7, payment.Lines.Count(l => l.LineType == WagePaymentLineType.Attendance));
        Assert.NotNull(payment.ExpenseId);

        var expense = await _database.Db.Expenses.SingleAsync();
        Assert.Equal(KnownExpenseCategories.EmployeeWagesId, expense.CategoryId);
        Assert.Equal(3850m, expense.Amount);
        Assert.Equal(_week1.AddDays(6), expense.ExpenseDate);
        Assert.Equal(payment.Id, expense.WagePaymentId);

        var week = await WeekOfAsync(ravi, _week1);
        Assert.Equal("Paid", week.Status);
        Assert.Equal(3850m, week.Payment!.Amount);
    }

    [Fact]
    public async Task A_week_cannot_be_paid_twice()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, null, null, null, null, null);
        await PayAsync(ravi, _week1, 700m);

        var error = await Assert.ThrowsAsync<DomainException>(() => PayAsync(ravi, _week1, 700m));

        Assert.Contains("already paid", error.Message);
        Assert.Equal(1, await _database.Db.WagePayments.CountAsync());
        Assert.Equal(1, await _database.Db.Expenses.CountAsync());
    }

    [Fact]
    public async Task The_database_itself_refuses_a_second_payment_for_the_same_week()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, null, null, null, null, null);
        await PayAsync(ravi, _week1, 700m);

        _database.Db.WagePayments.Add(new WagePayment
        {
            EmployeeId = ravi,
            PeriodStart = _week1,
            PeriodEnd = _week1.AddDays(6),
            PaymentDate = _week1.AddDays(6),
            Method = PaymentMethod.Cash,
            Amount = 700m
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_later_wage_change_never_alters_a_paid_week()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, Present, Present, Present, HalfDay);
        await PayAsync(ravi, _week1, 3850m);

        await _employees.AddWageRateAsync(ravi, new AddWageRateRequest(800m, _week2), default);

        var paid = await WeekOfAsync(ravi, _week1);
        Assert.Equal("Paid", paid.Status);
        Assert.Equal(3850m, paid.Payable);
        Assert.Equal([700m], paid.DailyWages);
        Assert.Equal(3850m, (await _database.Db.WagePayments.SingleAsync()).Amount);

        // And a raise cannot be back-dated into money already handed over.
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _employees.AddWageRateAsync(ravi, new AddWageRateRequest(900m, _week1.AddDays(3)), default));
        Assert.Contains("paid up to", error.Message);
    }

    [Fact]
    public async Task Payment_is_refused_when_the_amount_changed_since_the_screen_loaded()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, null, null, null, null);

        // The screen showed ₹1,400; then someone marked another day.
        await MarkAsync(ravi, _week1.AddDays(3), Present);

        var error = await Assert.ThrowsAsync<DomainException>(() => PayAsync(ravi, _week1, 1400m));

        Assert.Contains("₹2100.00", error.Message);
        Assert.Equal(0, await _database.Db.WagePayments.CountAsync());
    }

    [Fact]
    public async Task A_week_cannot_be_paid_before_its_saturday()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        var thisWeek = WageWeek.StartOf(IndiaTime.Today());

        if (IndiaTime.Today() == thisWeek.AddDays(6))
        {
            return; // Today is Saturday: this week can legitimately be paid.
        }

        await MarkAsync(ravi, thisWeek, Present);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _wages.PayAsync(new PayWagesRequest(thisWeek, IndiaTime.Today(), PaymentMethod.Cash, null, null,
                [new PayEmployeeRequest(ravi, 700m)]), default));

        Assert.Contains("can be paid from then", error.Message);
    }

    [Fact]
    public async Task A_correction_to_a_paid_week_is_carried_to_the_next_week_and_the_paid_week_stays_as_paid()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, Present, Present, Present, Present);
        await PayAsync(ravi, _week1, 4200m);

        // Wednesday of the paid week was really an absence.
        var saved = await _attendance.SaveAsync(
            new SaveAttendanceRequest(_week1.AddDays(3), [new AttendanceEntryRequest(ravi, Absent)]), default);
        Assert.Contains(saved.Warnings, w => w.Contains("already paid"));

        var paid = await WeekOfAsync(ravi, _week1);
        Assert.Equal(4200m, paid.Payable);
        Assert.Equal(-700m, paid.ChangedSincePaidAmount);

        await MarkWeekAsync(ravi, _week2, null, Present, Present, Present, null, null, null);
        var next = await WeekOfAsync(ravi, _week2);

        Assert.Equal(2100m, next.WorkAmount);
        var correction = Assert.Single(next.Adjustments);
        Assert.Equal("Correction", correction.Kind);
        Assert.Equal(-700m, correction.Amount);
        Assert.Equal(1400m, next.Payable);

        var payment = Assert.Single((await PayAsync(ravi, _week2, 1400m)).Payments);
        var line = Assert.Single(payment.Lines, l => l.LineType == WagePaymentLineType.Correction);
        Assert.Equal("Present", line.PreviousStatusName);
        Assert.Equal("Absent", line.StatusName);
        Assert.Equal(-700m, line.Amount);

        // Settled once: the paid week no longer shows it pending, and the next week does not repeat it.
        Assert.Equal(0m, (await WeekOfAsync(ravi, _week1)).ChangedSincePaidAmount);
        await MarkWeekAsync(ravi, _week3, null, Present, null, null, null, null, null);
        Assert.Empty((await WeekOfAsync(ravi, _week3)).Adjustments);

        // The expense is what was actually handed over.
        Assert.Equal(4200m + 1400m, await _database.Db.Expenses.SumAsync(e => e.Amount));
    }

    [Fact]
    public async Task An_underpaid_day_is_added_to_the_next_week()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, HalfDay, null, null, null, null);
        await PayAsync(ravi, _week1, 1050m);

        await MarkAsync(ravi, _week1.AddDays(2), Present);
        await MarkWeekAsync(ravi, _week2, null, Present, null, null, null, null, null);

        var next = await WeekOfAsync(ravi, _week2);
        Assert.Equal(350m, Assert.Single(next.Adjustments).Amount);
        Assert.Equal(1050m, next.Payable);
    }

    [Fact]
    public async Task An_overpayment_larger_than_the_next_weeks_pay_carries_on_to_the_week_after()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, Present, null, null, null);
        await PayAsync(ravi, _week1, 2100m);

        // None of those three days was worked after all: ₹2,100 overpaid.
        await MarkWeekAsync(ravi, _week1, null, Absent, Absent, Absent, null, null, null);
        await MarkWeekAsync(ravi, _week2, null, Present, null, null, null, null, null);

        var week2 = await WeekOfAsync(ravi, _week2);
        Assert.Equal(0m, week2.Payable);
        Assert.Equal(-1400m, week2.CarriedForward);

        var payment = Assert.Single((await PayAsync(ravi, _week2, 0m)).Payments);
        Assert.Equal(0m, payment.Amount);
        Assert.Equal(-1400m, payment.CarriedForward);
        Assert.Null(payment.ExpenseId); // Nothing was handed over, so nothing was spent.

        await MarkWeekAsync(ravi, _week3, null, Present, Present, Present, null, null, null);
        var week3 = await WeekOfAsync(ravi, _week3);
        var carried = Assert.Single(week3.Adjustments);
        Assert.Equal("CarriedBalance", carried.Kind);
        Assert.Equal(-1400m, carried.Amount);
        Assert.Equal(700m, week3.Payable);
    }

    [Fact]
    public async Task Cancelling_a_payment_cancels_its_expense_and_the_week_can_be_paid_again()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, null, null, null, null, null);
        var payment = Assert.Single((await PayAsync(ravi, _week1, 700m)).Payments);

        var cancelled = await _wages.CancelAsync(payment.Id, new CancelWagePaymentRequest("Recorded against the wrong week"), default);

        Assert.Equal(WagePaymentStatus.Cancelled, cancelled.Status);
        Assert.Equal("Recorded against the wrong week", cancelled.CancellationReason);

        var expense = await _database.Db.Expenses.AsNoTracking().SingleAsync();
        Assert.Equal(ExpenseStatus.Cancelled, expense.Status);
        Assert.Single(await _database.Db.ExpenseChanges.Where(c => c.ExpenseId == expense.Id).ToListAsync());

        Assert.Equal("Pending", (await WeekOfAsync(ravi, _week1)).Status);
        await PayAsync(ravi, _week1, 700m);
        Assert.Equal(2, await _database.Db.WagePayments.CountAsync());
    }

    [Fact]
    public async Task A_payment_cannot_be_cancelled_while_a_later_payment_settled_a_correction_to_it()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, null, null, null, null);
        var first = Assert.Single((await PayAsync(ravi, _week1, 1400m)).Payments);

        await MarkAsync(ravi, _week1.AddDays(2), Absent);
        await MarkWeekAsync(ravi, _week2, null, Present, Present, null, null, null, null);
        var second = Assert.Single((await PayAsync(ravi, _week2, 700m)).Payments);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _wages.CancelAsync(first.Id, new CancelWagePaymentRequest("mistake"), default));
        Assert.Contains("Cancel that payment first", error.Message);

        // Cancelling the later one first puts the correction back to pending, and then the first can go.
        await _wages.CancelAsync(second.Id, new CancelWagePaymentRequest("mistake"), default);
        Assert.Equal(-700m, (await WeekOfAsync(ravi, _week2)).AdjustmentAmount);
        await _wages.CancelAsync(first.Id, new CancelWagePaymentRequest("mistake"), default);
    }

    [Fact]
    public async Task Paying_several_employees_is_all_or_nothing()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        var anil = await CreateAsync("Anil", 650m);
        await MarkWeekAsync(ravi, _week1, null, Present, null, null, null, null, null);
        await MarkWeekAsync(anil, _week1, null, Present, Present, null, null, null, null);

        // Anil's figure is wrong, so Ravi is not paid either.
        await Assert.ThrowsAsync<DomainException>(() =>
            _wages.PayAsync(new PayWagesRequest(_week1, _week1.AddDays(6), PaymentMethod.Cash, null, null,
                [new PayEmployeeRequest(ravi, 700m), new PayEmployeeRequest(anil, 999m)]), default));
        Assert.Equal(0, await _database.Db.WagePayments.CountAsync());

        var response = await _wages.PayAsync(new PayWagesRequest(_week1, _week1.AddDays(6), PaymentMethod.Cash, null, null,
            [new PayEmployeeRequest(ravi, 700m), new PayEmployeeRequest(anil, 1300m)]), default);

        Assert.Equal(2000m, response.Total);
        var week = await _wages.GetWeekAsync(_week1, default);
        Assert.Equal(2000m, week.PaidTotal);
        Assert.Equal(2, week.PaidCount);
        Assert.Equal(2, await _database.Db.Expenses.CountAsync(e => e.CategoryId == KnownExpenseCategories.EmployeeWagesId));
    }

    [Fact]
    public async Task Deactivating_an_employee_keeps_attendance_wage_history_and_payments()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, Present, Present, null, null, null, null);
        await PayAsync(ravi, _week1, 1400m);

        await _employees.SetActiveAsync(ravi, false, default);

        var week = await WeekOfAsync(ravi, _week1);
        Assert.Equal("Paid", week.Status);
        Assert.False(week.IsActive);

        var sheet = await _attendance.GetSheetAsync(_week1.AddDays(1), default);
        Assert.Equal(Present, sheet.Rows.Single(r => r.EmployeeId == ravi).StatusId);

        Assert.Single(await _employees.GetWageRatesAsync(ravi, default));
        Assert.Single(await _wages.GetAllAsync(ravi, null, null, default));

        // No new attendance for someone who has left.
        var error = await Assert.ThrowsAsync<DomainException>(() => MarkAsync(ravi, _week2.AddDays(1), Present));
        Assert.Contains("inactive", error.Message);
    }

    [Fact]
    public async Task Changing_what_a_half_day_is_worth_does_not_touch_paid_weeks()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        await MarkWeekAsync(ravi, _week1, null, HalfDay, null, null, null, null, null);
        await PayAsync(ravi, _week1, 350m);
        await MarkWeekAsync(ravi, _week2, null, HalfDay, null, null, null, null, null);

        await _attendance.UpdateStatusAsync(HalfDay, 0.6m, default);

        var paid = await WeekOfAsync(ravi, _week1);
        Assert.Equal(350m, paid.Payable);
        Assert.Equal(0m, paid.ChangedSincePaidAmount);

        var next = await WeekOfAsync(ravi, _week2);
        Assert.Empty(next.Adjustments);
        Assert.Equal(420m, next.Payable);
    }

    [Fact]
    public async Task Attendance_cannot_be_marked_for_a_future_day()
    {
        var ravi = await CreateAsync("Ravi", 700m);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            MarkAsync(ravi, IndiaTime.Today().AddDays(1), Present));

        Assert.Contains("not happened yet", error.Message);
    }

    [Fact]
    public async Task The_register_lists_active_employees_and_saves_many_at_once()
    {
        var ravi = await CreateAsync("Ravi", 700m);
        var anil = await CreateAsync("Anil", 650m);
        var suresh = await CreateAsync("Suresh", 650m);
        var manu = await CreateAsync("Manu", 600m);
        var day = _week1.AddDays(1);

        var response = await _attendance.SaveAsync(new SaveAttendanceRequest(day,
        [
            new AttendanceEntryRequest(ravi, Present),
            new AttendanceEntryRequest(anil, Present),
            new AttendanceEntryRequest(suresh, HalfDay),
            new AttendanceEntryRequest(manu, Absent)
        ]), default);

        Assert.Equal(4, response.Changed);
        Assert.Equal(["Anil", "Manu", "Ravi", "Suresh"], response.Sheet.Rows.Select(r => r.Name));
        Assert.Equal(HalfDay, response.Sheet.Rows.Single(r => r.EmployeeId == suresh).StatusId);

        // Saving the same register again changes nothing.
        var again = await _attendance.SaveAsync(new SaveAttendanceRequest(day,
            [new AttendanceEntryRequest(ravi, Present)]), default);
        Assert.Equal(0, again.Changed);

        // Clearing a day puts it back to "not recorded".
        await MarkAsync(manu, day, null);
        Assert.Null((await _attendance.GetSheetAsync(day, default)).Rows.Single(r => r.EmployeeId == manu).StatusId);
    }

    private async Task<Guid> CreateAsync(string name, decimal dailyWage, DateOnly? from = null)
    {
        var employee = await _employees.CreateAsync(
            new SaveEmployeeRequest(name, null, null, null, null, dailyWage, from ?? _week1.AddDays(-365)), default);

        return employee.Id;
    }

    private Task MarkAsync(Guid employeeId, DateOnly date, Guid? statusId) =>
        _attendance.SaveAsync(new SaveAttendanceRequest(date, [new AttendanceEntryRequest(employeeId, statusId)]), default);

    /// <summary>Sunday to Saturday; null leaves the day unrecorded.</summary>
    private async Task MarkWeekAsync(Guid employeeId, DateOnly weekStart, params Guid?[] days)
    {
        for (var i = 0; i < days.Length; i++)
        {
            if (days[i] is not null)
            {
                await MarkAsync(employeeId, weekStart.AddDays(i), days[i]);
            }
        }
    }

    private async Task<EmployeeWageDto> WeekOfAsync(Guid employeeId, DateOnly weekStart) =>
        (await _wages.GetWeekAsync(weekStart, default)).Employees.Single(e => e.EmployeeId == employeeId);

    private Task<PayWagesResponse> PayAsync(Guid employeeId, DateOnly weekStart, decimal expected) =>
        _wages.PayAsync(
            new PayWagesRequest(weekStart, weekStart.AddDays(6), PaymentMethod.Cash, null, null,
                [new PayEmployeeRequest(employeeId, expected)]),
            default);
}
