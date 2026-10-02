namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// The units seeded with fixed ids, so code can name the one it means without matching on text.
/// </summary>
public static class KnownUnits
{
    /// <summary>
    /// Loose pappadam is counted in kilograms; a variety's pieces per kg is what turns a count-based
    /// packet (20 pieces) into the loose stock it uses.
    /// </summary>
    public static readonly Guid KilogramId = Guid.Parse("2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000001");

    /// <summary>What the own shop counts and sells pappadam in.</summary>
    public static readonly Guid PieceId = Guid.Parse("2a9f3f9e-0f01-4a1e-9f7a-0b1a0a000002");
}
