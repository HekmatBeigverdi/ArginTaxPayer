namespace ArginTaxPayer.Domain.Catalog;

public enum CatalogReleaseState { Staged = 1, Validated = 2, Active = 3, Superseded = 4 }

/// <summary>Edition and SHA identify a source. Import time is not legal effective time.</summary>
public sealed record CatalogReleaseIdentity(Guid Id, string Document, string Edition,
    string Sha256, CatalogReleaseState State, DateTime ImportedAtUtc);

public sealed record CatalogReferenceSnapshot(
    Guid ReleaseId, string Document, string Edition, string SourceSha256,
    string Code, string Title, DateTime CapturedAtUtc, DateTime? LegalEffectiveAtUtc);

public static class CatalogReleasePolicy
{
    public static bool CanTransition(CatalogReleaseState from, CatalogReleaseState to) =>
        (from, to) switch
        {
            (CatalogReleaseState.Staged, CatalogReleaseState.Validated) => true,
            (CatalogReleaseState.Validated, CatalogReleaseState.Active) => true,
            (CatalogReleaseState.Active, CatalogReleaseState.Superseded) => true,
            _ => false
        };

    public static bool IsReferenceWritable(CatalogReleaseState state) =>
        state == CatalogReleaseState.Staged;

    public static bool IsValidIdentity(CatalogReleaseIdentity release) =>
        release.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(release.Document) &&
        !string.IsNullOrWhiteSpace(release.Edition) &&
        release.Sha256.Length == 64 &&
        release.Sha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        release.ImportedAtUtc.Kind == DateTimeKind.Utc &&
        Enum.IsDefined(release.State);

    public static bool Overlaps(DateTime fromA, DateTime? toA, DateTime fromB, DateTime? toB)
    {
        if (fromA.Kind != DateTimeKind.Utc || fromB.Kind != DateTimeKind.Utc ||
            (toA is not null && (toA.Value.Kind != DateTimeKind.Utc || toA <= fromA)) ||
            (toB is not null && (toB.Value.Kind != DateTimeKind.Utc || toB <= fromB)))
            throw new ArgumentException("Invalid UTC half-open validity window");
        return (toA is null || fromB < toA.Value) && (toB is null || fromA < toB.Value);
    }

    public static CatalogReferenceSnapshot Capture(
        CatalogReleaseIdentity release, string code, string title, DateTime capturedAtUtc,
        DateTime? legalEffectiveAtUtc)
    {
        if (!IsValidIdentity(release) || release.State != CatalogReleaseState.Active ||
            string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) ||
            capturedAtUtc.Kind != DateTimeKind.Utc ||
            (legalEffectiveAtUtc is not null && legalEffectiveAtUtc.Value.Kind != DateTimeKind.Utc))
            throw new ArgumentException("Invalid activated release or reference snapshot data");
        return new CatalogReferenceSnapshot(release.Id, release.Document, release.Edition,
            release.Sha256, code, title, capturedAtUtc, legalEffectiveAtUtc);
    }
}
