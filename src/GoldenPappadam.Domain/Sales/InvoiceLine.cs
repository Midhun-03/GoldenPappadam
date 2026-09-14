using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.Sales;

public class InvoiceLine : Entity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>The product name as printed on this bill, kept even if the product is renamed later.</summary>
    public required string Description { get; set; }

    /// <summary>The unit at the time of sale, for the same reason.</summary>
    public required string UnitCode { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The price actually charged, defaulted from the product but overridable per bill.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
