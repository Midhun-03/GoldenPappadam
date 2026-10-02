namespace GoldenPappadam.Infrastructure.Persistence;

/// <summary>One database schema per module, so the schema stays readable as modules are added.</summary>
public static class Schemas
{
    public const string Inventory = "inventory";
    public const string Sales = "sales";

    /// <summary>The van and the road: loads, and later devices, visits and routes.</summary>
    public const string FieldSales = "fieldsales";

    /// <summary>Employees, their wage rates, attendance and weekly wage payments.</summary>
    public const string Staff = "staff";

    /// <summary>Money spent: expenses and their categories. Not a ledger or double entry.</summary>
    public const string Accounting = "accounting";

    /// <summary>The business's own retail shop: transfers from the factory and sales by the piece.</summary>
    public const string OwnShop = "ownshop";
    public const string Identity = "identity";
}
