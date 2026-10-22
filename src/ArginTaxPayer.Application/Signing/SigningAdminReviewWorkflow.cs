using ArginTaxPayer.Domain.Signing;

namespace ArginTaxPayer.Application.Signing;

public sealed record SigningAdminChangeRequest(Guid Id, Guid CustomerUserId,
    Guid TaxpayerId, string TaxpayerName, Guid FiscalMemoryId, string MemoryIdentifier,
    Guid CurrentKeyVersionId, long ExpectedKeyRevision, SigningChangeKind Kind,
    string ReasonCode, string Explanation, SigningChangeReviewState State,
    Guid RequestedByUserId, DateTime RequestedAtUtc, long Revision,
    DateTime? ReviewedAtUtc, DateTime? ExpiresAtUtc, string? DecisionReason);

public sealed record SigningAdminReviewDecision(Guid RequestId, long ExpectedRevision,
    bool Approve, string DecisionReason);

public enum SigningAdminReviewStatus { Updated, Unauthenticated, Forbidden, Invalid, NotFound, Conflict }
public sealed record SigningAdminReviewResult(SigningAdminReviewStatus Status,
    SigningAdminChangeRequest? Item = null);

public interface ISigningAdminReviewWorkflow
{
    Task<IReadOnlyList<SigningAdminChangeRequest>?> ListPendingAsync(string? bearer, CancellationToken ct = default);
    Task<SigningAdminReviewResult> DecideAsync(string? bearer,
        SigningAdminReviewDecision decision, CancellationToken ct = default);
}
