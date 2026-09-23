using GoldenPappadam.Api.Features.Sales.Invoices.Documents;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices;

/// <summary>
/// Admin-only through the fallback policy: the sales app finalizes through /api/mobile/sync and
/// can reach none of this - not the PDFs, not the email, not cancellation.
/// </summary>
[ApiController]
[Route("api/sales/invoices")]
public class InvoicesController(
    AppDbContext db,
    InvoiceService invoices,
    InvoiceDocumentService documents,
    InvoiceEmailService email) : ControllerBase
{
    /// <summary>
    /// Search matches the invoice number, the customer or the branch as printed on the invoice.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<InvoiceListItemDto>> GetAll(
        Guid? customerId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        InvoiceStatus? status = null,
        bool unpaidOnly = false,
        string? search = null,
        InvoiceEmailStatus? emailStatus = null,
        CancellationToken ct = default)
    {
        var text = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var rows = await InvoiceQueries.ProjectList(
                db.Invoices
                    .Where(i => customerId == null || i.CustomerId == customerId)
                    .Where(i => from == null || i.InvoiceDate >= from)
                    .Where(i => to == null || i.InvoiceDate <= to)
                    .Where(i => status == null || i.Status == status)
                    .Where(i => text == null ||
                                i.InvoiceNumber.Contains(text) ||
                                i.CustomerName.Contains(text) ||
                                (i.BranchName != null && i.BranchName.Contains(text)))
                    .Where(i => emailStatus == null ||
                                db.InvoiceEmailLogs
                                    .Where(l => l.InvoiceId == i.Id)
                                    .OrderByDescending(l => l.CreatedAt)
                                    .Select(l => (InvoiceEmailStatus?)l.Status)
                                    .FirstOrDefault() == emailStatus)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.CreatedAt),
                db)
            .ToListAsync(ct);

        return unpaidOnly ? rows.Where(i => i.Outstanding > 0m).ToList() : rows;
    }

    [HttpGet("{id:guid}")]
    public Task<InvoiceDetailDto> GetById(Guid id, CancellationToken ct) => invoices.GetDetailAsync(id, ct);

    /// <summary>
    /// What the bill would come to - agreed rates, discount, tax, round-off - by the same code that
    /// finalizes it. The New Bill screen shows this instead of doing its own arithmetic.
    /// </summary>
    [HttpPost("preview")]
    public Task<InvoicePreviewDto> Preview(CreateInvoiceRequest request, CancellationToken ct) =>
        invoices.PreviewAsync(request, ct);

    /// <summary>
    /// Finalizes the bill: number, stock and all, in one transaction. Then makes the PDF; if that
    /// fails the invoice still stands and the PDF can be made again from the invoice page.
    /// </summary>
    [HttpPost]
    public async Task<CreateInvoiceResponse> Create(CreateInvoiceRequest request, CancellationToken ct)
    {
        var created = await invoices.CreateAsync(request, ct);

        if (!await documents.TryEnsureGeneratedAsync(created.Invoice.Id, ct))
        {
            return created with
            {
                Warnings = [.. created.Warnings, "The invoice is saved, but its PDF could not be made yet. Open it to try again."]
            };
        }

        return created with { Invoice = await invoices.GetDetailAsync(created.Invoice.Id, ct) };
    }

    /// <summary>Reverses a bill: the stock goes back and nothing is owed. The bill and its number are kept.</summary>
    [HttpPost("{id:guid}/cancel")]
    public Task<InvoiceDetailDto> Cancel(Guid id, CancelInvoiceRequest request, CancellationToken ct) =>
        invoices.CancelAsync(id, request.Reason, ct);

    /// <summary>
    /// The stored PDF - the same file every time. Inline for viewing and printing, or as a download.
    /// Served only through here, to a signed-in admin: there is no public URL to guess.
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> GetPdf(Guid id, bool download = false, CancellationToken ct = default)
    {
        var pdf = await documents.GetPdfAsync(id, ct);

        Response.Headers.CacheControl = "private, no-store";

        if (download)
        {
            return File(pdf.Content, "application/pdf", pdf.FileName);
        }

        // Inline so the browser can show and print it; the name is kept for "Save as". The name is
        // made by the server from the invoice number, so it needs no escaping.
        Response.Headers.ContentDisposition = $"inline; filename=\"{pdf.FileName}\"";

        return File(pdf.Content, "application/pdf");
    }

    /// <summary>Makes the PDF if it has not been made yet - the retry for a failed generation.</summary>
    [HttpPost("{id:guid}/pdf")]
    public async Task<InvoiceDetailDto> GeneratePdf(Guid id, CancellationToken ct)
    {
        await documents.EnsureGeneratedAsync(id, ct);

        return await invoices.GetDetailAsync(id, ct);
    }

    /// <summary>Sends, or re-sends, the invoice PDF. A delivery failure comes back as a Failed attempt, not an error.</summary>
    [HttpPost("{id:guid}/email")]
    public Task<InvoiceEmailLogDto> Email(Guid id, EmailInvoiceRequest request, CancellationToken ct) =>
        email.SendAsync(id, request.Recipient, ct);

    [HttpGet("{id:guid}/emails")]
    public Task<IReadOnlyList<InvoiceEmailLogDto>> EmailHistory(Guid id, CancellationToken ct) =>
        email.GetHistoryAsync(id, ct);
}
