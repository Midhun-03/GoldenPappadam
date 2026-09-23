using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Sales;

/// <summary>
/// The stored PDF of a finalized invoice - Golden Pappadam's own copy. One per invoice, written
/// once and never replaced, so what was printed and emailed is what stays on file.
/// </summary>
public class InvoiceDocument : Entity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>Where the storage provider keeps it. Opaque: a local path today, a bucket key later.</summary>
    public required string StorageKey { get; set; }

    public required string FileName { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>SHA-256 of the file, so a copy can be proved to be the one that was issued.</summary>
    public required string Sha256 { get; set; }
}
