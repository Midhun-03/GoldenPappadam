using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Products;

/// <summary>
/// Product rules that the database cannot express on its own: unique codes, references that
/// must exist and be active, and packing chains that must not loop.
/// </summary>
public class ProductService(AppDbContext db)
{
    /// <summary>A pack made from a pack made from a pack... in practice one or two levels.</summary>
    private const int MaxSourceChainDepth = 10;

    public async Task<Product> CreateAsync(SaveProductRequest request, CancellationToken ct)
    {
        await EnsureCodeIsFree(request.ProductCode, null, ct);
        await EnsureCategoryAndUnitExist(request.CategoryId, request.UnitOfMeasureId, ct);

        // A brand new product cannot be part of a cycle, because nothing can point at it yet.
        var (sourceProductId, sourceQuantityPerPack) = await ResolveSourceAsync(Guid.Empty, request, ct);

        var product = new Product
        {
            ProductCode = request.ProductCode.Trim(),
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            Kind = request.Kind,
            UnitOfMeasureId = request.UnitOfMeasureId,
            SellingPrice = request.SellingPrice,
            LowStockThreshold = request.LowStockThreshold,
            SourceProductId = sourceProductId,
            SourceQuantityPerPack = sourceQuantityPerPack
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return product;
    }

    public async Task<Product> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Product");

        if (product.Kind != request.Kind)
        {
            throw new DomainException(
                "A product cannot change between loose and packed. Create a new product instead.");
        }

        await EnsureCodeIsFree(request.ProductCode, id, ct);
        await EnsureCategoryAndUnitExist(request.CategoryId, request.UnitOfMeasureId, ct);

        if (product.UnitOfMeasureId != request.UnitOfMeasureId &&
            await db.StockMovements.AnyAsync(m => m.ProductId == id, ct))
        {
            throw new DomainException(
                "The unit cannot be changed once stock has moved, because past quantities were recorded in the old unit.");
        }

        // Everything is validated before the entity is touched, so a rejected request
        // never leaves a half-changed product behind in the change tracker.
        var (sourceProductId, sourceQuantityPerPack) = await ResolveSourceAsync(product.Id, request, ct);
        await EnsureNoCycleAsync(product.Id, sourceProductId, ct);

        product.ProductCode = request.ProductCode.Trim();
        product.Name = request.Name.Trim();
        product.CategoryId = request.CategoryId;
        product.UnitOfMeasureId = request.UnitOfMeasureId;
        product.SellingPrice = request.SellingPrice;
        product.LowStockThreshold = request.LowStockThreshold;
        product.SourceProductId = sourceProductId;
        product.SourceQuantityPerPack = sourceQuantityPerPack;

        await db.SaveChangesAsync(ct);

        return product;
    }

    public async Task<Product> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Product");

        if (!isActive && await db.Products.AnyAsync(p => p.SourceProductId == id && p.IsActive, ct))
        {
            throw new DomainException("Active packed products are still packed from this product.");
        }

        product.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return product;
    }

    /// <summary>Works out the source fields for a request without touching the entity.</summary>
    private async Task<(Guid? SourceProductId, decimal? SourceQuantityPerPack)> ResolveSourceAsync(
        Guid productId,
        SaveProductRequest request,
        CancellationToken ct)
    {
        if (request.Kind != ProductKind.Packed)
        {
            return (null, null);
        }

        if (request.SourceProductId is null || request.SourceQuantityPerPack is null or <= 0)
        {
            throw new DomainException(
                "A packed product needs a source product and a source quantity per pack greater than zero.");
        }

        if (request.SourceProductId == productId)
        {
            throw new DomainException("A product cannot be packed from itself.");
        }

        var source = await db.Products
                         .AsNoTracking()
                         .FirstOrDefaultAsync(p => p.Id == request.SourceProductId, ct)
                     ?? throw new NotFoundException("Source product");

        if (!source.IsActive)
        {
            throw new DomainException($"The source product '{source.Name}' is not active.");
        }

        return (source.Id, request.SourceQuantityPerPack);
    }

    /// <summary>Walks up the packing chain so a product can never end up packed from itself.</summary>
    private async Task EnsureNoCycleAsync(Guid productId, Guid? sourceProductId, CancellationToken ct)
    {
        var currentId = sourceProductId;

        for (var depth = 0; currentId is not null; depth++)
        {
            if (currentId == productId)
            {
                throw new DomainException("This would make the product packed from itself through its source chain.");
            }

            if (depth >= MaxSourceChainDepth)
            {
                throw new DomainException("The packing chain is too long.");
            }

            var next = currentId;
            currentId = await db.Products
                .Where(p => p.Id == next)
                .Select(p => p.SourceProductId)
                .FirstOrDefaultAsync(ct);
        }
    }

    private async Task EnsureCodeIsFree(string productCode, Guid? exceptId, CancellationToken ct)
    {
        var code = productCode.Trim();
        var taken = await db.Products.AnyAsync(p => p.ProductCode == code && (exceptId == null || p.Id != exceptId), ct);

        if (taken)
        {
            throw new DomainException($"A product with code '{code}' already exists.");
        }
    }

    private async Task EnsureCategoryAndUnitExist(Guid categoryId, Guid unitId, CancellationToken ct)
    {
        if (!await db.ProductCategories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
        {
            throw new DomainException("The category does not exist or is not active.");
        }

        if (!await db.UnitOfMeasures.AnyAsync(u => u.Id == unitId && u.IsActive, ct))
        {
            throw new DomainException("The unit does not exist or is not active.");
        }
    }
}
