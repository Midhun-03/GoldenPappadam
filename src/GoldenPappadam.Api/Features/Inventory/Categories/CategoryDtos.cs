using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Inventory.Categories;

public record CategoryDto(Guid Id, string Name, bool IsActive);

public record SaveCategoryRequest([Required, MaxLength(100)] string Name);
