using GoldenPappadam.Api.Common;
using GoldenPappadam.Api.Features.Accounting.Expenses;
using GoldenPappadam.Api.Features.Staff;
using GoldenPappadam.Api.Features.Staff.Attendance;
using GoldenPappadam.Api.Features.Staff.Employees;
using GoldenPappadam.Api.Features.Staff.Wages;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Domain.Staff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Tests;

/// <summary>
/// Money spent: entering, editing without losing the old version, cancelling, finding and totalling
/// expenses - and wages arriving in them exactly once.
/// </summary>
public class ExpenseTests : IAsyncLifetime
{
    private static readonly Guid Fuel = KnownExpenseCategories.Seeded.Single(c => c.Name == "Fuel").Id;
    private static readonly Guid Electricity = KnownExpenseCategories.Seeded.Single(c => c.Name == "Electricity").Id;
    private static readonly Guid RawMaterial = KnownExpenseCategories.Seeded.Single(c => c.Name == "Raw material").Id;

    private TestDatabase _database = null!;
    private ExpenseService _expenses = null!;
    private DateOnly _today;

    public Task InitializeAsync()
    {
        _database = new TestDatabase();
        _expenses = new ExpenseService(_database.Db);
        _today = IndiaTime.Today();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task The_twelve_starting_categories_are_there()
    {
        var categories = await new ExpenseCategoriesController(_database.Db).GetAll();

        Assert.Equal(12, categories.Count);
        Assert.Single(categories, c => c.IsSystem && c.Name == "Employee wages");
    }

    [Fact]
    public async Task An_expense_is_added_with_who_and_when()
    {
        var user = Guid.NewGuid();
        _database.CurrentUser.UserId = user;

        var expense = await AddAsync(Fuel, 1500m, _today, "Diesel for the van");

        Assert.Equal("Fuel", expense.CategoryName);
        Assert.Equal(1500m, expense.Amount);
        Assert.Equal(ExpenseStatus.Recorded, expense.Status);

        var row = await _database.Db.Expenses.AsNoTracking().SingleAsync();
        Assert.Equal(user, row.CreatedBy);
    }

    [Fact]
    public async Task Editing_keeps_every_earlier_version()
    {
        var expense = await AddAsync(Fuel, 1500m, _today.AddDays(-1), "Diesel");

        await _expenses.UpdateAsync(expense.Id,
            new SaveExpenseRequest(Fuel, _today.AddDays(-1), 1550m, "Diesel, 16 litres", PaymentMethod.UPI, "UPI-1", "Wrong amount"),
            default);
        var edited = await _expenses.UpdateAsync(expense.Id,
            new SaveExpenseRequest(Electricity, _today.AddDays(-1), 1550m, "KSEB bill", PaymentMethod.UPI, "UPI-1"),
            default);

        Assert.Equal("Electricity", edited.CategoryName);
        Assert.Equal(2, edited.VersionCount);

        var history = await _expenses.GetHistoryAsync(expense.Id, default);
        Assert.Equal(["Recorded", "Edited", "Edited"], history.Select(v => v.Change));
        Assert.Equal([1500m, 1550m, 1550m], history.Select(v => v.Amount));
        Assert.Equal(["Fuel", "Fuel", "Electricity"], history.Select(v => v.CategoryName));
        Assert.Equal("Wrong amount", history[1].Reason);
    }

    [Fact]
    public async Task Saving_an_unchanged_expense_adds_no_version()
    {
        var expense = await AddAsync(Fuel, 1500m, _today, "Diesel");

        var saved = await _expenses.UpdateAsync(expense.Id,
            new SaveExpenseRequest(Fuel, _today, 1500m, "Diesel", PaymentMethod.Cash, null), default);

        Assert.Equal(0, saved.VersionCount);
    }

    [Fact]
    public async Task A_cancelled_expense_is_kept_but_leaves_every_total()
    {
        var fuel = await AddAsync(Fuel, 1500m, _today);
        await AddAsync(Fuel, 500m, _today);

        await _expenses.CancelAsync(fuel.Id, "Entered twice", default);

        var summary = await _expenses.GetSummaryAsync(_today, _today, null, null, default);
        Assert.Equal(500m, summary.Total);
        Assert.Single(await _expenses.GetAllAsync(_today, _today, null, null, includeCancelled: false, default));
        Assert.Equal(2, (await _expenses.GetAllAsync(_today, _today, null, null, includeCancelled: true, default)).Count);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            _expenses.UpdateAsync(fuel.Id, new SaveExpenseRequest(Fuel, _today, 1m, null, null, null), default));
        Assert.Contains("cancelled", error.Message);
        Assert.Equal("Cancelled", (await _expenses.GetHistoryAsync(fuel.Id, default)).Last().Change);
    }

    [Fact]
    public async Task Expenses_can_be_filtered_by_category_date_and_text()
    {
        await AddAsync(Fuel, 1500m, _today, "Diesel");
        await AddAsync(Fuel, 400m, _today.AddDays(-40), "Petrol for the scooter");
        await AddAsync(Electricity, 3200m, _today.AddDays(-2), "KSEB bill");

        Assert.Equal(2, (await ListAsync(categoryId: Fuel)).Count);
        Assert.Equal(2, (await ListAsync(from: _today.AddDays(-7), to: _today)).Count);
        Assert.Equal("KSEB bill", Assert.Single(await ListAsync(search: "KSEB")).Description);
        Assert.Single(await ListAsync(from: _today.AddDays(-7), to: _today, categoryId: Fuel));
    }

    [Fact]
    public async Task The_summary_totals_each_category_separately()
    {
        await AddAsync(RawMaterial, 12000m, _today, "Urad dal");
        await AddAsync(RawMaterial, 3000m, _today);
        await AddAsync(Fuel, 1500m, _today);
        await AddAsync(Electricity, 3200m, _today);

        var summary = await _expenses.GetSummaryAsync(_today, _today, null, null, default);

        Assert.Equal(19700m, summary.Total);
        Assert.Equal(4, summary.Count);
        Assert.Equal(["Raw material", "Electricity", "Fuel"], summary.Categories.Select(c => c.Name));
        Assert.Equal(15000m, summary.Categories[0].Total);
        Assert.Equal(2, summary.Categories[0].Count);
    }

    [Fact]
    public async Task A_new_category_can_be_added_and_used_without_any_code_change()
    {
        var controller = new ExpenseCategoriesController(_database.Db);

        var created = await controller.Create(new SaveExpenseCategoryRequest("Water"), default);
        var water = Assert.IsType<ExpenseCategoryDto>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);

        var expense = await AddAsync(water.Id, 250m, _today);
        Assert.Equal("Water", expense.CategoryName);

        // Renaming relabels history; deactivating hides it from new entries but keeps the old ones.
        await controller.Rename(water.Id, new SaveExpenseCategoryRequest("Water supply"), default);
        Assert.Equal("Water supply", (await _expenses.GetAsync(expense.Id, default)).CategoryName);

        await controller.SetActive(water.Id, false, default);
        await Assert.ThrowsAsync<DomainException>(() => AddAsync(water.Id, 100m, _today));
        Assert.Equal(250m, (await _expenses.GetAsync(expense.Id, default)).Amount);

        await Assert.ThrowsAsync<DomainException>(() =>
            controller.Create(new SaveExpenseCategoryRequest("Fuel"), default));
    }

    [Fact]
    public async Task Employee_wages_cannot_be_renamed_deactivated_or_entered_by_hand()
    {
        var controller = new ExpenseCategoriesController(_database.Db);

        await Assert.ThrowsAsync<DomainException>(() =>
            controller.Rename(KnownExpenseCategories.EmployeeWagesId, new SaveExpenseCategoryRequest("Salaries"), default));
        await Assert.ThrowsAsync<DomainException>(() =>
            controller.SetActive(KnownExpenseCategories.EmployeeWagesId, false, default));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            AddAsync(KnownExpenseCategories.EmployeeWagesId, 27450m, _today));
        Assert.Contains("count them twice", error.Message);
    }

    [Fact]
    public async Task An_expense_cannot_be_dated_in_the_future_or_be_zero()
    {
        await Assert.ThrowsAsync<DomainException>(() => AddAsync(Fuel, 100m, _today.AddDays(1)));
        await Assert.ThrowsAsync<DomainException>(() => AddAsync(Fuel, 0m, _today));
    }

    [Fact]
    public async Task Paid_wages_appear_once_under_employee_wages_and_cannot_be_edited_as_an_expense()
    {
        var employees = new EmployeeService(_database.Db);
        var attendance = new AttendanceService(_database.Db);
        var wages = new WagePaymentService(_database.Db, new WageCalculator(_database.Db));
        var week = WageWeek.StartOf(_today).AddDays(-7);

        var ravi = await employees.CreateAsync(new SaveEmployeeRequest("Ravi", null, null, null, null, 700m, week.AddDays(-30)), default);
        var anil = await employees.CreateAsync(new SaveEmployeeRequest("Anil", null, null, null, null, 650m, week.AddDays(-30)), default);

        foreach (var day in Enumerable.Range(1, 6).Select(week.AddDays))
        {
            await attendance.SaveAsync(new SaveAttendanceRequest(day,
            [
                new AttendanceEntryRequest(ravi.Id, KnownAttendanceStatuses.PresentId),
                new AttendanceEntryRequest(anil.Id, KnownAttendanceStatuses.PresentId)
            ]), default);
        }

        await wages.PayAsync(new PayWagesRequest(week, week.AddDays(6), PaymentMethod.Cash, null, null,
            [new PayEmployeeRequest(ravi.Id, 4200m), new PayEmployeeRequest(anil.Id, 3900m)]), default);
        await AddAsync(Fuel, 1500m, week.AddDays(6));

        var summary = await _expenses.GetSummaryAsync(week, week.AddDays(6), null, null, default);
        var wagesTotal = summary.Categories.Single(c => c.CategoryId == KnownExpenseCategories.EmployeeWagesId);
        Assert.Equal(8100m, wagesTotal.Total);
        Assert.Equal(2, wagesTotal.Count);
        Assert.Equal(9600m, summary.Total);

        var wageExpense = (await ListAsync(categoryId: KnownExpenseCategories.EmployeeWagesId))
            .Single(e => e.EmployeeId == ravi.Id);
        Assert.True(wageExpense.IsWageExpense);

        await Assert.ThrowsAsync<DomainException>(() =>
            _expenses.UpdateAsync(wageExpense.Id, new SaveExpenseRequest(Fuel, week, 1m, null, null, null), default));
        await Assert.ThrowsAsync<DomainException>(() => _expenses.CancelAsync(wageExpense.Id, "no", default));

        // Not even code that skips the service can change it.
        var row = await _database.Db.Expenses.SingleAsync(e => e.Id == wageExpense.Id);
        row.Amount = 1m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.Db.SaveChangesAsync());
    }

    private Task<ExpenseDto> AddAsync(Guid categoryId, decimal amount, DateOnly date, string? description = null) =>
        _expenses.CreateAsync(new SaveExpenseRequest(categoryId, date, amount, description, PaymentMethod.Cash, null), default);

    private Task<IReadOnlyList<ExpenseDto>> ListAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? categoryId = null,
        string? search = null) =>
        _expenses.GetAllAsync(from, to, categoryId, search, includeCancelled: false, default);
}
