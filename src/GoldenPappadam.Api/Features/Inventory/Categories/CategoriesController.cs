using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Categories;

[ApiController]
[Route("api/inventory/categories")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<CategoryDto>> GetAll(bool includeInactive = false, CancellationToken ct = default) =>
        await db.ProductCategories
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(SaveCategoryRequest request, CancellationToken ct)
    {
        await EnsureNameIsFree(request.Name, null, ct);

        var category = new ProductCategory { Name = request.Name.Trim() };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<CategoryDto> Update(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        var category = await Find(id, ct);
        await EnsureNameIsFree(request.Name, id, ct);

        category.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.IsActive);
    }

    /// <summary>Categories are deactivated, never deleted, because products refer to them.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<CategoryDto> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        var category = await Find(id, ct);
        category.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.IsActive);
    }

    private async Task<ProductCategory> Find(Guid id, CancellationToken ct) =>
        await db.ProductCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Category");

    private async Task EnsureNameIsFree(string name, Guid? exceptId, CancellationToken ct)
    {
        var taken = await db.ProductCategories
            .AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId), ct);

        if (taken)
        {
            throw new DomainException($"A category named '{name}' already exists.");
        }
    }
}
