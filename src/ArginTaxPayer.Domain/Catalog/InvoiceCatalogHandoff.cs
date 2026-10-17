namespace ArginTaxPayer.Domain.Catalog;

/// <summary>
/// Frozen *handoff data*, not a tax invoice, a persisted fiscal record or a tax calculation.
/// The caller must copy this value into its own immutable invoice-line snapshot at issuance.
/// </summary>
public sealed record InvoiceCatalogHandoff(
    int ContractVersion,
    Guid CustomerUserId,
    Guid CatalogItemId,
    long CatalogItemRevision,
    string InternalCode,
    string ItemTitle,
    CatalogItemKind ItemKind,
    Guid BaseUnitId,
    string OfficialUnitCode,
    string OfficialUnitTitle,
    Guid UnitReleaseId,
    string UnitReleaseDocument,
    string UnitReleaseEdition,
    string UnitReleaseSha256,
    Guid MappingId,
    long MappingRevision,
    DateTime MappingEffectiveFromUtc,
    DateTime? MappingEffectiveToUtc,
    Guid OfficialEntryId,
    string OfficialCode,
    string OfficialTitle,
    Guid OfficialReleaseId,
    string OfficialReleaseDocument,
    string OfficialReleaseEdition,
    string OfficialReleaseSha256,
    DateTime EffectiveAtUtc,
    DateTime CapturedAtUtc,
    InvoiceVatResolution Vat);

public enum InvoiceVatResolution { Unresolved = 0 }

public enum InvoiceCatalogHandoffDecision
{
    Success, Unauthenticated, Forbidden, Invalid, ItemNotFound, ItemInactive,
    MissingOrAmbiguousMapping, UnverifiedIdentifier, IncompatibleKind,
    UnitNotApproved, Conflict
}

public sealed record InvoiceCatalogHandoffResult(
    InvoiceCatalogHandoffDecision Decision, InvoiceCatalogHandoff? Snapshot = null);

public static class InvoiceCatalogHandoffPolicy
{
    public const int CurrentContractVersion = 1;

    public static bool IsEffective(DateTime fromUtc, DateTime? toUtc, DateTime asOfUtc) =>
        fromUtc.Kind == DateTimeKind.Utc &&
        asOfUtc.Kind == DateTimeKind.Utc &&
        (toUtc is null || (toUtc.Value.Kind == DateTimeKind.Utc && toUtc > fromUtc)) &&
        fromUtc <= asOfUtc && (toUtc is null || asOfUtc < toUtc.Value);

    public static bool CanCapture(
        int itemKind, int itemStatus, int officialKind, int verificationStatus,
        int officialReleaseState, int unitStatus, int unitReleaseState,
        bool reviewedSource, bool unitReviewed, DateTime effectiveAtUtc,
        DateTime mappingFromUtc, DateTime? mappingToUtc) =>
        itemStatus == (int)CatalogItemStatus.Active &&
        (itemKind is 1 or 2 or 3) &&
        OfficialGoodsServiceReferencePolicy.IsCompatible(itemKind, (OfficialEntryKind)officialKind) &&
        verificationStatus == (int)OfficialVerificationStatus.Verified &&
        officialReleaseState == (int)CatalogReleaseState.Active &&
        unitStatus == 1 && unitReleaseState == (int)CatalogReleaseState.Active &&
        reviewedSource && unitReviewed &&
        IsEffective(mappingFromUtc, mappingToUtc, effectiveAtUtc);

    public static bool IsComplete(InvoiceCatalogHandoff? s) =>
        s is { ContractVersion: CurrentContractVersion } &&
        s.CustomerUserId != Guid.Empty && s.CatalogItemId != Guid.Empty &&
        s.BaseUnitId != Guid.Empty && s.MappingId != Guid.Empty &&
        s.OfficialEntryId != Guid.Empty && s.OfficialReleaseId != Guid.Empty &&
        s.UnitReleaseId != Guid.Empty && s.CatalogItemRevision > 0 &&
        s.MappingRevision > 0 &&
        !string.IsNullOrWhiteSpace(s.InternalCode) &&
        !string.IsNullOrWhiteSpace(s.ItemTitle) &&
        !string.IsNullOrWhiteSpace(s.OfficialCode) &&
        !string.IsNullOrWhiteSpace(s.OfficialUnitCode) &&
        s.OfficialReleaseSha256.Length == 64 && s.UnitReleaseSha256.Length == 64 &&
        s.EffectiveAtUtc.Kind == DateTimeKind.Utc &&
        s.CapturedAtUtc.Kind == DateTimeKind.Utc &&
        s.Vat == InvoiceVatResolution.Unresolved;
}
