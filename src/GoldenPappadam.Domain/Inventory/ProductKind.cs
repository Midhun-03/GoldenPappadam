namespace GoldenPappadam.Domain.Inventory;

public enum ProductKind
{
    /// <summary>Bulk stock, e.g. loose pappadam counted in kg or pieces.</summary>
    Loose = 0,

    /// <summary>Packed stock produced from another product, e.g. a 20-piece packet or a box of 12 packets.</summary>
    Packed = 1
}
