using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Matching;

internal sealed record KeyMatchTicket(Guid IntentId, KeyProtectionIdentity Identity,
    long Revision, byte[] StoredSpki, string StoredFingerprint, string ProvidedFingerprint);
internal sealed record KeyMatchAdmission(SigningKeyMatchStatus Status, KeyMatchTicket? Ticket = null);
internal interface ISigningKeyMatchJournal
{
    Task<SigningKeyMatchStatus> AuthorizeMatchAsync(string? bearer, MatchSigningKeyRequest request, CancellationToken ct);
    Task<KeyMatchAdmission> BeginMatchAsync(string? bearer, MatchSigningKeyRequest request, string providedFingerprint, CancellationToken ct);
    Task<SigningKeyMatchResult> CompleteMatchAsync(string? bearer, KeyMatchTicket ticket, bool matched, CancellationToken ct);
}
internal interface IKeyMatchProof
{
    Task<bool> ProveAsync(KeyMatchTicket ticket, byte[] providedSpki, CancellationToken ct);
}
