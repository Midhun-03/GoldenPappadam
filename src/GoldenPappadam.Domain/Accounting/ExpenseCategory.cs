using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Accounting;

/// <summary>
/// Raw material, Fuel, Electricity... Master data the office can add, rename and deactivate, never
/// delete. Expenses point at the id, so renaming relabels history and deactivating only hides the
/// category from new entries.
/// </summary>
public class ExpenseCategory : AuditableEntity
{
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;
}
