namespace GoldenPappadam.Domain.Sales;

/// <summary>Where a salesperson's request to change a customer's rate stands.</summary>
public enum RateRequestStatus
{
    /// <summary>With the office. The customer's rate has not changed.</summary>
    Pending,

    /// <summary>An admin agreed: the rate changed, from the next bill.</summary>
    Approved,

    /// <summary>An admin said no: the rate stays as it was.</summary>
    Rejected,

    /// <summary>Withdrawn by the salesperson, or replaced by a newer request for the same product.</summary>
    Cancelled
}
