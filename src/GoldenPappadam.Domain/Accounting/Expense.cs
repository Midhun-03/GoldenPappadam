using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Domain.Staff;

namespace GoldenPappadam.Domain.Accounting;

/// <summary>
/// Money already spent. It never moves stock and never touches a customer's balance, which is why it
/// may be edited where a bill may not - but every edit and cancellation first saves the version it
/// replaces as an <see cref="ExpenseChange"/>.
///
/// A wage expense (<see cref="WagePaymentId"/> set) belongs to its wage payment: the database refuses
/// any change to it except being cancelled along with the payment.
/// </summary>
public class Expense : AuditableEntity
{
    public static readonly IReadOnlySet<string> PropertiesEditableOnWageExpense = new HashSet<string>
    {
        nameof(Status),
        nameof(UpdatedAt),
        nameof(UpdatedBy)
    };

    public Guid CategoryId { get; set; }
    public ExpenseCategory? Category { get; set; }

    /// <summary>The IST date the money was spent. Reports count the expense in the period of this date.</summary>
    public DateOnly ExpenseDate { get; set; }

    public decimal Amount { get; set; }

    public string? Description { get; set; }

    /// <summary>Null where it does not apply. Never a return credit.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Bill number, UPI reference, cheque number.</summary>
    public string? Reference { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Recorded;

    /// <summary>Set only on the expense a wage payment made. One expense per wage payment.</summary>
    public Guid? WagePaymentId { get; set; }
    public WagePayment? WagePayment { get; set; }
}
