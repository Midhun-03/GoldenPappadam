using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Domain.OwnShop;

/// <summary>
/// A sale over the counter of the own shop, by the piece. Paid in full when it is made (owner,
/// 2026-09-30: catering and wholesale buyers pay at the time of buying too), so it never touches a
/// customer's balance and is not an <see cref="Invoice"/>: no credit, no GST document, and a walk-in
/// customer needs no customer record at all.
///
/// A document like an invoice: numbered from the same locked counter in its own series
/// ("OS/26-27/000001"), never deleted, and only ever changed by being cancelled - see
/// <see cref="PropertiesEditableAfterSale"/>. Audit: CreatedAt/CreatedBy are the sale;
/// UpdatedAt/UpdatedBy the cancellation.
/// </summary>
public class ShopSale : AuditableEntity
{
    public static readonly IReadOnlySet<string> PropertiesEditableAfterSale = new HashSet<string>
    {
        nameof(Status),
        nameof(CancelledAt),
        nameof(CancellationReason),
        nameof(UpdatedAt),
        nameof(UpdatedBy)
    };

    public required string SaleNumber { get; set; }
    public required string SeriesCode { get; set; }
    public required string FinancialYear { get; set; }
    public int SequenceNumber { get; set; }

    /// <summary>The business date in IST.</summary>
    public DateOnly SaleDate { get; set; }

    /// <summary>The shop the pieces were sold from. Named rather than assumed, so a second shop is a row.</summary>
    public Guid LocationId { get; set; }
    public StockLocation? Location { get; set; }

    /// <summary>A known buyer - a caterer, another shop. Null for a walk-in customer.</summary>
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>The customer's name when the sale was made. Null for a walk-in customer.</summary>
    public string? CustomerName { get; set; }

    /// <summary>How it was paid. Never <see cref="Sales.PaymentMethod.ReturnCredit"/>, which is not money.</summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>The sum of the lines: what was paid.</summary>
    public decimal TotalAmount { get; set; }

    public ShopSaleStatus Status { get; set; } = ShopSaleStatus.Completed;

    public string? Notes { get; set; }

    /// <summary>Made by the screen when it opens, so pressing Save twice sells once.</summary>
    public Guid? ClientRequestId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public List<ShopSaleLine> Lines { get; set; } = [];
}
