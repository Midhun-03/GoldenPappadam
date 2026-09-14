using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>A shop or customer that buys, usually on credit.</summary>
public class Customer : AuditableEntity
{
    public required string Name { get; set; }

    public string? ContactPerson { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    /// <summary>
    /// What this customer already owed when the system started being used.
    /// Zero for customers added later.
    /// </summary>
    public decimal OpeningBalance { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
