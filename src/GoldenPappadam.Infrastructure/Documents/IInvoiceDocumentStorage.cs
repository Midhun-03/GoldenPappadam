namespace GoldenPappadam.Infrastructure.Documents;

/// <summary>
/// Where invoice PDFs are kept. Invoice code only ever sees a key - never a folder, a drive or a
/// bucket - so the local-disk implementation can be swapped for Supabase Storage (or any object
/// store) by registering a different class, without touching how invoices are made.
///
/// Keys look like "invoices/2026-27/GP-26-27-000125-3f2a....pdf": forward slashes, no "..", made
/// by the server only. Nothing a user types ever becomes part of a key.
/// </summary>
public interface IInvoiceDocumentStorage
{
    /// <summary>Stores a new file. Refuses to overwrite: an issued invoice's copy is never replaced.</summary>
    Task SaveInvoicePdfAsync(string key, byte[] content, CancellationToken ct);

    /// <summary>Null when nothing is stored under the key.</summary>
    Task<byte[]?> GetInvoicePdfAsync(string key, CancellationToken ct);

    /// <summary>
    /// Only for a file that never became the record - the loser of two simultaneous generations.
    /// An invoice's stored copy is never deleted.
    /// </summary>
    Task DeleteUnusedInvoicePdfAsync(string key, CancellationToken ct);
}
