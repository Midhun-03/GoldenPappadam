using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.Sales;

/// <summary>One product that came back. Never edited.</summary>
public class ReturnNoteLine : Entity
{
    public Guid ReturnNoteId { get; set; }
    public ReturnNote? ReturnNote { get; set; }

    public int LineNumber { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>The product name and unit as they were, like an invoice line.</summary>
    public required string Description { get; set; }
    public required string UnitCode { get; set; }

    public decimal Quantity { get; set; }

    public ReturnReason Reason { get; set; }

    /// <summary>What one packet was worth to this shop: its agreed rate unless the office entered another.</summary>
    public decimal UnitRate { get; set; }

    public decimal Value { get; set; }
}
