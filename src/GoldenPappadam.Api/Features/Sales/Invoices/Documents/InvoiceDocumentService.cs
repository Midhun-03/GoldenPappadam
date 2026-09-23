using System.Security.Cryptography;
using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Documents;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Sales.Invoices.Documents;

/// <summary>
/// Golden Pappadam's own copy of every invoice. The PDF is made once, after the invoice is safely
/// committed, stored through <see cref="IInvoiceDocumentStorage"/>, fingerprinted, and never made
/// again - so the file printed today, emailed tomorrow and downloaded next year is the same file.
///
/// Generation happens straight after an office bill is saved. A sale synced from a phone gets its
/// PDF the first time anyone opens, prints or emails it, which keeps the sync fast and keeps a
/// printing problem from ever holding up a salesperson's batch. Either way the content is the
/// same, because it is drawn from the invoice's frozen snapshot.
/// </summary>
public class InvoiceDocumentService(
    AppDbContext db,
    InvoiceService invoices,
    IInvoiceDocumentStorage storage,
    ILogger<InvoiceDocumentService> logger)
{
    private static readonly int[] UniqueViolationErrors = [2601, 2627];

    public record StoredPdf(string FileName, byte[] Content);

    /// <summary>For straight after finalizing: a failure is logged and shown later, never thrown at the sale.</summary>
    public async Task<bool> TryEnsureGeneratedAsync(Guid invoiceId, CancellationToken ct)
    {
        try
        {
            await EnsureGeneratedAsync(invoiceId, ct);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not generate the PDF for invoice {InvoiceId}", invoiceId);
            return false;
        }
    }

    public async Task<InvoiceDocument> EnsureGeneratedAsync(Guid invoiceId, CancellationToken ct)
    {
        var existing = await db.InvoiceDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.InvoiceId == invoiceId, ct);

        if (existing is not null)
        {
            return existing;
        }

        var invoice = await invoices.GetDetailAsync(invoiceId, ct);
        var settings = await db.InvoiceSettings.AsNoTracking().SingleAsync(s => s.Id == InvoiceSettings.SingletonId, ct);

        var content = InvoicePdfRenderer.Render(invoice, new InvoicePdfRenderer.BusinessDetails(
            settings.Phone, settings.Email, settings.PaymentTerms, settings.BankDetails, settings.TermsAndConditions));

        // A fresh key per attempt: if two requests generate at the same moment, each writes its
        // own file and the database decides which one is the record.
        var fileName = FileNameFor(invoice.InvoiceNumber);
        var key = $"invoices/{invoice.FinancialYear}/{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}.pdf";

        await storage.SaveInvoicePdfAsync(key, content, ct);

        var document = new InvoiceDocument
        {
            InvoiceId = invoiceId,
            StorageKey = key,
            FileName = fileName,
            SizeBytes = content.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(content))
        };

        db.InvoiceDocuments.Add(document);

        try
        {
            await db.SaveChangesAsync(ct);
            return document;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql &&
                                                   UniqueViolationErrors.Contains(sql.Number))
        {
            // The other request won. Its file is the record; this one was never referenced.
            db.Entry(document).State = EntityState.Detached;
            await storage.DeleteUnusedInvoicePdfAsync(key, ct);

            return await db.InvoiceDocuments.AsNoTracking().SingleAsync(d => d.InvoiceId == invoiceId, ct);
        }
    }

    /// <summary>
    /// The stored copy, checked against its fingerprint. A missing or altered file is reported,
    /// never silently regenerated: a new file would not be the document that was issued.
    /// </summary>
    public async Task<StoredPdf> GetPdfAsync(Guid invoiceId, CancellationToken ct)
    {
        var document = await EnsureGeneratedAsync(invoiceId, ct);
        var content = await storage.GetInvoicePdfAsync(document.StorageKey, ct);

        if (content is null)
        {
            logger.LogError("The stored PDF {StorageKey} for invoice {InvoiceId} is missing", document.StorageKey, invoiceId);
            throw new DomainException("The stored copy of this invoice is missing from document storage.");
        }

        if (Convert.ToHexStringLower(SHA256.HashData(content)) != document.Sha256)
        {
            logger.LogError("The stored PDF {StorageKey} for invoice {InvoiceId} does not match its fingerprint",
                document.StorageKey, invoiceId);
            throw new DomainException("The stored copy of this invoice has been altered and will not be served.");
        }

        return new StoredPdf(document.FileName, content);
    }

    /// <summary>"Invoice-GP-26-27-000125.pdf" - slashes are not allowed in file names.</summary>
    public static string FileNameFor(string invoiceNumber) =>
        $"Invoice-{invoiceNumber.Replace('/', '-')}.pdf";
}
