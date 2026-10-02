namespace GoldenPappadam.Domain.Inventory;

public enum StockLocationKind
{
    /// <summary>The premises. Production and packing happen here, and low stock is measured here.</summary>
    Warehouse = 0,

    /// <summary>A sales vehicle. Loaded in the morning, sold from during the day, emptied in the evening.</summary>
    Van = 1,

    /// <summary>
    /// The business's own retail shop. Holds only pieces products, received from the factory in kg and
    /// sold by the piece.
    /// </summary>
    Shop = 2
}
