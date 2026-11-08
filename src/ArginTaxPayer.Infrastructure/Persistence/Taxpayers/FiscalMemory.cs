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
    // NULL means an unclassified legacy memory. It can never authorize dispatch.
    public FiscalConnectionEnvironment? AuthorityEnvironment { get; set; }
    public FiscalMemoryStatus Status { get; set; } = FiscalMemoryStatus.PendingVerification;
    public bool? LastConnectionTestSucceeded { get; set; }
    public DateTime? LastConnectionTestAtUtc { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public TaxpayerProfile Taxpayer { get; set; } = null!;
}
