using ArginTaxPayer.Domain.Taxpayers;

namespace ArginTaxPayer.Infrastructure.Persistence.Taxpayers;

/// <summary>One active financial partner per customer, regardless of taxpayer count.</summary>
public sealed class CustomerCommercialOwner
{
    public Guid CustomerUserId { get; set; }
    public Guid RootLevel1UserId { get; set; }
    public Guid? PartnerLevel2UserId { get; set; }
    public CustomerAcquisitionChannel AcquisitionChannel { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CustomerCommercialOwnerChange
{
    public Guid Id { get; set; }
    public Guid CustomerUserId { get; set; }
    public Guid RootLevel1UserId { get; set; }
    public Guid? PreviousPartnerLevel2UserId { get; set; }
    public Guid? NewPartnerLevel2UserId { get; set; }
    public Guid ApprovedByLevel1UserId { get; set; }
    public CustomerOwnershipChangeReason Reason { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public long FromRevision { get; set; }
    public long ToRevision { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
