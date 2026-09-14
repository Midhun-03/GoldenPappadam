using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// Money received from a customer. Never edited: a mistake is corrected by recording
/// the opposite entry, so the ledger stays honest.
/// </summary>
public class Payment : Entity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>The business date in IST.</summary>
    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>UPI reference, cheque number, and so on.</summary>
    public string? Reference { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Which bills this payment settled. May cover less than Amount: the rest is money on
    /// account, which still reduces what the customer owes.
    /// </summary>
    public List<PaymentAllocation> Allocations { get; set; } = [];
}
