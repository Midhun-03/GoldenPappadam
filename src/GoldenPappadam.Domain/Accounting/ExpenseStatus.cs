namespace GoldenPappadam.Domain.Accounting;

public enum ExpenseStatus
{
    Recorded,

    /// <summary>Entered by mistake. Kept, left out of every total, never deleted.</summary>
    Cancelled
}
