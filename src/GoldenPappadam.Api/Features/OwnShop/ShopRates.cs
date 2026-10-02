using GoldenPappadam.Api.Common;

namespace GoldenPappadam.Api.Features.OwnShop;

/// <summary>
/// The own shop's pricing rules in one place (CLAUDE.md §4 "Own shop", owner 2026-09-30). A pieces
/// product's rate per piece is both what the shop charges by default and the most it may charge; the
/// office may sell lower, to caterers and other shops, but never below the product's minimum. With no
/// minimum set the rate cannot go below the standard rate at all. Nothing here knows a figure: ₹1.60
/// and ₹1.30 are data on the product.
/// </summary>
public static class ShopRates
{
    /// <summary>The lowest rate allowed: the product's minimum, or its rate when it has none.</summary>
    public static decimal MinimumOf(decimal defaultRate, decimal? minimumRate) => minimumRate ?? defaultRate;

    public static decimal DefaultOf(string productName, decimal? sellingPrice) =>
        sellingPrice ?? throw new DomainException($"'{productName}' has no rate per piece. Set it on the product first.");

    public static void EnsureAllowed(string productName, decimal rate, decimal? sellingPrice, decimal? minimumRate)
    {
        var defaultRate = DefaultOf(productName, sellingPrice);
        var minimum = MinimumOf(defaultRate, minimumRate);

        if (rate < minimum)
        {
            throw new DomainException(
                $"{rate:0.00} is below the lowest rate allowed for {productName} ({minimum:0.00} a piece).");
        }

        if (rate > defaultRate)
        {
            throw new DomainException(
                $"{rate:0.00} is above the rate for {productName} ({defaultRate:0.00} a piece), which is the most a piece sells for.");
        }
    }

    /// <summary>For the doors a pieces product must not go through: invoices, vans, the phone.</summary>
    public static DomainException OnlyAtTheShop(string productName) =>
        new($"'{productName}' is pappadam by the piece, sold only over the own shop's counter.");
}
