using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Products;

[ApiController]
[Route("api/inventory/products")]
public class ProductsController(AppDbContext db, ProductService products) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ProductDto>> GetAll(
        Guid? categoryId = null,
        ProductKind? kind = null,
        string? search = null,
        bool includeInactive = false,
        CancellationToken ct = default) =>
        await ProductQueries.Project(db.Products
                .Where(p => includeInactive || p.IsActive)
                .Where(p => categoryId == null || p.CategoryId == categoryId)
                .Where(p => kind == null || p.Kind == kind)
                .Where(p => search == null || p.Name.Contains(search) || p.ProductCode.Contains(search))
                .OrderBy(p => p.Name))
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<ProductDto> GetById(Guid id, CancellationToken ct) =>
        await ProductQueries.Project(db.Products.Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Product");

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest request, CancellationToken ct)
    {
        var product = await products.CreateAsync(request, ct);
        var dto = await GetById(product.Id, ct);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ProductDto> Update(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        await products.UpdateAsync(id, request, ct);

        return await GetById(id, ct);
    }

    /// <summary>Products are deactivated, never deleted, because invoices and stock history refer to them.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<ProductDto> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        await products.SetActiveAsync(id, isActive, ct);

        return await GetById(id, ct);
    }
}
