using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>A shop or customer that buys, usually on credit.</summary>
public class Customer : AuditableEntity
{
    public required string Name { get; set; }

    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    /// <summary>Where invoices are emailed.</summary>
    public string? Email { get; set; }

    /// <summary>Set for a GST-registered shop; printed on its invoices.</summary>
    public string? Gstin { get; set; }

    /// <summary>
    /// GST state code ("32" for Kerala). For a single-location customer this is the place of
    /// supply, which decides between CGST + SGST and IGST.
    /// </summary>
    public string? StateCode { get; set; }

    /// <summary>
    /// What this customer already owed when the system started being used.
    /// Zero for customers added later.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True for a parent company with several physical shops (see <see cref="CustomerBranch"/>).
    /// A bill for such a customer must name the branch; a plain shop never shows the branch picker.
    /// </summary>
    public bool HasMultipleBranches { get; set; }

    public List<CustomerBranch> Branches { get; set; } = [];
}
