namespace ArginTaxPayer.Application.Signing;

public readonly record struct SigningScope(Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId);
public enum SigningOperationKind : short { Generate = 1, Import = 2 }
public enum SigningOperationState : short
{
    Reserved = 1, VaultStaged = 2, Committed = 3, Failed = 4, RecoveryRequired = 5
}
public enum SigningPersistenceStatus
{
    Created, Updated, Replayed, Invalid, Unauthenticated, Forbidden, NotFound, Conflict, IntegrityFailure
}
public enum SigningFailure { None, ProviderUnavailable, ProviderOutcomeUnknown, InvalidPublicMaterial, Cancelled }

/// <summary>Only non-secret metadata. Import fingerprint must be derived before this reservation.</summary>
public sealed record ReserveSigningOperation(
    SigningScope Scope, Guid OperationId, SigningOperationKind Kind,
    string ProfileId, string ConsentVersion, bool AcceptServerCustody,
    string? ExpectedPublicFingerprintSha256 = null);

public sealed record SigningOperationView(
    Guid RecordId, Guid OperationId, SigningOperationState State, long Revision,
    Guid? KeyVersionId, string? PublicFingerprintSha256);

public sealed record SigningPersistenceResult(
    SigningPersistenceStatus Status, SigningOperationView? Operation = null);

/// <summary>Internal application boundary, not a public signing API.
/// No provider calls run in these database-only retryable operations.
/// A caller token is authenticated on every access and is never persisted.</summary>
public interface ISigningOperationRepository
{
    Task<SigningPersistenceResult> ReserveAsync(string? bearer, ReserveSigningOperation request,
        CancellationToken ct = default);
    Task<SigningPersistenceResult> FindAsync(string? bearer, SigningScope scope, Guid operationId,
        CancellationToken ct = default);
    Task<SigningPersistenceResult> AdvanceAsync(string? bearer, SigningScope scope, Guid operationId,
        long expectedRevision, SigningOperationState target, SigningFailure failure = SigningFailure.None,
        CancellationToken ct = default);
}
