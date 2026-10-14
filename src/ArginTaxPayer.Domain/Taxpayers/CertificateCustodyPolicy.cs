using System.Text.RegularExpressions;

namespace ArginTaxPayer.Domain.Taxpayers;

public enum CertificateOrigin { Generated = 1, ImportedExistingKey = 2 }
public enum CertificateCustodyMode { ExternalVaultReference = 1 }

/// <summary>Metadata rules only; never receive private key bytes or PEM strings.</summary>
public static class CertificateCustodyPolicy
{
    public static bool ValidFingerprint(string? fingerprint) =>
        fingerprint is { Length: 64 } && fingerprint.All(Uri.IsHexDigit);

    public static bool ValidCustodyReference(string? reference) =>
        reference is { Length: >= 8 and <= 256 } &&
        reference.StartsWith("vault://", StringComparison.Ordinal) &&
        !reference.Any(char.IsWhiteSpace) &&
        !reference.Contains('@') && !reference.Contains('?') && !reference.Contains('#') &&
        !reference.Contains("..", StringComparison.Ordinal);

    public static bool ValidValidityWindow(DateTime? fromUtc, DateTime? untilUtc) =>
        fromUtc is null || untilUtc is null || (fromUtc.Value.Kind == DateTimeKind.Utc &&
        untilUtc.Value.Kind == DateTimeKind.Utc && untilUtc.Value > fromUtc.Value);

    public static CertificateStatus EffectiveStatus(CertificateStatus storedStatus, DateTime? fromUtc,
        DateTime? untilUtc, DateTime nowUtc)
    {
        if (storedStatus == CertificateStatus.Revoked) return CertificateStatus.Revoked;
        if (untilUtc is not null && untilUtc <= nowUtc) return CertificateStatus.Expired;
        if (storedStatus == CertificateStatus.Valid && fromUtc is not null && fromUtc > nowUtc)
            return CertificateStatus.MetadataOnly;
        return storedStatus;
    }
}

/// <summary>Future protected key custody integration; no implementation stores raw secrets in ordinary DB.</summary>
public interface IPrivateKeyCustody
{
    Task<string> ImportPrivateKeyAsync(Guid fiscalMemoryId, ReadOnlyMemory<byte> privateKey,
        CancellationToken cancellationToken = default);
    Task<string> GenerateKeyPairAsync(Guid fiscalMemoryId, CancellationToken cancellationToken = default);
    Task<string> GetPublicKeyFingerprintAsync(string custodyReference,
        CancellationToken cancellationToken = default);
}
