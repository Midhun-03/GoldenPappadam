using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Domain.Inventory;

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
    bool IsActive);

/// <summary>
/// Source fields are required for packed products and ignored for loose ones.
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
    decimal? LowStockThreshold);
