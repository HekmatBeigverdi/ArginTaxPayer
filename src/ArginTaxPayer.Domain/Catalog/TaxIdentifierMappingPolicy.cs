using ArginTaxPayer.Domain.Catalog;

namespace ArginTaxPayer.Domain.Catalog;

public sealed record TaxIdentifierMappingWindow(DateTime FromUtc, DateTime? ToUtc);

public static class TaxIdentifierMappingPolicy
{
    public static bool IsValidWindow(TaxIdentifierMappingWindow value) =>
        value.FromUtc.Kind == DateTimeKind.Utc &&
        (value.ToUtc is null ||
         (value.ToUtc.Value.Kind == DateTimeKind.Utc && value.ToUtc.Value > value.FromUtc));

    public static bool Overlaps(TaxIdentifierMappingWindow first, TaxIdentifierMappingWindow second)
    {
        if (!IsValidWindow(first) || !IsValidWindow(second)) return true;
        return (first.ToUtc is null || second.FromUtc < first.ToUtc.Value) &&
               (second.ToUtc is null || first.FromUtc < second.ToUtc.Value);
    }

    public static bool CanMap(int itemKind, int officialKind, int verificationStatus,
        int releaseState, bool hasTrustedSourceEvidence, bool itemIsActive) =>
        itemIsActive &&
        OfficialGoodsServiceReferencePolicy.IsCompatible(itemKind, (OfficialEntryKind)officialKind) &&
        OfficialGoodsServiceReferencePolicy.CanBeTreatedAsVerified(
            (OfficialVerificationStatus)verificationStatus, releaseState, hasTrustedSourceEvidence);

    // Replacement must not rewrite the earlier interval's start, code or source.
    public static bool CanClose(TaxIdentifierMappingWindow previous, DateTime closedAtUtc) =>
        IsValidWindow(previous) && previous.ToUtc is null &&
        closedAtUtc.Kind == DateTimeKind.Utc && closedAtUtc > previous.FromUtc;
}
