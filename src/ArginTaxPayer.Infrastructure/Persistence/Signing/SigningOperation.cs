namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Metadata only. Never store private keys, passwords or envelope bytes here.</summary>
public sealed class SigningOperation
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public Guid OperationId { get; set; }
    public Guid ActorUserId { get; set; }
    public short OperationKind { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string MetadataDigestSha256 { get; set; } = string.Empty;
    public short State { get; set; }
    public string? SafeErrorCode { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
