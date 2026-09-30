using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Api.Features.Inventory.Products;

public static class ProductQueries
{
    /// <summary>
    /// Shapes products into the DTO the client sees. Apply filtering and ordering to the
    /// product query first: SQL Server cannot order by an already-projected record.
    /// </summary>
    public static IQueryable<ProductDto> Project(IQueryable<Product> products) =>
        products.Select(p => new ProductDto(
            p.Id,
            p.ProductCode,
            p.Name,
            p.CategoryId,
            p.Category!.Name,
            p.Kind,
            p.UnitOfMeasureId,
            p.UnitOfMeasure!.Code,
            p.SourceProductId,
            p.SourceProduct != null ? p.SourceProduct.Name : null,
            p.SourceQuantityPerPack,
            p.SellingPrice,
            p.LowStockThreshold,
            p.IsActive,
            p.HsnCode,
            p.TaxTreatment,
            p.GstRate,
            p.ShelfLifeDays,
            p.PiecesPerPack,
            p.PiecesPerKg,
            p.PiecesPerPack != null
                ? p.SourceProduct!.PiecesPerKg != null && p.SourceProduct.PiecesPerKg > 0
                    ? (decimal)p.PiecesPerPack / p.SourceProduct.PiecesPerKg
                    : null
                : p.SourceQuantityPerPack));
}
