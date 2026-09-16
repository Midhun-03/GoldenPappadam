using System.ComponentModel.DataAnnotations;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.FieldSales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.FieldSales.StockRequests;

public record StockRequestLineRequest(
    [Required] Guid ProductId,
    [Range(typeof(decimal), "0.001", "79228162514264337593543950335")] decimal Quantity);

public record CreateStockRequest(
    DateOnly RequiredDate,
    [Required, MinLength(1)] List<StockRequestLineRequest> Lines,
    [MaxLength(300)] string? Notes);

public record StockRequestLineDto(Guid ProductId, string ProductName, string UnitCode, decimal Quantity);

public record StockRequestDto(
    Guid Id,
    DateOnly RequiredDate,
    StockRequestStatus Status,
    string RequestedBy,
    string? DeviceName,
    DateTime CreatedAt,
    string? Notes,
    IReadOnlyList<StockRequestLineDto> Lines);

/// <summary>
/// What the packing unit has to make, added up. One row per product per day, which is the shape
/// somebody standing at the packing table actually needs.
/// </summary>
public record PackingNeedDto(
    DateOnly RequiredDate,
    Guid ProductId,
    string ProductName,
    string UnitCode,
    decimal Quantity,
    int RequestCount);

/// <summary>
/// Requests for stock from the packing unit. No money, no stock movement and no reservation: this
/// only replaces the salesperson telling somebody in person what tomorrow needs to look like.
/// </summary>
public class StockRequestService(AppDbContext db)
{
    public async Task<StockRequestDto> CreateAsync(
        CreateStockRequest request,
        Guid? deviceId,
        CancellationToken ct)
    {
        if (request.Lines.Count == 0)
        {
            throw new DomainException("A request needs at least one product.");
        }

        if (request.Lines.Select(l => l.ProductId).Distinct().Count() != request.Lines.Count)
        {
            throw new DomainException("The same product is listed twice. Combine the quantities into one line.");
        }

        // Everything is checked before a row is written, so a rejected request leaves nothing behind.
        var ids = request.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.IsActive })
            .ToListAsync(ct);

        foreach (var line in request.Lines)
        {
            var product = products.FirstOrDefault(p => p.Id == line.ProductId)
                          ?? throw new NotFoundException("Product");

            if (!product.IsActive)
            {
                throw new DomainException($"Product '{product.Name}' is not active.");
            }

            if (line.Quantity <= 0m)
            {
                throw new DomainException($"The quantity for '{product.Name}' must be greater than zero.");
            }
        }

        var stockRequest = new StockRequest
        {
            DeviceId = deviceId,
            RequiredDate = request.RequiredDate == default ? IndiaTime.Today().AddDays(1) : request.RequiredDate,
            Status = StockRequestStatus.Requested,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Lines = request.Lines
                .Select(l => new StockRequestLine { ProductId = l.ProductId, Quantity = l.Quantity })
                .ToList()
        };

        db.StockRequests.Add(stockRequest);
        await db.SaveChangesAsync(ct);

        return await GetAsync(stockRequest.Id, ct);
    }

    public async Task<StockRequestDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.StockRequests.Where(r => r.Id == id), db).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Stock request");

    public async Task<IReadOnlyList<StockRequestDto>> GetAllAsync(
        DateOnly? from,
        DateOnly? to,
        StockRequestStatus? status,
        CancellationToken ct) =>
        await Project(
                db.StockRequests
                    .Where(r => from == null || r.RequiredDate >= from)
                    .Where(r => to == null || r.RequiredDate <= to)
                    .Where(r => status == null || r.Status == status)
                    .OrderBy(r => r.RequiredDate)
                    .ThenByDescending(r => r.CreatedAt),
                db)
            .ToListAsync(ct);

    /// <summary>
    /// The same requests added up per product per day. Cancelled ones are left out: the packing
    /// unit should not be making something somebody already called off.
    /// </summary>
    public async Task<IReadOnlyList<PackingNeedDto>> GetPackingNeedsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var rows = await db.StockRequestLines
            .Where(l => l.StockRequest!.Status != StockRequestStatus.Cancelled)
            .Where(l => from == null || l.StockRequest!.RequiredDate >= from)
            .Where(l => to == null || l.StockRequest!.RequiredDate <= to)
            .Select(l => new
            {
                l.StockRequest!.RequiredDate,
                l.ProductId,
                ProductName = l.Product!.Name,
                UnitCode = l.Product!.UnitOfMeasure!.Code,
                l.Quantity,
                l.StockRequestId
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.RequiredDate, r.ProductId, r.ProductName, r.UnitCode })
            .Select(g => new PackingNeedDto(
                g.Key.RequiredDate,
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.UnitCode,
                g.Sum(r => r.Quantity),
                g.Select(r => r.StockRequestId).Distinct().Count()))
            .OrderBy(n => n.RequiredDate)
            .ThenBy(n => n.ProductName)
            .ToList();
    }

    /// <summary>The packing unit answering: packed, or not being packed.</summary>
    public async Task<StockRequestDto> SetStatusAsync(Guid id, StockRequestStatus status, CancellationToken ct)
    {
        var request = await db.StockRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new NotFoundException("Stock request");

        if (request.Status == status)
        {
            return await GetAsync(id, ct);
        }

        request.Status = status;
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private static IQueryable<StockRequestDto> Project(IQueryable<StockRequest> requests, AppDbContext db) =>
        requests.Select(r => new StockRequestDto(
            r.Id,
            r.RequiredDate,
            r.Status,
            db.Users.Where(u => u.Id == r.CreatedBy).Select(u => u.FullName).FirstOrDefault() ?? "Office",
            r.Device!.Name,
            r.CreatedAt,
            r.Notes,
            r.Lines
                .Select(l => new StockRequestLineDto(
                    l.ProductId, l.Product!.Name, l.Product!.UnitOfMeasure!.Code, l.Quantity))
                .ToList()));
}
