using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Inventory.Units;

public record UnitDto(Guid Id, string Code, string Name, bool IsActive);

public record SaveUnitRequest(
    [Required, MaxLength(10)] string Code,
    [Required, MaxLength(50)] string Name);
