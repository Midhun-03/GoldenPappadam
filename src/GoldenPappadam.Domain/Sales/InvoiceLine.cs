using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.Sales;

/// <summary>One product on a bill, with its tax worked out and frozen. Never edited.</summary>
public class InvoiceLine : Entity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>1, 2, 3... in the order the lines were entered: the serial number printed on the invoice.</summary>
    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>The product name as printed on this bill, kept even if the product is renamed later.</summary>
    public required string Description { get; set; }

    /// <summary>The unit at the time of sale, for the same reason.</summary>
    public required string UnitCode { get; set; }

    public string? HsnCode { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>The price actually charged, defaulted from the product but overridable per bill.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity × UnitPrice, rounded to the paisa. Before discount and, when prices exclude it, before tax.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>This line's share of the bill-level discount.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Null when the supplier had no GSTIN, so no tax treatment applied.</summary>
    public TaxTreatment? TaxTreatment { get; set; }

    /// <summary>Full GST rate in percent. Zero for anything not taxable.</summary>
    public decimal GstRate { get; set; }

    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
}
