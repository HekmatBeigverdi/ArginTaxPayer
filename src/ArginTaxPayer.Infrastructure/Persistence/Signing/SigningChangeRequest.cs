namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Metadata only. Never store private keys, passwords or envelope bytes here.</summary>
public sealed class SigningChangeRequest
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public Guid CurrentKeyVersionId { get; set; }
    public long ExpectedKeyRevision { get; set; }
    public short Kind { get; set; }
    public short State { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByOperationRecordId { get; set; }
    public long Revision { get; set; }
}
