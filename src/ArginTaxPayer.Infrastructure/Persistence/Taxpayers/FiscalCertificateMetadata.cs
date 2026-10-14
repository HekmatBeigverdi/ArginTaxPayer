using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>Only public certificate metadata and opaque restricted custody locator.</summary>
public sealed class FiscalCertificateMetadata
{
    public Guid Id { get; set; }
    public Guid FiscalMemoryId { get; set; }
    public string FingerprintSha256 { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public DateTime? NotBeforeUtc { get; set; }
    public DateTime? NotAfterUtc { get; set; }
    public CertificateStatus Status { get; set; } = CertificateStatus.MetadataOnly;
    public CertificateOrigin Origin { get; set; }
    public CertificateCustodyMode CustodyMode { get; set; } = CertificateCustodyMode.ExternalVaultReference;
    public string CustodyReference { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public FiscalMemory FiscalMemory { get; set; } = null!;
}
