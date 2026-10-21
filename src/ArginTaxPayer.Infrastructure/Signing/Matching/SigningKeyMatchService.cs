using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Matching;

internal sealed class SigningKeyMatchService(ISigningKeyMatchJournal journal, IKeyMatchProof proof,
    SigningPublicKeyParser parser) : ISigningKeyMatchService
{
    public async Task<SigningKeyMatchResult> MatchAsync(string? bearer, MatchSigningKeyRequest request,
        ReadOnlyMemory<byte> providedPublicKey, CancellationToken ct = default)
    {
        if (request is null || request.TaxpayerId == Guid.Empty || request.FiscalMemoryId == Guid.Empty ||
            request.KeyVersionId == Guid.Empty || request.ExpectedRevision is < 1 or >= long.MaxValue - 1 ||
            !Enum.IsDefined(request.Format) || providedPublicKey.Length is < 1 or > 16384)
            return new(SigningKeyMatchStatus.Invalid);
        byte[]? owned = null;
        try
        {
            var status = await journal.AuthorizeMatchAsync(bearer, request, ct);
            if (status != SigningKeyMatchStatus.Matched) return new(status);
            ct.ThrowIfCancellationRequested();
            owned = providedPublicKey.ToArray();
            byte[] spki;
            try { spki = parser.Parse(request.Format, owned); }
            catch (KeyProtectionException) { return new(SigningKeyMatchStatus.Invalid); }
            var admission = await journal.BeginMatchAsync(bearer, request, RsaSigningKeyMaterial.Fingerprint(spki), ct);
            if (admission.Ticket is not { } ticket) return new(admission.Status);
            ct.ThrowIfCancellationRequested();
            var matched = await proof.ProveAsync(ticket, spki, ct);
            ct.ThrowIfCancellationRequested();
            // Completion rechecks identity, key revision and audit; nothing is released before commit.
            return await journal.CompleteMatchAsync(bearer, ticket, matched, ct);
        }
        catch (KeyProtectionException error)
        {
            return new(error.Failure switch
            {
                KeyProtectionFailure.Unavailable => SigningKeyMatchStatus.Unavailable,
                KeyProtectionFailure.Integrity => SigningKeyMatchStatus.IntegrityFailure,
                _ => SigningKeyMatchStatus.RecoveryRequired
            });
        }
        catch (OperationCanceledException) { return new(SigningKeyMatchStatus.RecoveryRequired); }
        finally { if (owned is not null) CryptographicOperations.ZeroMemory(owned); }
    }
}
