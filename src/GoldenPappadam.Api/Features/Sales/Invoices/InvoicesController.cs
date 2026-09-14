using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

[ApiController]
[Route("api/sales/invoices")]
public class InvoicesController(AppDbContext db, InvoiceService invoices) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<InvoiceListItemDto>> GetAll(
        Guid? customerId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        InvoiceStatus? status = null,
        bool unpaidOnly = false,
        CancellationToken ct = default)
    {
        var rows = await InvoiceQueries.ProjectList(
                db.Invoices
                    .Where(i => customerId == null || i.CustomerId == customerId)
                    .Where(i => from == null || i.InvoiceDate >= from)
                    .Where(i => to == null || i.InvoiceDate <= to)
                    .Where(i => status == null || i.Status == status)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.InvoiceNumber),
                db)
            .ToListAsync(ct);

        return unpaidOnly ? rows.Where(i => i.Outstanding > 0m).ToList() : rows;
    }

    [HttpGet("{id:guid}")]
    public Task<InvoiceDetailDto> GetById(Guid id, CancellationToken ct) => invoices.GetDetailAsync(id, ct);

    /// <summary>Creates the bill and takes the goods off stock. Short stock warns; it never blocks.</summary>
    [HttpPost]
    public Task<CreateInvoiceResponse> Create(CreateInvoiceRequest request, CancellationToken ct) =>
        invoices.CreateAsync(request, ct);

    /// <summary>Reverses a bill: the stock goes back and nothing is owed. The bill itself is kept.</summary>
    [HttpPost("{id:guid}/cancel")]
    public Task<InvoiceDetailDto> Cancel(Guid id, CancelInvoiceRequest request, CancellationToken ct) =>
        invoices.CancelAsync(id, request.Reason, ct);
}
