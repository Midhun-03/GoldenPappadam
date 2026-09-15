namespace GoldenPappadam.Domain.FieldSales;

/// <summary>What happened at the shop. Stored as text, so new answers cost no migration.</summary>
public enum VisitOutcome
{
    /// <summary>Goods were delivered and billed.</summary>
    Sold,

    /// <summary>Visited, but the shop did not need anything.</summary>
    NoOrder,

    /// <summary>The shop was shut.</summary>
    Closed,

    /// <summary>Passed by without stopping.</summary>
    Skipped
}
