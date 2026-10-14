namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>Historical, never hard-deleted taxpayer access assigned by Level 1.</summary>
public sealed class TaxpayerDelegateGrant
{
    public Guid Id { get; set; }
    public Guid TaxpayerId { get; set; }
    public Guid DelegateUserId { get; set; }
    public Guid GrantedByLevel1UserId { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? RevokedByLevel1UserId { get; set; }
    public string? RevocationReason { get; set; }
    public long Revision { get; set; } = 1;
}
