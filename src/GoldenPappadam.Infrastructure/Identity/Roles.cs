namespace GoldenPappadam.Infrastructure.Identity;

/// <summary>
/// The two things a user can be. Phase 1 had no roles at all; a salesperson on the road needs a
/// much smaller surface than the office does, and the API - not the app - is what enforces that.
/// </summary>
public static class Roles
{
    /// <summary>The office. Everything: products, stock, pricing, bills, payments, users.</summary>
    public const string Admin = "Admin";

    /// <summary>
    /// The van. Reads shops, prices and balances; records deliveries, payments and visits; creates
    /// the shops it finds, their branches and their rates. Cannot deactivate anything, cannot set an
    /// opening balance, cannot cancel a bill, cannot touch product master data.
    /// </summary>
    public const string Salesperson = "Salesperson";

    public static readonly string[] All = [Admin, Salesperson];
}
