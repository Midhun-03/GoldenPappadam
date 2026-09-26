namespace GoldenPappadam.Domain.Sales;

public enum ReturnStatus
{
    Recorded,

    /// <summary>Recorded in error. Any replacement stock went back; the note and its number are kept.</summary>
    Cancelled
}
