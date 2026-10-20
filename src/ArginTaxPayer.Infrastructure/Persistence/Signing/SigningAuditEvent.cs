namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Metadata only. Never store private keys, passwords or envelope bytes here.</summary>
public sealed class SigningAuditEvent
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public Guid OperationRecordId { get; set; }
    public Guid ActorUserId { get; set; }
    public short SubjectKind { get; set; }
    public Guid SubjectId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string OutcomeCode { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string? PublicFingerprintSha256 { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
