using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;

namespace GoldenPappadam.Api.Features.Inventory.Products;

public record ProductDto(
    Guid Id,
    string ProductCode,
    string Name,
    Guid CategoryId,
    string CategoryName,
    ProductKind Kind,
    Guid UnitOfMeasureId,
    string UnitCode,
    Guid? SourceProductId,
    string? SourceProductName,
    decimal? SourceQuantityPerPack,
    decimal? SellingPrice,
    decimal? LowStockThreshold,
    bool IsActive,
    string? HsnCode,
    TaxTreatment? TaxTreatment,
    decimal? GstRate,
    int? ShelfLifeDays,
    int? PiecesPerPack = null,
    decimal? PiecesPerKg = null,
    decimal? SourcePerPack = null);

/// <summary>
/// A packed product needs its source and exactly one of SourceQuantityPerPack (a quantity of the source:
/// 0.250 kg, 12 packets) or PiecesPerPack (a count-based packet from loose kg); both are ignored for a
/// loose product. PiecesPerKg belongs to a loose product counted in kg. GstRate is required for a
/// taxable product and ignored for any other treatment.
///
/// SourcePerPack on the DTO is what one pack uses of its source, worked out: 0.100 kg for 20 pieces
/// at 200 per kg.
/// </summary>
public record SaveProductRequest(
    [Required, MaxLength(30)] string ProductCode,
    [Required, MaxLength(150)] string Name,
    Guid CategoryId,
    ProductKind Kind,
    Guid UnitOfMeasureId,
    Guid? SourceProductId,
    decimal? SourceQuantityPerPack,
    decimal? SellingPrice,
    decimal? LowStockThreshold,
    [MaxLength(8)] string? HsnCode = null,
    TaxTreatment? TaxTreatment = null,
    decimal? GstRate = null,
    [Range(1, 3650)] int? ShelfLifeDays = null,
    [Range(1, 100000)] int? PiecesPerPack = null,
    [Range(typeof(decimal), "0.001", "1000000")] decimal? PiecesPerKg = null);
