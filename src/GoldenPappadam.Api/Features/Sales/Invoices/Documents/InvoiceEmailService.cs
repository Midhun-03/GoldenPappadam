using System.Globalization;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Email;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices.Documents;

/// <summary>
/// Emails the stored invoice PDF. Email is a courtesy on top of a finished invoice: a failure is
/// recorded in the invoice's email history for the office to retry, and the invoice, its number
/// and its PDF are untouched either way.
/// </summary>
public class InvoiceEmailService(
    AppDbContext db,
    InvoiceService invoices,
    InvoiceDocumentService documents,
    IEmailSender sender,
    ILogger<InvoiceEmailService> logger)
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>
    /// Sends, or tries to, and returns the attempt as recorded. A delivery failure is an answer, not
    /// an exception: it comes back with status Failed and the server's reason.
    /// </summary>
    public async Task<InvoiceEmailLogDto> SendAsync(Guid invoiceId, string? requestedRecipient, CancellationToken ct)
    {
        var invoice = await invoices.GetDetailAsync(invoiceId, ct);

        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            throw new DomainException($"Invoice {invoice.InvoiceNumber} is cancelled, so it is not sent to the customer.");
        }

        var recipient = string.IsNullOrWhiteSpace(requestedRecipient) ? invoice.CustomerEmail : requestedRecipient.Trim();

        if (recipient is null)
        {
            throw new DomainException(
                $"{invoice.CustomerName} has no email address. Add one on the customer, or type an address to send to.");
        }

        // Only a finalized invoice with a stored, verified PDF is ever sent.
        var pdf = await documents.GetPdfAsync(invoiceId, ct);
        var settings = await db.InvoiceSettings.AsNoTracking().SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);

        var subject = $"{InvoicePdfRenderer.Title(invoice.DocumentType)} {invoice.InvoiceNumber} — {settings.LegalName}";
        var attempt = await db.InvoiceEmailLogs.CountAsync(l => l.InvoiceId == invoiceId, ct) + 1;

        var log = new InvoiceEmailLog
        {
            InvoiceId = invoiceId,
            Recipient = recipient,
            Subject = subject,
            AttemptNumber = attempt
        };

        try
        {
            await sender.SendAsync(
                new EmailMessage(
                    recipient,
                    subject,
                    Body(invoice, settings),
                    [new EmailAttachment(pdf.FileName, "application/pdf", pdf.Content)],
                    settings.Email),
                ct);

            log.Status = InvoiceEmailStatus.Sent;
        }
        catch (EmailDeliveryException exception)
        {
            logger.LogWarning(exception, "Emailing invoice {InvoiceNumber} to {Recipient} failed", invoice.InvoiceNumber, recipient);

            log.Status = InvoiceEmailStatus.Failed;
            log.ErrorMessage = exception.Message.Length > 500 ? exception.Message[..500] : exception.Message;
        }

        db.InvoiceEmailLogs.Add(log);
        await db.SaveChangesAsync(ct);

        return (await GetHistoryAsync(invoiceId, ct)).First(l => l.Id == log.Id);
    }

    public async Task<IReadOnlyList<InvoiceEmailLogDto>> GetHistoryAsync(Guid invoiceId, CancellationToken ct) =>
        await db.InvoiceEmailLogs
            .Where(l => l.InvoiceId == invoiceId)
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.AttemptNumber)
            .Select(l => new InvoiceEmailLogDto(
                l.Id,
                l.CreatedAt,
                l.Recipient,
                l.Subject,
                l.Status,
                l.ErrorMessage,
                l.AttemptNumber,
                db.Users.Where(u => u.Id == l.CreatedBy).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);

    private static string Body(InvoiceDetailDto invoice, InvoiceSettings settings)
    {
        var lines = new List<string>
        {
            $"Dear {invoice.CustomerName},",
            "",
            $"Please find attached {InvoicePdfRenderer.Title(invoice.DocumentType).ToLowerInvariant()} " +
            $"{invoice.InvoiceNumber} dated {invoice.InvoiceDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} " +
            $"for Rs. {invoice.TotalAmount.ToString("#,##0.00", India)}" +
            (invoice.BranchName is null ? "." : $", for your {invoice.BranchName} branch.")
        };

        if (invoice.Outstanding > 0m && invoice.Outstanding < invoice.TotalAmount)
        {
            lines.Add($"Rs. {invoice.Outstanding.ToString("#,##0.00", India)} of it is still due.");
        }

        if (settings.PaymentTerms is not null)
        {
            lines.Add("");
            lines.Add(settings.PaymentTerms);
        }

        lines.AddRange(["", "Thank you for your business.", "", settings.LegalName]);

        if (settings.Phone is not null)
        {
            lines.Add(settings.Phone);
        }

        return string.Join(Environment.NewLine, lines);
    }
}
