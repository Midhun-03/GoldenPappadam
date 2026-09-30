using GoldenPappadam.Api.Common;

namespace GoldenPappadam.Api.Features.Inventory.Products;

/// <summary>
/// How much of its source a pack holds - the one place this is worked out, for packing, repacking and
/// the product screen alike (CLAUDE.md §4 "Packing conversion"). No pieces-per-kg figure appears in
/// code: it is data on each loose variety.
///
/// A count-based packet from loose kg holds pieces: 20 pieces of a 200/kg pappadam is 0.1 kg. Any
/// other pack holds a quantity of its source: 0.250 kg for a 250 g packet, 12 packets for a box.
/// </summary>
public static class PackConversion
{
    /// <summary>Stock quantities are kept to three places: a gram, or a thousandth of a packet.</summary>
    public static decimal Round(decimal quantity) => decimal.Round(quantity, 3, MidpointRounding.AwayFromZero);

    /// <summary>Source stock one pack holds, in the source's unit, unrounded (20 ÷ 170 = 0.117647...).</summary>
    public static decimal SourcePerPack(Pack pack) =>
        pack.PiecesPerPack is { } pieces
            ? pieces / PiecesPerKgOf(pack)
            : pack.SourceQuantityPerPack
              ?? throw new DomainException($"'{pack.Name}' does not say how much one pack holds.");

    /// <summary>
    /// Source stock <paramref name="packs"/> packs use, rounded to the gram. Worked out from the pieces
    /// rather than from the rounded per-pack figure, so 250 × 20 ÷ 200 is exactly 25 kg.
    /// </summary>
    public static decimal SourceFor(Pack pack, decimal packs) =>
        Round(pack.PiecesPerPack is { } pieces
            ? packs * pieces / PiecesPerKgOf(pack)
            : packs * SourcePerPack(pack));

    /// <summary>
    /// The loose variety's pieces per kg - required for a count-based packet, which is otherwise
    /// impossible to convert. Asking beats guessing 200.
    /// </summary>
    public static decimal PiecesPerKgOf(Pack pack) =>
        pack.SourcePiecesPerKg is { } perKg && perKg > 0m
            ? perKg
            : throw new DomainException(
                $"'{pack.SourceName}' has no pieces per kg, so '{pack.Name}' ({pack.PiecesPerPack} pieces) cannot be " +
                "worked out in kg. Set pieces per kg on the loose product first.");

    /// <summary>What a conversion needs to know about a packed product and its source.</summary>
    public sealed record Pack(
        string Name,
        int? PiecesPerPack,
        decimal? SourceQuantityPerPack,
        string SourceName,
        decimal? SourcePiecesPerKg);
}
