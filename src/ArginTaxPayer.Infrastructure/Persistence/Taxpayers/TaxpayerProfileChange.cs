using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>Append-only business history; no credentials or tax signing secrets.</summary>
public sealed class TaxpayerProfileChange
{
    public Guid Id { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid ActorUserId { get; set; }
    public long FromRevision { get; set; }
    public long ToRevision { get; set; }
    public string PriorDisplayName { get; set; } = string.Empty;
    public string NewDisplayName { get; set; } = string.Empty;
    public TaxpayerStatus PriorStatus { get; set; }
    public TaxpayerStatus NewStatus { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
