namespace ArginTaxPayer.Application.Signing;

// Customer identity is resolved from the authenticated session, never accepted from this request.
public sealed record GenerateSigningKeyRequest(Guid TaxpayerId, Guid FiscalMemoryId, Guid OperationId,
    string ProfileId, string ConsentVersion, bool AcceptServerCustody);

public enum SigningGenerationStatus
{
    Created, Replayed, Invalid, Unauthenticated, Forbidden, NotFound, Conflict,
    IntegrityFailure, Unavailable, RecoveryRequired
}

public sealed record SigningPublicKeyArtifact(Guid KeyVersionId, string ProfileId,
    string FingerprintSha256, string Pem, string FileName, long Revision, short LifecycleState)
{
    public string Encoding => "SPKI-DER/PUBLIC-KEY-PEM";
    public string ContentType => "application/x-pem-file";
    public string Purpose => "TechnicalReview";
    public string PortalVerification => "Unverified";
    public bool PortalUploadApproved => false;
}

public sealed record SigningKeyGenerationResult(SigningGenerationStatus Status,
    SigningPublicKeyArtifact? PublicKey = null);

public interface ISigningKeyGenerationService
{
    Task<SigningKeyGenerationResult> GenerateAsync(string? bearer, GenerateSigningKeyRequest request,
        CancellationToken ct = default);
    Task<SigningKeyGenerationResult> ReadPublicAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, Guid keyVersionId, CancellationToken ct = default);
}
