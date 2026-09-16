using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.FieldSales.Devices;

public record FieldDeviceDto(
    Guid Id,
    string Name,
    string Platform,
    string Salesperson,
    Guid? LocationId,
    string? VanName,
    DateTime LastSeenAt,
    bool IsActive);

/// <summary>
/// The phones the sales team carries, and which van each one rides in.
///
/// That link is what lets a salesperson record a load at all: the server reads the van off the
/// device rather than trusting anything the phone sends, so until the office says which van, the
/// phone can move no stock. It is one decision, made once per handset, by the office.
/// </summary>
[ApiController]
[Route("api/fieldsales/devices")]
public class DevicesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<FieldDeviceDto>> GetAll(
        bool includeInactive = false,
        CancellationToken ct = default) =>
        await db.Devices
            .Where(d => includeInactive || d.IsActive)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new FieldDeviceDto(
                d.Id,
                d.Name,
                d.Platform,
                db.Users.Where(u => u.Id == d.UserId).Select(u => u.FullName).FirstOrDefault() ?? "Unknown",
                d.LocationId,
                d.Location!.Name,
                d.LastSeenAt,
                d.IsActive))
            .ToListAsync(ct);

    /// <summary>
    /// Says which van this phone rides in. Passing no location unassigns it, which stops that
    /// phone recording loads without touching anything it has already recorded.
    /// </summary>
    [HttpPost("{id:guid}/van")]
    public async Task<FieldDeviceDto> SetVan(Guid id, Guid? locationId, CancellationToken ct)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundException("Device");

        if (locationId is { } vanId)
        {
            var location = await db.StockLocations.FirstOrDefaultAsync(l => l.Id == vanId, ct)
                           ?? throw new NotFoundException("Stock location");

            if (location.Kind != StockLocationKind.Van)
            {
                throw new DomainException($"'{location.Name}' is not a van.");
            }

            if (!location.IsActive)
            {
                throw new DomainException($"'{location.Name}' is not active.");
            }
        }

        device.LocationId = locationId;
        await db.SaveChangesAsync(ct);

        return (await GetAll(includeInactive: true, ct)).First(d => d.Id == id);
    }

    /// <summary>A lost handset. Its history stays; it simply stops being able to send anything.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<FieldDeviceDto> SetActive(Guid id, bool isActive, CancellationToken ct)
    {
        var device = await db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundException("Device");

        device.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return (await GetAll(includeInactive: true, ct)).First(d => d.Id == id);
    }
}
