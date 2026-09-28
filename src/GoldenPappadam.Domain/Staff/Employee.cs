using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Staff;

/// <summary>
/// Someone the business pays a daily wage. Not a login: a salesperson who is also paid wages has an
/// employee record and a user account, and the two are not linked.
/// Deactivated, never deleted, because attendance, wage rates and payments refer to it.
/// </summary>
public class Employee : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>What they do - "Packing", "Driver". Not an Identity role.</summary>
    public string? Designation { get; set; }

    public string? Phone { get; set; }

    public string? Address { get; set; }

    public DateOnly? JoinedOn { get; set; }

    public bool IsActive { get; set; } = true;

    public List<EmployeeWageRate> WageRates { get; set; } = [];
}
