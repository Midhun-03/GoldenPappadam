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

    /// <summary>
    /// Deactivated rather than deleted: a closed branch's historical bills must keep showing it.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
