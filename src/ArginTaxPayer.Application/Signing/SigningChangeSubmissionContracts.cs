using System.Text.RegularExpressions;
using ArginTaxPayer.Domain.Signing;

namespace ArginTaxPayer.Application.Signing;

public sealed record SigningChangeRequestInput(Guid RequestId, long ExpectedKeyRevision,
    SigningChangeKind Kind, string ReasonCode, string Explanation);
public enum SigningChangeSubmissionStatus
{
    Created, Replayed, Invalid, Forbidden, NotFound, Conflict, SecondMemoryDenied, KeyNotReady
}
public sealed record SigningChangeRequestView(Guid Id, Guid FiscalMemoryId, SigningChangeKind Kind,
    string ReasonCode, string Explanation, SigningChangeReviewState State,
    long ExpectedKeyRevision, DateTime RequestedAtUtc, long Revision);
public sealed record SigningChangeSubmission(SigningChangeSubmissionStatus Status,
    SigningChangeRequestView? Request = null);
public interface ISigningChangeRequestSubmission
{
    Task<SigningChangeSubmission> SubmitAsync(string? bearer, Guid taxpayerId, Guid memoryId,
        SigningChangeRequestInput input, CancellationToken ct = default);
    Task<IReadOnlyList<SigningChangeRequestView>?> ListAsync(string? bearer, Guid taxpayerId,
        Guid memoryId, CancellationToken ct = default);
}

public static class SigningChangeSubmissionPolicy
{
    private static readonly Regex Reason = new("^[a-z][a-z0-9_]{2,63}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static bool Valid(SigningChangeRequestInput? input) =>
        input is not null && input.RequestId != Guid.Empty && input.ExpectedKeyRevision > 0 &&
        Enum.IsDefined(input.Kind) && input.ReasonCode is not null && Reason.IsMatch(input.ReasonCode) &&
        input.Explanation is { Length: >= 10 and <= 1000 } &&
        !string.IsNullOrWhiteSpace(input.Explanation) && !input.Explanation.Any(char.IsControl);
}
