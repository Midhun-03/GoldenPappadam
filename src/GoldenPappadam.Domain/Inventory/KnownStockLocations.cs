namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// Locations that exist from the first day and are seeded with fixed ids, the same way the units
/// are. Fixed ids let a migration backfill history and let code name the warehouse without a lookup.
/// More vans are ordinary rows, added by the office, and nothing here needs to change for them.
/// </summary>
public static class KnownStockLocations
{
    /// <summary>
    /// The premises. Every movement recorded before locations existed happened here, so the
    /// migration backfills history to this id.
    /// </summary>
    public static readonly Guid MainWarehouseId = Guid.Parse("3b0a4a0f-1002-4b2f-8a6b-1c2b1b000001");

    /// <summary>The one van the business runs today.</summary>
    public static readonly Guid FirstVanId = Guid.Parse("3b0a4a0f-1002-4b2f-8a6b-1c2b1b000002");
}
