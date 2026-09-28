using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Domain.Accounting;

/// <summary>
/// The version of an expense that an edit or a cancellation replaced. Immutable: CreatedAt and
/// CreatedBy are when and by whom it was changed. Reading these in order, followed by the expense
/// itself, gives every version it has ever had.
/// </summary>
public class ExpenseChange : Entity
{
    public Guid ExpenseId { get; set; }
    public Expense? Expense { get; set; }

    public ExpenseChangeType ChangeType { get; set; }

    public Guid CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    public string? Reference { get; set; }

    /// <summary>Why it was changed or cancelled.</summary>
    public string? Reason { get; set; }
}
