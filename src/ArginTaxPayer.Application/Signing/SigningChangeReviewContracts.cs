using ArginTaxPayer.Domain.Signing;

namespace ArginTaxPayer.Application.Signing;

public sealed record SigningChangeReviewRequest(Guid Id, Guid CustomerUserId, Guid TaxpayerId,
    Guid FiscalMemoryId, Guid CurrentKeyVersionId, long ExpectedKeyRevision, SigningChangeKind Kind,
    Guid RequestedByUserId, string Reason, DateTime RequestedAtUtc, SigningChangeReviewState State,
    Guid? ReviewedByUserId = null, DateTime? ReviewedAtUtc = null, DateTime? ExpiresAtUtc = null);

public enum SigningChangeReviewStatus { Approved, Rejected, Consumed, Expired, Invalid, Conflict, Forbidden }
public sealed record SigningChangeReviewResult(SigningChangeReviewStatus Status, SigningChangeReviewRequest Request);

public interface ISigningChangeReviewService
{
    SigningChangeReviewResult Decide(SigningChangeReviewRequest request, Guid approverUserId,
        bool approve, string decisionReason, DateTime nowUtc);
    SigningChangeReviewResult Consume(SigningChangeReviewRequest request, Guid operationRecordId,
        Guid actorUserId, DateTime nowUtc);
}
