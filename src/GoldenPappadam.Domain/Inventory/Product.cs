using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;

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
    /// Packed products only: how much source stock one pack consumes, expressed in the source
    /// product's unit - 0.250 for a 250 g packet of loose kg, 12 for a box of packets, 20 when the
    /// loose is counted in pieces. Null for a count-based packet, which uses <see cref="PiecesPerPack"/>
    /// instead; exactly one of the two is set on a packed product.
    /// </summary>
    public decimal? SourceQuantityPerPack { get; set; }

    /// <summary>
    /// A count-based packet packed from loose pappadam counted in kg: how many pieces it holds (20, 6).
    /// The loose it uses is worked out with the source's <see cref="PiecesPerKg"/>.
    /// </summary>
    public int? PiecesPerPack { get; set; }

    /// <summary>
    /// A loose variety counted in kg: its average pieces per kg - 200 for the standard 4-inch
    /// pappadam, fewer for a larger one. An average, not a measurement; a packet is converted with the
    /// figure of the loose it is packed from.
    /// </summary>
    public decimal? PiecesPerKg { get; set; }

    /// <summary>Default selling price. Null means the product is not normally sold as it is.</summary>
    public decimal? SellingPrice { get; set; }

    /// <summary>Null means no low-stock alert for this product.</summary>
    public decimal? LowStockThreshold { get; set; }

    /// <summary>
    /// How many days the product stays good, counted from packing (for loose stock, from when it was
    /// made). Pappadam is 20. After that it is expired and must be written off, never sold. Null means
    /// the product does not expire, and it is left out of stock-age reporting.
    /// </summary>
    public int? ShelfLifeDays { get; set; }

    /// <summary>HSN code printed on GST invoices, 4 to 8 digits. Confirmed by the accountant.</summary>
    public string? HsnCode { get; set; }

    /// <summary>
    /// Null until someone decides. Harmless while the business has no GSTIN on file; once it has
    /// one, a product with no treatment cannot be billed, because guessing a tax is worse than asking.
    /// </summary>
    public TaxTreatment? TaxTreatment { get; set; }

    /// <summary>The full GST rate in percent (5 means CGST 2.5 + SGST 2.5, or IGST 5). Taxable products only.</summary>
    public decimal? GstRate { get; set; }

    public bool IsActive { get; set; } = true;
}
