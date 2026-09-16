using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.FieldSales;

public class StockRequestLine : Entity
{
    public Guid StockRequestId { get; set; }
    public StockRequest? StockRequest { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>How many packets are wanted. Always positive.</summary>
    public decimal Quantity { get; set; }
}
