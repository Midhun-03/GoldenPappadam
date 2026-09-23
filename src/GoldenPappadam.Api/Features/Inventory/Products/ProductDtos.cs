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
    decimal? GstRate);

/// <summary>
/// Source fields are required for packed products and ignored for loose ones.
/// GstRate is required for a taxable product and ignored for any other treatment.
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
    decimal? GstRate = null);
