namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// Why stock changed. Stored as text, so new values can be added without disturbing existing rows.
/// </summary>
public enum StockMovementType
{
    /// <summary>One-time opening stock when the system starts being used.</summary>
    Opening,

    /// <summary>Loose stock produced. Replaced by the production module later.</summary>
    Production,

    /// <summary>Packing: negative on the source product, positive on the packed product.</summary>
    Packing,

    /// <summary>Stock delivered on an invoice.</summary>
    Sale,

    /// <summary>Stock returned by cancelling an invoice.</summary>
    SaleReversal,

    /// <summary>Damaged or unusable stock written off.</summary>
    Damage,

    /// <summary>Correction after a physical count. Can be positive or negative.</summary>
    Adjustment
}
