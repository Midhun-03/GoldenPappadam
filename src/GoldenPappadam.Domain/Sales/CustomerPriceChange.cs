using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// One change to what a customer pays for a product. Salesmen set rates as well as the office, so
/// <see cref="CustomerPrice"/> - which only remembers its latest value and last editor - cannot say
/// what the rate used to be. This can.
///
/// Immutable: <see cref="Entity.CreatedAt"/> and <see cref="Entity.CreatedBy"/> are when and by whom,
/// filled by the same audit hook as every other table.
/// </summary>
public class CustomerPriceChange : Entity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Null when the customer had no agreed rate and paid the standard price.</summary>
    public decimal? PreviousPrice { get; set; }

    /// <summary>Null when the agreed rate was removed and the customer went back to the standard price.</summary>
    public decimal? NewPrice { get; set; }

    /// <summary>
    /// Set when the change is an approved salesperson request: <see cref="Entity.CreatedBy"/> is then the
    /// admin who approved it, and the request names the salesperson who asked.
    /// </summary>
    public Guid? RateRequestId { get; set; }
    public CustomerRateRequest? RateRequest { get; set; }
}
