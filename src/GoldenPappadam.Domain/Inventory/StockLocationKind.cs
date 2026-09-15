namespace GoldenPappadam.Domain.Inventory;

public enum StockLocationKind
{
    /// <summary>The premises. Production and packing happen here, and low stock is measured here.</summary>
    Warehouse = 0,

    /// <summary>A sales vehicle. Loaded in the morning, sold from during the day, emptied in the evening.</summary>
    Van = 1
}
