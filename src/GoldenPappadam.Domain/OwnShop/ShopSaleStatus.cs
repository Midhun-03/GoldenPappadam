namespace GoldenPappadam.Domain.OwnShop;

public enum ShopSaleStatus
{
    /// <summary>Sold and paid for at the counter.</summary>
    Completed,

    /// <summary>Recorded in error. The pieces went back on the shelf; the sale and its number are kept.</summary>
    Cancelled
}
