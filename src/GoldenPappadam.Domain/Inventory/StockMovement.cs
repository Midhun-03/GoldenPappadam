using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// One change in stock. Never edited or deleted: mistakes are corrected with a new movement.
/// Current stock is the sum of Quantity for a product.
/// </summary>
public class StockMovement : Entity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public StockMovementType MovementType { get; set; }

    /// <summary>Signed: positive adds stock, negative removes it. Never zero.</summary>
    public decimal Quantity { get; set; }

    /// <summary>UTC. When the movement happened in the business, which may differ from CreatedAt.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>The record that caused this movement, if any. See <see cref="StockReferenceType"/>.</summary>
    public StockReferenceType? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    /// <summary>Required by the application for Damage and Adjustment movements.</summary>
    public string? Notes { get; set; }
}
