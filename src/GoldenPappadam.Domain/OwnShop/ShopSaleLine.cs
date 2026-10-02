using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.OwnShop;

/// <summary>
/// One variety on a shop sale: whole pieces at a rate per piece. The product's rates at the time are
/// kept beside the rate charged, so a sale below the standard rate stays visible after the rates change.
/// </summary>
public class ShopSaleLine : Entity
{
    public Guid ShopSaleId { get; set; }
    public ShopSale? ShopSale { get; set; }

    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>The product name as it was when sold.</summary>
    public required string Description { get; set; }

    /// <summary>Pieces. Always whole.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The rate charged per piece.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The product's standard rate per piece at the time - the most it could be sold for.</summary>
    public decimal DefaultPrice { get; set; }

    /// <summary>The lowest rate allowed at the time.</summary>
    public decimal MinimumPrice { get; set; }

    /// <summary>Quantity × UnitPrice, to the paisa.</summary>
    public decimal LineTotal { get; set; }
}
