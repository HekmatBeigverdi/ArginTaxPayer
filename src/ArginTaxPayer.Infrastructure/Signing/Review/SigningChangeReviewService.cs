using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;

namespace ArginTaxPayer.Infrastructure.Signing.Review;

internal sealed class SigningChangeReviewService : ISigningChangeReviewService
{
    public SigningChangeReviewResult Decide(SigningChangeReviewRequest request, Guid approverUserId,
        bool approve, string decisionReason, DateTime nowUtc)
    {
        if (!Valid(request) || approverUserId == Guid.Empty || approverUserId == request.RequestedByUserId ||
            string.IsNullOrWhiteSpace(decisionReason) || decisionReason.Length > 1000 || decisionReason.Any(char.IsControl))
            return new(SigningChangeReviewStatus.Invalid, request);
        if (request.State != SigningChangeReviewState.Pending)
            return new(SigningChangeReviewStatus.Conflict, request);
        var reviewed = nowUtc.ToUniversalTime();
        var state = approve ? SigningChangeReviewState.Approved : SigningChangeReviewState.Rejected;
        var next = request with { State = state, ReviewedByUserId = approverUserId, ReviewedAtUtc = reviewed,
            ExpiresAtUtc = approve ? SigningChangeReviewPolicy.Expiry(reviewed) : null };
        return new(approve ? SigningChangeReviewStatus.Approved : SigningChangeReviewStatus.Rejected, next);
    }

    public SigningChangeReviewResult Consume(SigningChangeReviewRequest request, Guid operationRecordId,
        Guid actorUserId, DateTime nowUtc)
    {
        if (!Valid(request) || operationRecordId == Guid.Empty || actorUserId == Guid.Empty ||
            !SigningChangeReviewPolicy.IsUsable(request.State, nowUtc.ToUniversalTime(), request.ExpiresAtUtc))
            return new(request.ExpiresAtUtc is not null && request.ExpiresAtUtc <= nowUtc
                ? SigningChangeReviewStatus.Expired : SigningChangeReviewStatus.Invalid, request);
        var next = request with { State = SigningChangeReviewState.Consumed };
        return new(SigningChangeReviewStatus.Consumed, next);
    }

    private static bool Valid(SigningChangeReviewRequest r) => r is not null && r.Id != Guid.Empty &&
        r.CustomerUserId != Guid.Empty && r.TaxpayerId != Guid.Empty && r.FiscalMemoryId != Guid.Empty &&
        r.CurrentKeyVersionId != Guid.Empty && r.ExpectedKeyRevision > 0 && r.RequestedByUserId != Guid.Empty &&
        Enum.IsDefined(r.Kind) && r.Reason is { Length: > 0 and <= 1000 } && !r.Reason.Any(char.IsControl);
}
