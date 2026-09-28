namespace GoldenPappadam.Domain.Accounting;

/// <summary>The categories the business started with, seeded with fixed ids like the units.</summary>
public static class KnownExpenseCategories
{
    /// <summary>
    /// The system category. Its expenses are made only by wage payments, one per payment, and are
    /// never entered, edited or cancelled by hand - which is what stops wages being counted twice.
    /// </summary>
    public static readonly Guid EmployeeWagesId = Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000001");

    public static readonly IReadOnlyList<(Guid Id, string Name)> Seeded =
    [
        (EmployeeWagesId, "Employee wages"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000002"), "Raw material"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000003"), "Packaging"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000004"), "Fuel"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000005"), "Electricity"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000006"), "Gas"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000007"), "Transportation"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000008"), "Vehicle maintenance"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b000009"), "Rent"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b00000a"), "Repairs"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b00000b"), "Marketing"),
        (Guid.Parse("6d2e3f40-3001-4b7c-8d2e-0b0b0b00000c"), "Other")
    ];
}
