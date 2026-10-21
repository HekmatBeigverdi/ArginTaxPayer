using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Matching;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed partial class EfSigningOperationRepository : ISigningKeyMatchJournal
{
    private static SigningKeyMatchStatus MatchStatus(SigningPersistenceStatus status) => status switch
    {
        SigningPersistenceStatus.Created or SigningPersistenceStatus.Updated => SigningKeyMatchStatus.Matched,
        SigningPersistenceStatus.Unauthenticated => SigningKeyMatchStatus.Unauthenticated,
        SigningPersistenceStatus.Forbidden => SigningKeyMatchStatus.Forbidden,
        SigningPersistenceStatus.NotFound => SigningKeyMatchStatus.NotFound,
        SigningPersistenceStatus.Invalid => SigningKeyMatchStatus.Invalid,
        SigningPersistenceStatus.IntegrityFailure => SigningKeyMatchStatus.IntegrityFailure,
        _ => SigningKeyMatchStatus.Conflict
    };

    async Task<SigningKeyMatchStatus> ISigningKeyMatchJournal.AuthorizeMatchAsync(string? bearer,
        MatchSigningKeyRequest request, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return SigningKeyMatchStatus.Unauthenticated;
        var scope = new SigningScope(actor.Value, request.TaxpayerId, request.FiscalMemoryId);
        if (await AuthorizeAsync(bearer, scope, true, ct) is null ||
            await RecentLoginAsync(bearer, actor.Value, DateTime.UtcNow, ct) is null)
            return SigningKeyMatchStatus.Forbidden;
        return SigningKeyMatchStatus.Matched; // Internal admission only, not a public comparison result.
    }

    async Task<KeyMatchAdmission> ISigningKeyMatchJournal.BeginMatchAsync(string? bearer,
        MatchSigningKeyRequest request, string providedFingerprint, CancellationToken ct)
    {
        var actorId = await identity.CurrentUserIdAsync(bearer, ct);
        if (actorId is null) return new(SigningKeyMatchStatus.Unauthenticated);
        if (!SigningOperationPolicy.ValidFingerprint(providedFingerprint) || request.ExpectedRevision is < 1 or >= long.MaxValue - 1)
            return new(SigningKeyMatchStatus.Invalid);
        var scope = new SigningScope(actorId.Value, request.TaxpayerId, request.FiscalMemoryId);
        KeyMatchTicket? ticket = null;
        var result = await WriteAsync(bearer, scope, request.KeyVersionId, async (actor, now) =>
        {
            ticket = null; // Reset on every retry, including unknown commit outcomes.
            if (await RecentLoginAsync(bearer, actor, now, ct) is null) return new(SigningPersistenceStatus.Forbidden);
            var key = await MatchKeyAsync(scope, request.KeyVersionId, ct);
            if (key is null) return new(SigningPersistenceStatus.NotFound);
            if (key.Revision != request.ExpectedRevision || key.LifecycleState is not (2 or 3) ||
                await db.SigningKeySelections.AnyAsync(s => s.FiscalMemoryId == scope.FiscalMemoryId && s.KeyVersionId == key.Id, ct))
                return new(SigningPersistenceStatus.Conflict);
            if (!ValidMatchMetadata(key, scope)) return new(SigningPersistenceStatus.IntegrityFailure);
            if (!await db.SigningOperations.AnyAsync(o => o.Id == key.OriginOperationRecordId && o.State == 3 &&
                o.ActorUserId == actor && o.ProfileId == key.ProfileId && o.OperationKind == key.Origin, ct) ||
                !await db.SigningConsents.AnyAsync(c => c.OperationRecordId == key.OriginOperationRecordId &&
                    c.ActorUserId == actor && c.ConsentVersion == SigningOperationPolicy.ConsentVersion, ct))
                return new(SigningPersistenceStatus.IntegrityFailure);
            key.Revision++;
            key.UpdatedAtUtc = now < key.UpdatedAtUtc ? key.UpdatedAtUtc : now;
            var intent = StageMatchAudit(key, actor, "public_match_intent", "Pending", providedFingerprint, key.UpdatedAtUtc);
            ticket = new(intent.Id, new(scope.CustomerUserId, scope.TaxpayerId, scope.FiscalMemoryId,
                key.Id, key.OriginOperationRecordId), key.Revision, key.PublicSpkiDer!.ToArray(),
                key.PublicFingerprintSha256!, providedFingerprint);
            return new(SigningPersistenceStatus.Updated);
        }, ct);
        return new(MatchStatus(result.Status), result.Status == SigningPersistenceStatus.Updated ? ticket : null);
    }

    async Task<SigningKeyMatchResult> ISigningKeyMatchJournal.CompleteMatchAsync(string? bearer,
        KeyMatchTicket ticket, bool matched, CancellationToken ct)
    {
        var id = ticket.Identity;
        var scope = new SigningScope(id.CustomerUserId, id.TaxpayerId, id.FiscalMemoryId);
        if (matched != (ticket.StoredFingerprint == ticket.ProvidedFingerprint))
            return new(SigningKeyMatchStatus.IntegrityFailure);
        var result = await WriteAsync(bearer, scope, id.KeyVersionId, async (actor, now) =>
        {
            if (await RecentLoginAsync(bearer, actor, now, ct) is null) return new(SigningPersistenceStatus.Forbidden);
            var key = await MatchKeyAsync(scope, id.KeyVersionId, ct);
            if (key is null) return new(SigningPersistenceStatus.NotFound);
            if (key.Revision != ticket.Revision || key.Revision == long.MaxValue || key.LifecycleState is not (2 or 3) ||
                key.OriginOperationRecordId != id.OperationRecordId ||
                await db.SigningKeySelections.AnyAsync(s => s.FiscalMemoryId == scope.FiscalMemoryId && s.KeyVersionId == key.Id, ct))
                return new(SigningPersistenceStatus.Conflict);
            if (!ValidMatchMetadata(key, scope) || key.PublicFingerprintSha256 != ticket.StoredFingerprint ||
                !key.PublicSpkiDer!.AsSpan().SequenceEqual(ticket.StoredSpki))
                return new(SigningPersistenceStatus.IntegrityFailure);
            if (!await db.SigningAuditEvents.AnyAsync(a => a.Id == ticket.IntentId && a.SubjectKind == 2 &&
                a.SubjectId == key.Id && a.Revision == ticket.Revision && a.CustomerUserId == scope.CustomerUserId &&
                a.TaxpayerId == scope.TaxpayerId && a.FiscalMemoryId == scope.FiscalMemoryId &&
                a.OperationRecordId == id.OperationRecordId && a.ActorUserId == actor &&
                a.ActionCode == "public_match_intent" && a.PublicFingerprintSha256 == ticket.ProvidedFingerprint &&
                a.OccurredAtUtc <= now && a.OccurredAtUtc >= now.AddMinutes(-5), ct))
                return new(SigningPersistenceStatus.Forbidden);
            key.PublicMatch = matched ? (short)2 : (short)3;
            if (!matched) key.LifecycleState = 3; // Safety stop, never automatically resume on a later match.
            key.Revision++;
            key.UpdatedAtUtc = now < key.UpdatedAtUtc ? key.UpdatedAtUtc : now;
            StageMatchAudit(key, actor, "public_match_completed", matched ? "MatchesProvided" : "Mismatch",
                ticket.ProvidedFingerprint, key.UpdatedAtUtc);
            return new(SigningPersistenceStatus.Updated);
        }, ct);
        return result.Status == SigningPersistenceStatus.Updated
            ? new(matched ? SigningKeyMatchStatus.Matched : SigningKeyMatchStatus.Mismatch,
                id.KeyVersionId, ticket.StoredFingerprint, ticket.ProvidedFingerprint, ticket.Revision + 1)
            : new(MatchStatus(result.Status));
    }

    private Task<SigningKeyVersion?> MatchKeyAsync(SigningScope scope, Guid keyId, CancellationToken ct) =>
        db.SigningKeyVersions.SingleOrDefaultAsync(k => k.Id == keyId && k.CustomerUserId == scope.CustomerUserId &&
            k.TaxpayerId == scope.TaxpayerId && k.FiscalMemoryId == scope.FiscalMemoryId, ct);

    private static bool ValidMatchMetadata(SigningKeyVersion key, SigningScope scope)
    {
        if (key.ProfileId != SigningOperationPolicy.LegacyProfile || key.PublicSpkiDer is null ||
            RsaSigningKeyMaterial.Fingerprint(key.PublicSpkiDer) != key.PublicFingerprintSha256) return false;
        var id = new KeyProtectionIdentity(scope.CustomerUserId, scope.TaxpayerId, scope.FiscalMemoryId,
            key.Id, key.OriginOperationRecordId);
        if (key.CustodyReference != "vault://" + id.StoragePath()) return false;
        try { RsaSigningKeyMaterial.Artifact(key.Id, key.PublicSpkiDer, key.Revision, key.LifecycleState); return true; }
        catch (KeyProtectionException) { return false; }
    }

    private SigningAuditEvent StageMatchAudit(SigningKeyVersion key, Guid actor, string action, string outcome,
        string providedFingerprint, DateTime now)
    {
        var audit = new SigningAuditEvent
        {
            Id = Guid.NewGuid(), CustomerUserId = key.CustomerUserId, TaxpayerId = key.TaxpayerId,
            FiscalMemoryId = key.FiscalMemoryId, OperationRecordId = key.OriginOperationRecordId,
            ActorUserId = actor, SubjectKind = 2, SubjectId = key.Id, Revision = key.Revision,
            ActionCode = action, OutcomeCode = outcome, PublicFingerprintSha256 = providedFingerprint,
            OccurredAtUtc = now
        };
        db.SigningAuditEvents.Add(audit);
        return audit;
    }
}
