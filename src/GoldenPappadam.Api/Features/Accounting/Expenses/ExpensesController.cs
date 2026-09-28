using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Accounting.Expenses;

/// <summary>Admin-only, like every endpoint without an attribute (the fallback policy).</summary>
[ApiController]
[Route("api/accounting/expenses")]
public class ExpensesController(ExpenseService expenses) : ControllerBase
{
    /// <summary>Newest first. Dates are IST business days, both ends included.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ExpenseDto>> GetAll(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? categoryId = null,
        string? search = null,
        bool includeCancelled = false,
        CancellationToken ct = default) =>
        expenses.GetAllAsync(from, to, categoryId, Clean(search), includeCancelled, ct);

    /// <summary>Totals per category for the same filters as the list.</summary>
    [HttpGet("summary")]
    public Task<ExpenseSummaryDto> GetSummary(
        DateOnly? from = null,
        DateOnly? to = null,
        Guid? categoryId = null,
        string? search = null,
        CancellationToken ct = default) =>
        expenses.GetSummaryAsync(from, to, categoryId, Clean(search), ct);

    [HttpGet("{id:guid}")]
    public Task<ExpenseDto> GetById(Guid id, CancellationToken ct) => expenses.GetAsync(id, ct);

    [HttpGet("{id:guid}/history")]
    public Task<IReadOnlyList<ExpenseVersionDto>> GetHistory(Guid id, CancellationToken ct) =>
        expenses.GetHistoryAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(SaveExpenseRequest request, CancellationToken ct)
    {
        var expense = await expenses.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, expense);
    }

    [HttpPut("{id:guid}")]
    public Task<ExpenseDto> Update(Guid id, SaveExpenseRequest request, CancellationToken ct) =>
        expenses.UpdateAsync(id, request, ct);

    /// <summary>Expenses are cancelled, never deleted.</summary>
    [HttpPost("{id:guid}/cancel")]
    public Task<ExpenseDto> Cancel(Guid id, CancelExpenseRequest request, CancellationToken ct) =>
        expenses.CancelAsync(id, request.Reason, ct);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
