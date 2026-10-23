namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Encrypted envelope only; never map raw PKCS8, a plaintext DEK or the master key.</summary>
public sealed class DbSigningKeyEnvelope
{
    public Guid KeyVersionId { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public Guid OriginOperationRecordId { get; set; }
    public string EnvelopeJson { get; set; } = "";
    public int Version { get; set; }
}
