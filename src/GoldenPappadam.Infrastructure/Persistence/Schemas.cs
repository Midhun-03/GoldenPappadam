namespace GoldenPappadam.Infrastructure.Persistence;

/// <summary>One database schema per module, so the schema stays readable as modules are added.</summary>
public static class Schemas
{
    public const string Inventory = "inventory";
    public const string Sales = "sales";

    /// <summary>The van and the road: loads, and later devices, visits and routes.</summary>
    public const string FieldSales = "fieldsales";
    public const string Identity = "identity";
}
