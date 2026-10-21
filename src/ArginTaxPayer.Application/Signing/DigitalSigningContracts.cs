namespace ArginTaxPayer.Application.Signing;

/// <summary>Trusted, immutable input selected by the server for one signing attempt.</summary>
public sealed record AuthorizedSigningRequest(
    Guid InvoiceId,
    Guid CustomerUserId,
    Guid TaxpayerId,
    Guid FiscalMemoryId,
    Guid KeyVersionId,
    long ExpectedInvoiceRevision,
    long ExpectedKeyRevision,
    string SnapshotSha256,
    string ProfileId);

public enum DigitalSigningStatus { Signed, Invalid, Forbidden, Conflict, Unavailable, IntegrityFailure, RecoveryRequired }

public sealed record SigningResult(
    DigitalSigningStatus Status,
    string? SignatureBase64 = null,
    string? SnapshotSha256 = null,
    Guid? KeyVersionId = null,
    string? ProfileId = null)
{
    public bool IsFiscalSubmissionAuthorized => false;
}

/// <summary>Internal application boundary; never expose a generic Sign(bytes) endpoint.</summary>
public interface IDigitalSigningService
{
    Task<SigningResult> SignAsync(string? bearer, AuthorizedSigningRequest request,
        CancellationToken ct = default);
}
