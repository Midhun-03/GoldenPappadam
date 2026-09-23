using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.FieldSales;

/// <summary>
/// One thing a phone sent, and the record it became. This is the whole of the idempotency
/// mechanism: <see cref="ClientRequestId"/> is generated once on the device when the salesperson
/// saves, never regenerated on a retry, and unique here. Send the same sale twice - because the
/// signal dropped after the server committed but before the phone heard back - and the second
/// attempt finds this row and returns the original bill instead of making another one.
///
/// It also carries the two timestamps that make offline work legible to the office: when the
/// salesperson actually recorded it, and when it reached the server.
/// </summary>
public class SyncSubmission : Entity
{
    public Guid ClientRequestId { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    public SubmissionType SubmissionType { get; set; }

    /// <summary>UTC, on the device, when the salesperson saved it.</summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>UTC, when it reached the server. The gap is how long the phone was out of signal.</summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>The record this became: an invoice, payment, visit, customer, branch or customer price.</summary>
    public Guid CreatedRecordId { get; set; }

    /// <summary>
    /// The price charged differs from the price that applies now, because the office changed it
    /// while the phone was offline. The sale keeps the price it was made at; this asks a person
    /// to look. See docs/03-field-sales-design.md B6.
    /// </summary>
    public bool PriceMismatch { get; set; }
}
