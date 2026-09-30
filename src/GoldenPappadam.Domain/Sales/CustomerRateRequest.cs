using GoldenPappadam.Domain.Common;
using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// A salesperson asking for a customer's rate to change (CLAUDE.md §4 "Rate-change approval",
/// 2026-09-30). A salesperson sets rates only when onboarding a new customer; after that the rate
/// changes only when an admin approves a request like this one.
///
/// Never deleted, and after it is made only the decision may change - <c>AppDbContext</c> refuses
/// anything else. <see cref="Entity.CreatedBy"/> is the salesperson who asked and
/// <see cref="Entity.CreatedAt"/> when the office received it.
/// </summary>
public class CustomerRateRequest : AuditableEntity
{
    /// <summary>What may change once the request exists: the decision, and nothing about the request.</summary>
    public static readonly IReadOnlySet<string> DecisionProperties = new HashSet<string>
    {
        nameof(Status), nameof(DecidedBy), nameof(DecidedAt), nameof(DecisionNote), nameof(ReplacedById),
        nameof(UpdatedAt), nameof(UpdatedBy)
    };

    // The id is made on the phone, like a customer's, so the phone can withdraw the request later.

    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>The customer's agreed rate when the office received the request; null = the standard price.</summary>
    public decimal? PriceWhenRequested { get; set; }

    public decimal RequestedPrice { get; set; }

    /// <summary>When the salesperson asked, UTC - the phone's time, which may be hours before it synced.</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>The salesperson's reason, optional.</summary>
    public string? Reason { get; set; }

    public RateRequestStatus Status { get; set; } = RateRequestStatus.Pending;

    /// <summary>The admin who approved or rejected it, or the salesperson who withdrew it.</summary>
    public Guid? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    /// <summary>The office's note to the salesperson, optional.</summary>
    public string? DecisionNote { get; set; }

    /// <summary>Set when a newer request for the same customer and product replaced this pending one.</summary>
    public Guid? ReplacedById { get; set; }
}
