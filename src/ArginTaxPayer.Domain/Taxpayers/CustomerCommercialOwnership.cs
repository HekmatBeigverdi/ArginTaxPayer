namespace ArginTaxPayer.Domain.Taxpayers;

/// <summary>
/// Customer account, not taxpayer/fiscal memory, is the unit of reseller
/// relationship and future financial settlement. Phase 12 implements ledgers.
/// </summary>
public enum CustomerAcquisitionChannel
{
    PublicSelfRegistration = 1,
    Level1Created = 2,
    Level2Created = 3,
    Level2ReferralApprovedByLevel1 = 4,
    LegacyReconciledByLevel1 = 5
}

public enum CustomerOwnershipChangeReason
{
    ApprovedReferral = 1,
    ManualCorrection = 2,
    PartnerDeactivation = 3,
    Other = 4
}

public sealed record CustomerOwnershipContract(
    Guid CustomerUserId,
    Guid RootLevel1UserId,
    Guid? PartnerLevel2UserId,
    CustomerAcquisitionChannel AcquisitionChannel,
    long Revision);

public sealed record CustomerOwnershipChangeContract(
    Guid CustomerUserId, Guid RootLevel1UserId,
    Guid? PreviousPartnerLevel2UserId, Guid? NewPartnerLevel2UserId,
    Guid DecidedByLevel1UserId, CustomerOwnershipChangeReason Reason,
    string Explanation, DateTimeOffset ChangedAtUtc);

/// <summary>
/// A monthly/annual package constrains how many fiscal taxpayers a customer may
/// activate, not how many customers can belong to a partner.
/// Public sign-up routes to Level1. Level2-created customer belongs to Level2.
/// Existing public customers require manual Level1 approval to transfer.
/// </summary>
public static class CustomerCommercialOwnershipPolicy
{
    public static Guid? InitialPartner(CustomerAcquisitionChannel channel, Guid? creatingLevel2Id) =>
        channel == CustomerAcquisitionChannel.Level2Created ? creatingLevel2Id : null;

    public static bool RequiresLevel1Approval(CustomerAcquisitionChannel channel) =>
        channel == CustomerAcquisitionChannel.Level2ReferralApprovedByLevel1;
}
