using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.FieldSales;

public class VanLoadLine : Entity
{
    public Guid VanLoadId { get; set; }
    public VanLoad? VanLoad { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Always positive: which way it moves is the load's direction, not the line's sign.</summary>
    public decimal Quantity { get; set; }
}
