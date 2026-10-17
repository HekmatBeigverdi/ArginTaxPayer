using ArginTaxPayer.Domain.Invoices;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Stored canonical, revisioned invoice snapshot. No fiscal submission is performed.</summary>
public sealed class StoredInvoice
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public long Revision { get; set; }
    public InvoiceLifecycleState State { get; set; }
    public InvoiceSubject Subject { get; set; }
    public DateTime IssueDateUtc { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string SnapshotSha256 { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Idempotency outcome belongs to this invoice operation, not generic system reservations.</summary>
public sealed class StoredInvoiceOperation
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid InvoiceId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string FingerprintSha256 { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public InvoiceSubject Subject { get; set; }
    public string? ReferenceTaxId { get; set; }
    public int ReferenceSlot { get; set; }
    public bool IsClaimActive { get; set; }
    public string OutcomeJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// One DB-scoped CAS generation for reference reservations. All operations are
/// committed through a real transaction; never use in-memory registry as lock.
/// </summary>
public sealed class StoredInvoiceReservationCursor
{
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public long Revision { get; set; }
}

/// <summary>
/// Immutable canonical payload for one committed invoice revision.
/// PostgreSQL trigger rejects updates and deletes after insert.
/// </summary>
public sealed class StoredInvoiceRevision
{
    public Guid InvoiceId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public long Revision { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string SnapshotSha256 { get; set; } = "";
    public DateTime CapturedAtUtc { get; set; }
}
