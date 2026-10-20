namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Metadata only. Never store private keys, passwords or envelope bytes here.</summary>
public sealed class SigningKeyVersion
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public Guid OriginOperationRecordId { get; set; }
    public short Origin { get; set; }
    public long VersionNumber { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string? PublicFingerprintSha256 { get; set; }
    public byte[]? PublicSpkiDer { get; set; }
    public string? CustodyReference { get; set; }
    public string? ProtectionKeyVersion { get; set; }
    public short LifecycleState { get; set; }
    public short PortalState { get; set; }
    public short PublicMatch { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
