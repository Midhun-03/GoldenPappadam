namespace GoldenPappadam.Domain.FieldSales;

/// <summary>Stored as text, so the packing unit can gain states without a migration.</summary>
public enum StockRequestStatus
{
    /// <summary>Asked for. The packing unit has not answered yet.</summary>
    Requested,

    /// <summary>Packed and ready, or handed over.</summary>
    Fulfilled,

    /// <summary>Not being packed. The salesperson finds out at the next sync.</summary>
    Cancelled
}
