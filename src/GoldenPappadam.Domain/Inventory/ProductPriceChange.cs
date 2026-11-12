using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// One change to a product's standard price - the price a shop with no agreed rate pays.
/// <see cref="Product.SellingPrice"/> only remembers its latest value, and a bill made on a phone
/// away from the office may have been priced before the change, so the server has to be able to say
/// what the price used to be. This can. The agreed-rate twin is <c>CustomerPriceChange</c>.
///
/// Immutable: <see cref="Entity.CreatedAt"/> and <see cref="Entity.CreatedBy"/> are when and by whom.
/// </summary>
public class ProductPriceChange : Entity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Null for a product's first price, or one that had been left blank before the rule that forbids it.</summary>
    public decimal? PreviousPrice { get; set; }

    public decimal NewPrice { get; set; }
}
