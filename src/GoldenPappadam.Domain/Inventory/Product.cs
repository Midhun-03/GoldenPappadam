using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// Anything stock is kept for: loose varieties and packed products alike.
/// One table keeps sales lines, stock movements and reports pointing at a single ProductId.
/// </summary>
public class Product : AuditableEntity
{
    public required string ProductCode { get; set; }

    public required string Name { get; set; }

    public Guid CategoryId { get; set; }
    public ProductCategory? Category { get; set; }

    public ProductKind Kind { get; set; }

    /// <summary>The unit this product's own stock is counted in.</summary>
    public Guid UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }

    /// <summary>
    /// Packed products only: the product this one is packed from. Usually a loose product,
    /// occasionally another packed product (a box of 12 packets). Null for loose products.
    /// </summary>
    public Guid? SourceProductId { get; set; }
    public Product? SourceProduct { get; set; }

    /// <summary>
    /// Packed products only: how much source stock one pack consumes, expressed in the
    /// source product's unit - 0.250 when the source is loose kg, 20 when it is loose pieces,
    /// 12 when the source is packets. Null for loose products.
    /// </summary>
    public decimal? SourceQuantityPerPack { get; set; }

    /// <summary>Default selling price. Null means the product is not normally sold as it is.</summary>
    public decimal? SellingPrice { get; set; }

    /// <summary>Null means no low-stock alert for this product.</summary>
    public decimal? LowStockThreshold { get; set; }

    public bool IsActive { get; set; } = true;
}
