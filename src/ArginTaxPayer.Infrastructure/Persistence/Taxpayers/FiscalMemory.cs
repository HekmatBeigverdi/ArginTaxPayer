using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>
/// A fiscal-memory identifier obtained from the tax authority, not generated locally.
/// Key material, certificate binaries and delegated access are not stored here.
/// </summary>
public sealed class FiscalMemory
{
    public Guid Id { get; set; }
    public Guid TaxpayerId { get; set; }
    public string MemoryIdentifier { get; set; } = string.Empty;
    public FiscalMemoryStatus Status { get; set; } = FiscalMemoryStatus.PendingVerification;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public TaxpayerProfile Taxpayer { get; set; } = null!;
}
