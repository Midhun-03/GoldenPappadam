using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Accounting;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Accounting.Expenses;

/// <summary>
/// Money spent. Expenses may be edited and cancelled, never deleted, and every edit or cancellation
/// first saves the version it replaces, so nothing that was once recorded is lost.
/// Employee wages expenses are made only by wage payments and are refused here.
/// </summary>
public class ExpenseService(AppDbContext db)
{
    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        string? search,
        bool includeCancelled,
        CancellationToken ct) =>
        await ProjectAsync(
            Filter(from, to, categoryId, search)
                .Where(e => includeCancelled || e.Status == ExpenseStatus.Recorded),
            ct);

    public async Task<ExpenseDto> GetAsync(Guid id, CancellationToken ct) =>
        (await ProjectAsync(db.Expenses.Where(e => e.Id == id), ct)).FirstOrDefault()
        ?? throw new NotFoundException("Expense");

    /// <summary>Totals per category, largest first. Cancelled expenses are not money spent.</summary>
    public async Task<ExpenseSummaryDto> GetSummaryAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        string? search,
        CancellationToken ct)
    {
        var categories = await Filter(from, to, categoryId, search)
            .Where(e => e.Status == ExpenseStatus.Recorded)
            .GroupBy(e => new { e.CategoryId, e.Category!.Name })
            .Select(g => new ExpenseCategoryTotalDto(g.Key.CategoryId, g.Key.Name, g.Sum(e => e.Amount), g.Count()))
            .ToListAsync(ct);

        var ordered = categories.OrderByDescending(c => c.Total).ThenBy(c => c.Name).ToList();

        return new ExpenseSummaryDto(from, to, ordered.Sum(c => c.Total), ordered.Sum(c => c.Count), ordered);
    }

    public async Task<ExpenseDto> CreateAsync(SaveExpenseRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, currentCategoryId: null, ct);

        var expense = new Expense
        {
            CategoryId = request.CategoryId,
            ExpenseDate = request.ExpenseDate,
            Amount = request.Amount,
            Description = Clean(request.Description),
            PaymentMethod = request.PaymentMethod,
            Reference = Clean(request.Reference)
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        return await GetAsync(expense.Id, ct);
    }

    /// <summary>
    /// Replaces an expense's details, keeping the version it replaces. An expense can stay in a
    /// category deactivated since, but cannot be moved into one.
    /// </summary>
    public async Task<ExpenseDto> UpdateAsync(Guid id, SaveExpenseRequest request, CancellationToken ct)
    {
        var expense = await FindEditableAsync(id, ct);
        await ValidateAsync(request, expense.CategoryId, ct);

        var description = Clean(request.Description);
        var reference = Clean(request.Reference);

        var unchanged = expense.CategoryId == request.CategoryId &&
                        expense.ExpenseDate == request.ExpenseDate &&
                        expense.Amount == request.Amount &&
                        expense.Description == description &&
                        expense.PaymentMethod == request.PaymentMethod &&
                        expense.Reference == reference;

        if (!unchanged)
        {
            db.ExpenseChanges.Add(Snapshot(expense, ExpenseChangeType.Edited, Clean(request.Reason)));

            expense.CategoryId = request.CategoryId;
            expense.ExpenseDate = request.ExpenseDate;
            expense.Amount = request.Amount;
            expense.Description = description;
            expense.PaymentMethod = request.PaymentMethod;
            expense.Reference = reference;

            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    /// <summary>For an expense entered by mistake. It stays, marked cancelled, and leaves every total.</summary>
    public async Task<ExpenseDto> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var expense = await FindEditableAsync(id, ct);

        db.ExpenseChanges.Add(Snapshot(expense, ExpenseChangeType.Cancelled, reason.Trim()));
        expense.Status = ExpenseStatus.Cancelled;
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>Every version the expense has had, oldest first, each with the change that produced it.</summary>
    public async Task<IReadOnlyList<ExpenseVersionDto>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.Expenses.AsNoTracking().Include(e => e.Category).FirstOrDefaultAsync(e => e.Id == id, ct)
                      ?? throw new NotFoundException("Expense");

        var changes = await db.ExpenseChanges
            .AsNoTracking()
            .Include(c => c.Category)
            .Where(c => c.ExpenseId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var userIds = changes.Select(c => c.CreatedBy).Append(expense.CreatedBy)
            .Where(u => u != null).Select(u => u!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(Guid? user) => user is { } u ? names.GetValueOrDefault(u) : null;

        // Change i holds the values it replaced, so what things looked like *after* change i is in
        // change i+1 - or, after the last change, in the expense as it stands.
        ExpenseVersionDto Version(string change, int valuesAt, DateTime at, Guid? by, string? reason)
        {
            if (valuesAt < changes.Count)
            {
                var c = changes[valuesAt];
                return new ExpenseVersionDto(change, c.Category!.Name, c.ExpenseDate, c.Amount, c.Description,
                    c.PaymentMethod, c.Reference, at, NameOf(by), reason);
            }

            return new ExpenseVersionDto(change, expense.Category!.Name, expense.ExpenseDate, expense.Amount,
                expense.Description, expense.PaymentMethod, expense.Reference, at, NameOf(by), reason);
        }

        var versions = new List<ExpenseVersionDto> { Version("Recorded", 0, expense.CreatedAt, expense.CreatedBy, null) };

        for (var i = 0; i < changes.Count; i++)
        {
            versions.Add(Version(changes[i].ChangeType.ToString(), i + 1, changes[i].CreatedAt, changes[i].CreatedBy, changes[i].Reason));
        }

        return versions;
    }

    private IQueryable<Expense> Filter(DateOnly? from, DateOnly? to, Guid? categoryId, string? search) =>
        db.Expenses.Where(e =>
            (from == null || e.ExpenseDate >= from) &&
            (to == null || e.ExpenseDate <= to) &&
            (categoryId == null || e.CategoryId == categoryId) &&
            (search == null ||
             e.Description!.Contains(search) ||
             e.Reference!.Contains(search) ||
             e.Category!.Name.Contains(search)));

    private async Task ValidateAsync(SaveExpenseRequest request, Guid? currentCategoryId, CancellationToken ct)
    {
        if (request.Amount <= 0m)
        {
            throw new DomainException("An expense must be more than zero.");
        }

        if (request.ExpenseDate > IndiaTime.Today())
        {
            throw new DomainException("An expense is money already spent, so its date cannot be in the future.");
        }

        if (request.PaymentMethod == PaymentMethod.ReturnCredit)
        {
            throw new DomainException("A return credit is not money spent. Choose how the expense was paid.");
        }

        if (request.CategoryId == KnownExpenseCategories.EmployeeWagesId)
        {
            throw new DomainException(
                "Employee wages are recorded by paying them on the Weekly wages screen, which adds the expense itself. " +
                "Entering them here as well would count them twice.");
        }

        var category = await db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct)
                       ?? throw new NotFoundException("Expense category");

        if (!category.IsActive && category.Id != currentCategoryId)
        {
            throw new DomainException($"The category '{category.Name}' is no longer in use.");
        }
    }

    private async Task<Expense> FindEditableAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct)
                      ?? throw new NotFoundException("Expense");

        if (expense.WagePaymentId is not null)
        {
            throw new DomainException(
                "This expense was made by a wage payment. To undo it, cancel the wage payment on the Weekly wages screen.");
        }

        if (expense.Status == ExpenseStatus.Cancelled)
        {
            throw new DomainException("This expense is cancelled and can no longer be changed.");
        }

        return expense;
    }

    private static ExpenseChange Snapshot(Expense expense, ExpenseChangeType type, string? reason) => new()
    {
        ExpenseId = expense.Id,
        ChangeType = type,
        CategoryId = expense.CategoryId,
        ExpenseDate = expense.ExpenseDate,
        Amount = expense.Amount,
        Description = expense.Description,
        PaymentMethod = expense.PaymentMethod,
        Reference = expense.Reference,
        Reason = reason
    };

    private async Task<IReadOnlyList<ExpenseDto>> ProjectAsync(IQueryable<Expense> query, CancellationToken ct)
    {
        var rows = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                Expense = e,
                CategoryName = e.Category!.Name,
                EmployeeId = e.WagePayment == null ? (Guid?)null : e.WagePayment.EmployeeId,
                VersionCount = db.ExpenseChanges.Count(c => c.ExpenseId == e.Id)
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var userIds = rows
            .SelectMany(r => new[] { r.Expense.CreatedBy, r.Expense.UpdatedBy })
            .Where(u => u != null)
            .Select(u => u!.Value)
            .Distinct()
            .ToList();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? NameOf(Guid? user) => user is { } u ? names.GetValueOrDefault(u) : null;

        return rows
            .Select(r => new ExpenseDto(
                r.Expense.Id,
                r.Expense.CategoryId,
                r.CategoryName,
                r.Expense.ExpenseDate,
                r.Expense.Amount,
                r.Expense.Description,
                r.Expense.PaymentMethod,
                r.Expense.Reference,
                r.Expense.Status,
                r.Expense.WagePaymentId != null,
                r.Expense.WagePaymentId,
                r.EmployeeId,
                r.Expense.CreatedAt,
                NameOf(r.Expense.CreatedBy),
                r.Expense.UpdatedAt,
                NameOf(r.Expense.UpdatedBy),
                r.VersionCount))
            .ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
