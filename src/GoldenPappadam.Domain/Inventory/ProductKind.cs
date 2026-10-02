namespace GoldenPappadam.Domain.Inventory;

public enum ProductKind
{
    /// <summary>Bulk stock, e.g. loose pappadam counted in kg or pieces.</summary>
    Loose = 0,

    /// <summary>Packed stock produced from another product, e.g. a 20-piece packet or a box of 12 packets.</summary>
    Packed = 1,

    /// <summary>
    /// Loose pappadam counted by the piece and sold by the piece at the own shop (CLAUDE.md §4 "Own
    /// shop"). One per loose-kg variety, which it points at through <see cref="Product.SourceProductId"/>:
    /// a transfer to the shop turns that variety's kg into these pieces with its pieces per kg. Only ever
    /// held at the shop, and never packed, loaded on a van or billed on an invoice. The 15/30/50-piece
    /// bundles the shop makes up are not products: they are still these pieces.
    /// </summary>
    Pieces = 2
}
