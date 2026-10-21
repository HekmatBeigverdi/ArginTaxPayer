using System.Text.RegularExpressions;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed partial class EfSigningOperationRepository : ISigningGenerationJournal
{
    private static SigningGenerationStatus GenerationStatus(SigningPersistenceStatus status) => status switch
    {
        SigningPersistenceStatus.Created => SigningGenerationStatus.Created,
        SigningPersistenceStatus.Updated => SigningGenerationStatus.Created,
        SigningPersistenceStatus.Replayed => SigningGenerationStatus.Replayed,
        SigningPersistenceStatus.Invalid => SigningGenerationStatus.Invalid,
        SigningPersistenceStatus.Unauthenticated => SigningGenerationStatus.Unauthenticated,
        SigningPersistenceStatus.Forbidden => SigningGenerationStatus.Forbidden,
        SigningPersistenceStatus.NotFound => SigningGenerationStatus.NotFound,
        SigningPersistenceStatus.IntegrityFailure => SigningGenerationStatus.IntegrityFailure,
        _ => SigningGenerationStatus.Conflict
    };

    async Task<GenerationReservation> ISigningGenerationJournal.ReserveGenerationAsync(string? bearer,
        GenerateSigningKeyRequest request, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(SigningGenerationStatus.Unauthenticated, default, Guid.Empty, default);
        var scope = new SigningScope(actor.Value, request.TaxpayerId, request.FiscalMemoryId);
        if (await AuthorizeAsync(bearer, scope, true, ct) is null)
            return new(SigningGenerationStatus.Forbidden, default, Guid.Empty, default);
        var result = await ReserveAsync(bearer, new(scope, request.OperationId, SigningOperationKind.Generate,
            request.ProfileId, request.ConsentVersion, request.AcceptServerCustody), ct);
        return new(GenerationStatus(result.Status), scope, result.Operation?.RecordId ?? Guid.Empty,
            result.Operation?.State ?? default);
    }

    async Task<GenerationAdmission> ISigningGenerationJournal.ClaimGenerationAsync(string? bearer,
        GenerationReservation reservation, Guid operationId, CancellationToken ct)
    {
        if (!Valid(reservation.Scope, operationId) || reservation.OperationRecordId == Guid.Empty)
            return new(SigningGenerationStatus.Invalid);
        KeyProtectionIdentity? allocated = null;
        var owns = false;
        var result = await WriteAsync(bearer, reservation.Scope, operationId, async (actor, now) =>
        {
            // Reset state on EVERY EF execution-strategy attempt, including ambiguous-commit replays.
            allocated = null;
            owns = false;
            var op = await Scoped(reservation.Scope, operationId).SingleOrDefaultAsync(ct);
            if (op is null || op.Id != reservation.OperationRecordId)
                return new(SigningPersistenceStatus.NotFound);
            if (op.ActorUserId != actor || op.OperationKind != 1 || op.ProfileId != SigningOperationPolicy.LegacyProfile ||
                op.MetadataDigestSha256 != RequestDigest(reservation.Scope, SigningOperationKind.Generate, op.ProfileId, null))
                return new(SigningPersistenceStatus.Conflict);
            if (await RecentLoginAsync(bearer, actor, now, ct) is null)
                return new(SigningPersistenceStatus.Forbidden);
            var existing = await db.SigningKeyVersions.AsNoTracking().SingleOrDefaultAsync(
                k => k.OriginOperationRecordId == op.Id, ct);
            if (existing is not null)
            {
                if (existing.CustomerUserId != op.CustomerUserId || existing.TaxpayerId != op.TaxpayerId ||
                    existing.FiscalMemoryId != op.FiscalMemoryId || existing.Origin != 1 ||
                    existing.ProfileId != op.ProfileId || existing.LifecycleState is not (1 or 2) ||
                    op.State is not (2 or 3 or 5))
                    return new(SigningPersistenceStatus.Conflict);
                allocated = new(op.CustomerUserId, op.TaxpayerId, op.FiscalMemoryId, existing.Id, op.Id);
                return new(SigningPersistenceStatus.Replayed, await ViewAsync(op, ct));
            }
            if (op.State != 1 || await db.SigningKeyVersions.AnyAsync(
                k => k.FiscalMemoryId == op.FiscalMemoryId, ct))
                return new(SigningPersistenceStatus.Conflict); // Rotation/replacement is a later controlled use case.
            if (!await db.SigningConsents.AnyAsync(c => c.OperationRecordId == op.Id &&
                c.ActorUserId == actor && c.ConsentVersion == SigningOperationPolicy.ConsentVersion, ct))
                return new(SigningPersistenceStatus.IntegrityFailure);
            var key = new SigningKeyVersion
            {
                Id = Guid.NewGuid(), CustomerUserId = op.CustomerUserId, TaxpayerId = op.TaxpayerId,
                FiscalMemoryId = op.FiscalMemoryId, OriginOperationRecordId = op.Id,
                Origin = 1, VersionNumber = 1, ProfileId = op.ProfileId, LifecycleState = 1,
                PortalState = 1, PublicMatch = 1, Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.SigningKeyVersions.Add(key);
            // Conservative durable intent: a crash after this commit must probe, never generate again.
            op.State = (short)SigningOperationState.RecoveryRequired;
            op.SafeErrorCode = "PROVIDER_OUTCOME_UNKNOWN";
            op.Revision++;
            op.UpdatedAtUtc = now < op.UpdatedAtUtc ? op.UpdatedAtUtc : now;
            SigningOperationAudit.Stage(db, op, actor, "generation_claimed", op.UpdatedAtUtc);
            StageKeyAudit(op, key, actor, "key_generation_intent", now);
            allocated = new(op.CustomerUserId, op.TaxpayerId, op.FiscalMemoryId, key.Id, op.Id);
            owns = true;
            return new(SigningPersistenceStatus.Created,
                new(op.Id, op.OperationId, (SigningOperationState)op.State, op.Revision, key.Id, null));
        }, ct);
        return new(GenerationStatus(result.Status), allocated,
            owns && result.Status == SigningPersistenceStatus.Created);
    }

    async Task<SigningKeyGenerationResult> ISigningGenerationJournal.CompleteGenerationAsync(string? bearer,
        Guid operationId, SealedGeneratedKey receipt, CancellationToken ct)
    {
        var id = receipt.Identity;
        var scope = new SigningScope(id.CustomerUserId, id.TaxpayerId, id.FiscalMemoryId);
        if (!Valid(scope, operationId) || id.KeyVersionId == Guid.Empty || id.OperationRecordId == Guid.Empty)
            return new(SigningGenerationStatus.Invalid);
        try { RsaSigningKeyMaterial.Artifact(id.KeyVersionId, receipt.PublicSpkiDer, 1, 2); }
        catch (KeyProtectionException) { return new(SigningGenerationStatus.IntegrityFailure); }
        if (RsaSigningKeyMaterial.Fingerprint(receipt.PublicSpkiDer) != receipt.PublicFingerprintSha256 ||
            receipt.ProtectionKeyVersion is not { Length: > 0 and <= 64 } ||
            !Regex.IsMatch(receipt.ProtectionKeyVersion, "^vault:v[1-9][0-9]*$", RegexOptions.CultureInvariant))
            return new(SigningGenerationStatus.IntegrityFailure);
        long keyRevision = 0;
        var result = await WriteAsync(bearer, scope, operationId, async (actor, now) =>
        {
            var op = await Scoped(scope, operationId).SingleOrDefaultAsync(ct);
            var key = await db.SigningKeyVersions.SingleOrDefaultAsync(k => k.Id == id.KeyVersionId &&
                k.CustomerUserId == id.CustomerUserId && k.TaxpayerId == id.TaxpayerId &&
                k.FiscalMemoryId == id.FiscalMemoryId && k.OriginOperationRecordId == id.OperationRecordId, ct);
            if (op is null || key is null || op.Id != id.OperationRecordId)
                return new(SigningPersistenceStatus.NotFound);
            if (op.ActorUserId != actor || op.OperationKind != 1 || key.Origin != 1 ||
                op.ProfileId != SigningOperationPolicy.LegacyProfile || key.ProfileId != op.ProfileId ||
                op.MetadataDigestSha256 != RequestDigest(scope, SigningOperationKind.Generate, op.ProfileId, null))
                return new(SigningPersistenceStatus.Conflict);
            keyRevision = key.Revision;
            var locator = "vault://" + id.StoragePath();
            if (op.State == 3 && key.LifecycleState == 2)
            {
                if (key.PublicFingerprintSha256 != receipt.PublicFingerprintSha256 ||
                    key.CustodyReference != locator || key.PublicSpkiDer is null ||
                    !key.PublicSpkiDer.AsSpan().SequenceEqual(receipt.PublicSpkiDer))
                    return new(SigningPersistenceStatus.Conflict);
                return new(SigningPersistenceStatus.Replayed, await ViewAsync(op, ct));
            }
            if (op.State is not (2 or 5) || key.LifecycleState != 1 ||
                await RecentLoginAsync(bearer, actor, now, ct) is null)
                return new(SigningPersistenceStatus.Forbidden);
            if (!await db.SigningConsents.AnyAsync(c => c.OperationRecordId == op.Id &&
                c.ActorUserId == actor && c.ConsentVersion == SigningOperationPolicy.ConsentVersion, ct))
                return new(SigningPersistenceStatus.IntegrityFailure);
            key.PublicSpkiDer = receipt.PublicSpkiDer.ToArray();
            key.PublicFingerprintSha256 = receipt.PublicFingerprintSha256;
            key.CustodyReference = locator;
            key.ProtectionKeyVersion = receipt.ProtectionKeyVersion;
            key.LifecycleState = 2;
            key.PortalState = 1;
            key.PublicMatch = 1;
            key.Revision++;
            keyRevision = key.Revision;
            key.UpdatedAtUtc = now < key.UpdatedAtUtc ? key.UpdatedAtUtc : now;
            op.State = 3;
            op.SafeErrorCode = null;
            op.Revision++;
            op.UpdatedAtUtc = now < op.UpdatedAtUtc ? op.UpdatedAtUtc : now;
            StageKeyAudit(op, key, actor, "key_generation_sealed", key.UpdatedAtUtc);
            SigningOperationAudit.Stage(db, op, actor, "generation_committed", op.UpdatedAtUtc);
            return new(SigningPersistenceStatus.Updated,
                new(op.Id, op.OperationId, SigningOperationState.Committed, op.Revision, key.Id, key.PublicFingerprintSha256));
        }, ct);
        var status = GenerationStatus(result.Status);
        return status is SigningGenerationStatus.Created or SigningGenerationStatus.Replayed
            ? new(status, RsaSigningKeyMaterial.Artifact(id.KeyVersionId, receipt.PublicSpkiDer, keyRevision, 2))
            : new(status);
    }

    private void StageKeyAudit(SigningOperation op, SigningKeyVersion key, Guid actor, string action, DateTime now) =>
        db.SigningAuditEvents.Add(new SigningAuditEvent
        {
            Id = Guid.NewGuid(), CustomerUserId = op.CustomerUserId, TaxpayerId = op.TaxpayerId,
            FiscalMemoryId = op.FiscalMemoryId, OperationRecordId = op.Id, ActorUserId = actor,
            SubjectKind = 2, SubjectId = key.Id, Revision = key.Revision, ActionCode = action,
            OutcomeCode = key.LifecycleState == 1 ? "Staged" : "LocallyReady",
            PublicFingerprintSha256 = key.PublicFingerprintSha256, OccurredAtUtc = now
        });

    async Task<SigningKeyGenerationResult> ISigningGenerationJournal.ReadGeneratedAsync(string? bearer,
        SigningScope scope, Guid keyId, Guid? operationRecordId, CancellationToken ct)
    {
        if (!SigningOperationPolicy.ValidScope(scope.CustomerUserId, scope.TaxpayerId, scope.FiscalMemoryId) ||
            keyId == Guid.Empty && operationRecordId is null)
            return new(SigningGenerationStatus.Invalid);
        if (await identity.CurrentUserIdAsync(bearer, ct) is null) return new(SigningGenerationStatus.Unauthenticated);
        if (await AuthorizeAsync(bearer, scope, false, ct) is null) return new(SigningGenerationStatus.Forbidden);
        var key = await db.SigningKeyVersions.AsNoTracking().SingleOrDefaultAsync(k =>
            k.CustomerUserId == scope.CustomerUserId && k.TaxpayerId == scope.TaxpayerId &&
            k.FiscalMemoryId == scope.FiscalMemoryId &&
            (keyId == Guid.Empty || k.Id == keyId) &&
            (operationRecordId == null || k.OriginOperationRecordId == operationRecordId) &&
            k.LifecycleState >= 2 && k.ProfileId == SigningOperationPolicy.LegacyProfile &&
            db.SigningOperations.Any(o => o.Id == k.OriginOperationRecordId && o.State == 3), ct);
        if (key is null) return new(SigningGenerationStatus.NotFound);
        if (key.PublicSpkiDer is null || RsaSigningKeyMaterial.Fingerprint(key.PublicSpkiDer) != key.PublicFingerprintSha256)
            return new(SigningGenerationStatus.IntegrityFailure);
        try { return new(SigningGenerationStatus.Replayed,
            RsaSigningKeyMaterial.Artifact(key.Id, key.PublicSpkiDer, key.Revision, key.LifecycleState)); }
        catch (KeyProtectionException) { return new(SigningGenerationStatus.IntegrityFailure); }
    }

    async Task<SigningKeyGenerationResult> ISigningGenerationJournal.ReadPublicAsync(string? bearer,
        Guid taxpayerId, Guid memoryId, Guid keyId, CancellationToken ct)
    {
        if (taxpayerId == Guid.Empty || memoryId == Guid.Empty || keyId == Guid.Empty)
            return new(SigningGenerationStatus.Invalid);
        if (await identity.CurrentUserIdAsync(bearer, ct) is null) return new(SigningGenerationStatus.Unauthenticated);
        var owner = await db.TaxpayerProfiles.AsNoTracking().Where(p => p.Id == taxpayerId)
            .Select(p => p.CustomerUserId).SingleOrDefaultAsync(ct);
        return owner is null ? new(SigningGenerationStatus.NotFound) :
            await ((ISigningGenerationJournal)this).ReadGeneratedAsync(bearer, new(owner.Value, taxpayerId, memoryId),
                keyId, null, ct);
    }
}
