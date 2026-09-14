using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>Part of a payment applied to one bill. This is what makes bill-to-bill settlement work.</summary>
public class PaymentAllocation : Entity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public decimal Amount { get; set; }
}
