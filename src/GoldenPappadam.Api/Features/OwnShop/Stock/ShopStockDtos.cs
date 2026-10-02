namespace GoldenPappadam.Api.Features.OwnShop.Stock;

/// <summary>
/// DefaultRate is the rate per piece and the most a piece sells for; MinimumRate the lowest allowed
/// (the default itself when the product has no minimum). Rate is where a sale starts.
/// </summary>
public record ShopStockDto(
    Guid ProductId,
    string ProductCode,
    string Name,
    Guid SourceProductId,
    string SourceProductName,
    decimal? PiecesPerKg,
    decimal ShopPieces,
    decimal FactoryKg,
    decimal? DefaultRate,
    decimal? MinimumRate,
    decimal? Rate,
    decimal? LowStockThreshold,
    bool IsLowStock,
    bool IsActive);
