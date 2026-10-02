using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Tests;

/// <summary>
/// The own shop's pieces product for a loose variety, the way the office would set it up: counted in
/// pieces, at the rates the owner gave on 2026-09-30 (₹1.60 a piece, no lower than ₹1.30).
/// </summary>
public static class OwnShopTestData
{
    public static async Task<Product> SeedPiecesAsync(
        this TestDatabase database,
        Product loose,
        decimal rate = 1.60m,
        decimal? minimum = 1.30m,
        string code = "SHOP-STD")
    {
        var pieces = new Product
        {
            ProductCode = code,
            Name = $"{loose.Name} (pieces)",
            CategoryId = loose.CategoryId,
            Kind = ProductKind.Pieces,
            UnitOfMeasureId = KnownUnits.PieceId,
            SourceProductId = loose.Id,
            SellingPrice = rate,
            MinimumSellingPrice = minimum
        };
        database.Db.Add(pieces);
        await database.Db.SaveChangesAsync();

        return pieces;
    }
}
