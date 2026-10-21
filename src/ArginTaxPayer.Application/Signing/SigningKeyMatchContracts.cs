namespace ArginTaxPayer.Application.Signing;

// These are accepted local comparison encodings, not certified portal-upload formats.
public enum SigningPublicKeyFormat { SpkiDer = 1, SpkiPem = 2, RsaPublicDer = 3, RsaPublicPem = 4 }
public enum SigningKeyMatchStatus
{
    Matched, Mismatch, Invalid, Unauthenticated, Forbidden, NotFound, Conflict,
    IntegrityFailure, Unavailable, RecoveryRequired
}
public sealed record MatchSigningKeyRequest(Guid TaxpayerId, Guid FiscalMemoryId,
    Guid KeyVersionId, long ExpectedRevision, SigningPublicKeyFormat Format);
public sealed record SigningKeyMatchResult(SigningKeyMatchStatus Status,
    Guid? KeyVersionId = null, string? StoredFingerprintSha256 = null,
    string? ProvidedFingerprintSha256 = null, long? Revision = null)
{
    public string PortalVerification => "Unverified";
    public bool PortalRegistrationProven => false;
}

// No signature, challenge, private material or provider locator is returned.
public interface ISigningKeyMatchService
{
    Task<SigningKeyMatchResult> MatchAsync(string? bearer, MatchSigningKeyRequest request,
        ReadOnlyMemory<byte> providedPublicKey, CancellationToken ct = default);
}
