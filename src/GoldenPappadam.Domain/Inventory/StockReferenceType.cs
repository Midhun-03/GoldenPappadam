namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// Which kind of record caused a stock movement. Paired with StockMovement.ReferenceId.
/// Deliberately not a foreign key: it keeps the inventory module independent of sales,
/// production and every future module. The code is responsible for setting it correctly.
/// </summary>
public enum StockReferenceType
{
    PackingEntry,
    Invoice,
    VanLoad,
    RepackEntry,
    ReturnNote,
    ShopTransfer,
    ShopSale
}
