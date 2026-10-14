using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>Taxpayer business record; never conflated with a login identity.</summary>
public sealed class TaxpayerProfile
{
    public Guid Id { get; set; }
    // All fiscal taxpayers are attached to a customer account, not directly to a Level2 partner.
    // Null denotes a legacy record requiring explicit owner reconciliation.
    public Guid? CustomerUserId { get; set; }
    public TaxpayerKind Kind { get; set; }
    public string NormalizedIdentifier { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TaxpayerStatus Status { get; set; } = TaxpayerStatus.Active;
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
