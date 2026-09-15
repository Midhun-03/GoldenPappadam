using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Domain.FieldSales;

/// <summary>
/// A stop on the route. The point of recording the ones that sold nothing is that "visited, took
/// nothing" is a fact the business has never been able to see.
///
/// It points at the bill and the payment rather than the other way round, so the sales module
/// stays independent of the road.
/// </summary>
public class ShopVisit : Entity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>UTC, as recorded on the phone - which may be hours before the server heard about it.</summary>
    public DateTime VisitedAt { get; set; }

    public VisitOutcome Outcome { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public string? Notes { get; set; }
}
