using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// One physical shop belonging to a multi-branch customer - "Danya Supermarket - Kundara" under
/// the parent customer "Danya Supermarket". Pricing is never duplicated here: a bill for any
/// branch still prices through <see cref="CustomerPrice"/> keyed on the parent <see cref="CustomerId"/>.
/// </summary>
public class CustomerBranch : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public required string Name { get; set; }

    public string? Location { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? ContactPerson { get; set; }

    /// <summary>A branch in another state is registered separately, so it has its own GSTIN.</summary>
    public string? Gstin { get; set; }

    /// <summary>
    /// GST state code. A branch bill's place of supply is the branch, never the parent company:
    /// Danya's Coimbatore shop is in Tamil Nadu however Kerala the head office is.
    /// </summary>
    public string? StateCode { get; set; }

    /// <summary>
    /// Deactivated rather than deleted: a closed branch's historical bills must keep showing it.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
