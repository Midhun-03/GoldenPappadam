namespace GoldenPappadam.Domain.Staff;

public enum WagePaymentStatus
{
    Paid,

    /// <summary>Recorded, but the money was never handed over. Kept, never deleted.</summary>
    Cancelled
}
