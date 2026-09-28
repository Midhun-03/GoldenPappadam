using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Accounting.Expenses;

/// <summary>
/// Expense categories are data: the office adds new ones without any code changing. Employee wages is
/// the one fixed category, because wage payments depend on it.
/// </summary>
[ApiController]
[Route("api/accounting/expense-categories")]
public class ExpenseCategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ExpenseCategoryDto>> GetAll(bool includeInactive = false, CancellationToken ct = default) =>
        await db.ExpenseCategories
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExpenseCategoryDto(c.Id, c.Name, c.IsActive, c.Id == KnownExpenseCategories.EmployeeWagesId))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<ExpenseCategoryDto>> Create(SaveExpenseCategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        await EnsureNameIsFree(name, null, ct);

        var category = new ExpenseCategory { Name = name };
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), ToDto(category));
    }

    /// <summary>A rename relabels the category's past expenses too, since they point at it by id.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ExpenseCategoryDto> Rename(Guid id, SaveExpenseCategoryRequest request, CancellationToken ct)
    {
        var category = await FindChangeable(id, ct);
        var name = request.Name.Trim();
        await EnsureNameIsFree(name, id, ct);

        category.Name = name;
        await db.SaveChangesAsync(ct);

        return ToDto(category);
    }

    /// <summary>Deactivated, never deleted: past expenses keep their category.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<ExpenseCategoryDto> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        var category = await FindChangeable(id, ct);
        category.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return ToDto(category);
    }

    private static ExpenseCategoryDto ToDto(ExpenseCategory c) =>
        new(c.Id, c.Name, c.IsActive, c.Id == KnownExpenseCategories.EmployeeWagesId);

    private async Task<ExpenseCategory> FindChangeable(Guid id, CancellationToken ct)
    {
        if (id == KnownExpenseCategories.EmployeeWagesId)
        {
            throw new DomainException("Employee wages is used by wage payments and cannot be renamed or deactivated.");
        }

        return await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
               ?? throw new NotFoundException("Expense category");
    }

    private async Task EnsureNameIsFree(string name, Guid? exceptId, CancellationToken ct)
    {
        if (await db.ExpenseCategories.AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId), ct))
        {
            throw new DomainException($"An expense category named '{name}' already exists.");
        }
    }
}
