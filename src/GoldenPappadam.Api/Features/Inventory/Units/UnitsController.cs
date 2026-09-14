using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Units;

[ApiController]
[Route("api/inventory/units")]
public class UnitsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UnitDto>> GetAll(bool includeInactive = false, CancellationToken ct = default) =>
        await db.UnitOfMeasures
            .Where(u => includeInactive || u.IsActive)
            .OrderBy(u => u.Code)
            .Select(u => new UnitDto(u.Id, u.Code, u.Name, u.IsActive))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<UnitDto>> Create(SaveUnitRequest request, CancellationToken ct)
    {
        await EnsureCodeIsFree(request.Code, null, ct);

        var unit = new UnitOfMeasure { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim() };
        db.UnitOfMeasures.Add(unit);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new UnitDto(unit.Id, unit.Code, unit.Name, unit.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<UnitDto> Update(Guid id, SaveUnitRequest request, CancellationToken ct)
    {
        var unit = await Find(id, ct);
        await EnsureCodeIsFree(request.Code, id, ct);

        unit.Code = request.Code.Trim().ToUpperInvariant();
        unit.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);

        return new UnitDto(unit.Id, unit.Code, unit.Name, unit.IsActive);
    }

    [HttpPost("{id:guid}/active")]
    public async Task<UnitDto> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        var unit = await Find(id, ct);

        if (!isActive && await db.Products.AnyAsync(p => p.UnitOfMeasureId == id && p.IsActive, ct))
        {
            throw new DomainException("This unit is still used by active products.");
        }

        unit.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return new UnitDto(unit.Id, unit.Code, unit.Name, unit.IsActive);
    }

    private async Task<UnitOfMeasure> Find(Guid id, CancellationToken ct) =>
        await db.UnitOfMeasures.FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException("Unit");

    private async Task EnsureCodeIsFree(string code, Guid? exceptId, CancellationToken ct)
    {
        var normalised = code.Trim().ToUpperInvariant();
        var taken = await db.UnitOfMeasures
            .AnyAsync(u => u.Code == normalised && (exceptId == null || u.Id != exceptId), ct);

        if (taken)
        {
            throw new DomainException($"A unit with code '{normalised}' already exists.");
        }
    }
}
