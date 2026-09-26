using GoldenPappadam.Api.Features.Reports;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Returns;

/// <summary>Admin-only through the fallback policy: settling a return is the office's decision.</summary>
[ApiController]
[Route("api/sales/returns")]
public class ReturnsController(AppDbContext db, ReturnService returns) : ControllerBase
{
    /// <summary>pendingOnly lists the returns still waiting for the office to decide.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ReturnListItemDto>> GetAll(
        Guid? customerId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool pendingOnly = false,
        CancellationToken ct = default) =>
        await ReturnQueries.ProjectList(db.ReturnNotes
                .Where(r => customerId == null || r.CustomerId == customerId)
                .Where(r => from == null || r.ReturnDate >= from)
                .Where(r => to == null || r.ReturnDate <= to)
                .Where(r => !pendingOnly || (r.Settlement == ReturnSettlement.Pending && r.Status == ReturnStatus.Recorded))
                .OrderByDescending(r => r.ReturnDate)
                .ThenByDescending(r => r.CreatedAt))
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<ReturnDetailDto> GetById(Guid id, CancellationToken ct) => returns.GetAsync(id, ct);

    /// <summary>The return note to print or hand to the shop, in the reports' A4 style.</summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var note = await returns.GetAsync(id, ct);
        var settings = await db.InvoiceSettings.AsNoTracking().SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);

        Response.Headers.CacheControl = "private, no-store";

        return File(
            ReportPdfRenderer.Render(ReturnNoteDocument.Build(note), new ReportPdfRenderer.Business(settings.LegalName, settings.Address)),
            "application/pdf",
            $"{note.ReturnNumber.Replace('/', '-')}.pdf");
    }

    [HttpPost]
    public Task<ReturnResponse> Create(CreateReturnRequest request, CancellationToken ct) => returns.CreateAsync(request, ct);

    [HttpPost("{id:guid}/settle")]
    public Task<ReturnResponse> Settle(Guid id, SettleReturnRequest request, CancellationToken ct) =>
        returns.SettleAsync(id, request, ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<ReturnDetailDto> Cancel(Guid id, CancelReturnRequest request, CancellationToken ct) =>
        returns.CancelAsync(id, request.Reason, ct);
}
