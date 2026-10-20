using System.Data;
using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

/// <summary>Scoped database-only operation journal. No key generation, vault access, public endpoint,
/// memory activation, entitlement consumption, or private material is implemented here.</summary>
public sealed class EfSigningOperationRepository : ISigningOperationRepository
{
    private readonly ArginTaxPayerDbContext db;
    private readonly IdentityAuthorizationService identity;
    private readonly ParticipantScopeService participants;

    public EfSigningOperationRepository(ArginTaxPayerDbContext db)
    {
        this.db = db;
        identity = new(db, new IdentitySessionService(db));
        participants = new(db);
    }

    private IQueryable<SigningOperation> Scoped(SigningScope scope, Guid operationId) =>
        db.SigningOperations.Where(x => x.CustomerUserId == scope.CustomerUserId &&
            x.TaxpayerId == scope.TaxpayerId && x.FiscalMemoryId == scope.FiscalMemoryId &&
            x.OperationId == operationId);

    private static bool Valid(SigningScope s, Guid operationId) =>
        SigningOperationPolicy.ValidScope(s.CustomerUserId, s.TaxpayerId, s.FiscalMemoryId) &&
        operationId != Guid.Empty;

    private async Task<Guid?> AuthorizeAsync(string? bearer, SigningScope scope, bool ownerOnly,
        CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null || ownerOnly && actor != scope.CustomerUserId) return null;
        if (!await participants.CanManageAsync(actor.Value, scope.CustomerUserId, ct)) return null;
        if (!await db.TaxpayerProfiles.AsNoTracking().AnyAsync(p =>
            p.Id == scope.TaxpayerId && p.CustomerUserId == scope.CustomerUserId &&
            p.Status == TaxpayerStatus.Active &&
            db.FiscalMemories.Any(m => m.Id == scope.FiscalMemoryId &&
                m.TaxpayerId == p.Id &&
                (m.Status == FiscalMemoryStatus.PendingVerification || m.Status == FiscalMemoryStatus.Active)), ct))
            return null;
        return actor;
    }

    private async Task<DateTime?> RecentLoginAsync(string? bearer, Guid actor, DateTime now,
        CancellationToken ct)
    {
        if (bearer is null) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(bearer))).ToLowerInvariant();
        var login = await db.IdentitySessions.AsNoTracking()
            .Where(s => s.UserId == actor && s.AccessTokenHash == hash && s.RevokedAtUtc == null &&
                s.AccessExpiresAtUtc > now && s.RefreshExpiresAtUtc > now)
            .Select(s => (DateTime?)s.CreatedAtUtc).SingleOrDefaultAsync(ct);
        return login is { } value && SigningOperationPolicy.FreshAuthentication(value, now) ? value : null;
    }

    private async Task<SigningOperationView> ViewAsync(SigningOperation op, CancellationToken ct)
    {
        var key = await db.SigningKeyVersions.AsNoTracking()
            .Where(k => k.OriginOperationRecordId == op.Id &&
                k.CustomerUserId == op.CustomerUserId && k.TaxpayerId == op.TaxpayerId &&
                k.FiscalMemoryId == op.FiscalMemoryId)
            .Select(k => new { k.Id, k.PublicFingerprintSha256 }).SingleOrDefaultAsync(ct);
        return new(op.Id, op.OperationId, (SigningOperationState)op.State, op.Revision,
            key?.Id, key?.PublicFingerprintSha256);
    }

    public async Task<SigningPersistenceResult> FindAsync(string? bearer, SigningScope scope,
        Guid operationId, CancellationToken ct = default)
    {
        if (!Valid(scope, operationId)) return new(SigningPersistenceStatus.Invalid);
        if (await identity.CurrentUserIdAsync(bearer, ct) is null)
            return new(SigningPersistenceStatus.Unauthenticated);
        if (await AuthorizeAsync(bearer, scope, false, ct) is null)
            return new(SigningPersistenceStatus.Forbidden);
        var op = await Scoped(scope, operationId).AsNoTracking().SingleOrDefaultAsync(ct);
        return op is null ? new(SigningPersistenceStatus.NotFound) :
            new(SigningPersistenceStatus.Replayed, await ViewAsync(op, ct));
    }

    public Task<SigningPersistenceResult> ReserveAsync(string? bearer, ReserveSigningOperation request,
        CancellationToken ct = default)
    {
        if (request is null || !Valid(request.Scope, request.OperationId) ||
            request.ProfileId != SigningOperationPolicy.LegacyProfile ||
            request.ConsentVersion != SigningOperationPolicy.ConsentVersion || !request.AcceptServerCustody ||
            request.Kind is not (SigningOperationKind.Generate or SigningOperationKind.Import) ||
            request.Kind == SigningOperationKind.Generate && request.ExpectedPublicFingerprintSha256 is not null ||
            request.Kind == SigningOperationKind.Import &&
                !SigningOperationPolicy.ValidFingerprint(request.ExpectedPublicFingerprintSha256))
            return Task.FromResult(new SigningPersistenceResult(SigningPersistenceStatus.Invalid));

        var digest = RequestDigest(request.Scope, request.Kind, request.ProfileId,
            request.ExpectedPublicFingerprintSha256);
        return WriteAsync(bearer, request.Scope, request.OperationId, async (actor, now) =>
        {
            var existing = await Scoped(request.Scope, request.OperationId).AsNoTracking().SingleOrDefaultAsync(ct);
            if (existing is not null)
            {
                if (existing.ActorUserId != actor || existing.MetadataDigestSha256 != digest)
                    return new(SigningPersistenceStatus.Conflict);
                return new(SigningPersistenceStatus.Replayed, await ViewAsync(existing, ct));
            }

            var login = await RecentLoginAsync(bearer, actor, now, ct);
            if (login is null) return new(SigningPersistenceStatus.Forbidden);
            var op = new SigningOperation
            {
                Id = Guid.NewGuid(), CustomerUserId = request.Scope.CustomerUserId,
                TaxpayerId = request.Scope.TaxpayerId, FiscalMemoryId = request.Scope.FiscalMemoryId,
                OperationId = request.OperationId, ActorUserId = actor, OperationKind = (short)request.Kind,
                ProfileId = request.ProfileId, MetadataDigestSha256 = digest,
                State = (short)SigningOperationState.Reserved, Revision = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            var consent = new SigningConsent
            {
                Id = Guid.NewGuid(), OperationRecordId = op.Id, CustomerUserId = op.CustomerUserId,
                TaxpayerId = op.TaxpayerId, FiscalMemoryId = op.FiscalMemoryId, ActorUserId = actor,
                ConsentVersion = SigningOperationPolicy.ConsentVersion,
                ReauthenticatedAtUtc = login.Value, ConsentedAtUtc = now
            };
            db.SigningOperations.Add(op);
            db.SigningConsents.Add(consent);
            SigningOperationAudit.Stage(db, op, actor, "operation_reserved", now, consent);
            return new(SigningPersistenceStatus.Created, await ViewAsync(op, ct));
        }, ct);
    }

    public Task<SigningPersistenceResult> AdvanceAsync(string? bearer, SigningScope scope,
        Guid operationId, long expectedRevision, SigningOperationState target,
        SigningFailure failure = SigningFailure.None, CancellationToken ct = default)
    {
        if (!Valid(scope, operationId) || expectedRevision < 1 || expectedRevision == long.MaxValue ||
            !Enum.IsDefined(target) || !Enum.IsDefined(failure) ||
            target is SigningOperationState.Reserved ||
            (target is SigningOperationState.Failed or SigningOperationState.RecoveryRequired
                ? failure == SigningFailure.None
                : failure != SigningFailure.None))
            return Task.FromResult(new SigningPersistenceResult(SigningPersistenceStatus.Invalid));

        return WriteAsync(bearer, scope, operationId, async (actor, now) =>
        {
            var op = await Scoped(scope, operationId).SingleOrDefaultAsync(ct);
            if (op is null) return new(SigningPersistenceStatus.NotFound);
            if (op.ActorUserId != actor) return new(SigningPersistenceStatus.Forbidden);
            if (op.OperationKind is not (1 or 2) || op.ProfileId != SigningOperationPolicy.LegacyProfile)
                return new(SigningPersistenceStatus.Conflict);
            if (op.Revision != expectedRevision)
            {
                if (op.Revision == expectedRevision + 1 && op.State == (short)target &&
                    op.SafeErrorCode == ErrorCode(failure))
                    return new(SigningPersistenceStatus.Replayed, await ViewAsync(op, ct));
                return new(SigningPersistenceStatus.Conflict);
            }
            if (!SigningOperationPolicy.CanAdvance(op.State, (short)target))
                return new(SigningPersistenceStatus.Conflict);

            if (target == SigningOperationState.Committed)
            {
                // Completion only follows independently persisted, sealed public metadata.
                // Actual mathematical/key-provider validation belongs to Steps05–09.
                var sealedKey = await db.SigningKeyVersions.AsNoTracking()
                    .Where(k => k.OriginOperationRecordId == op.Id && k.CustomerUserId == scope.CustomerUserId &&
                        k.TaxpayerId == scope.TaxpayerId && k.FiscalMemoryId == scope.FiscalMemoryId &&
                        k.LifecycleState == 2 && k.Origin == op.OperationKind && k.ProfileId == op.ProfileId &&
                        k.ProtectionKeyVersion != null && k.PublicMatch != 3 && k.PublicFingerprintSha256 != null &&
                        k.PublicSpkiDer != null && k.CustodyReference != null &&
                        db.SigningConsents.Any(c => c.OperationRecordId == op.Id &&
                            c.ActorUserId == scope.CustomerUserId))
                    .Select(k => new { k.PublicFingerprintSha256, k.PublicSpkiDer }).SingleOrDefaultAsync(ct);
                if (sealedKey is null || sealedKey.PublicSpkiDer is null ||
                    Convert.ToHexString(SHA256.HashData(sealedKey.PublicSpkiDer)).ToLowerInvariant()
                        != sealedKey.PublicFingerprintSha256)
                    return new(SigningPersistenceStatus.Conflict);
                var sealedDigest = RequestDigest(scope, (SigningOperationKind)op.OperationKind, op.ProfileId,
                    op.OperationKind == 2 ? sealedKey.PublicFingerprintSha256 : null);
                if (sealedDigest != op.MetadataDigestSha256)
                    return new(SigningPersistenceStatus.Conflict);
            }
            op.State = (short)target;
            op.SafeErrorCode = ErrorCode(failure);
            op.Revision++;
            op.UpdatedAtUtc = now < op.UpdatedAtUtc ? op.UpdatedAtUtc : now;
            SigningOperationAudit.Stage(db, op, actor, "operation_advanced", op.UpdatedAtUtc);
            return new(SigningPersistenceStatus.Updated, await ViewAsync(op, ct));
        }, ct);
    }

    private static string RequestDigest(SigningScope scope, SigningOperationKind kind,
        string profile, string? publicFingerprint)
    {
        var metadata = FormattableString.Invariant(
            $"{scope.CustomerUserId:D}|{scope.TaxpayerId:D}|{scope.FiscalMemoryId:D}|{(short)kind}|{profile}|{SigningOperationPolicy.ConsentVersion}|{publicFingerprint ?? "-"}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadata))).ToLowerInvariant();
    }

    private static string? ErrorCode(SigningFailure failure) => failure switch
    {
        SigningFailure.None => null,
        SigningFailure.ProviderUnavailable => "PROVIDER_UNAVAILABLE",
        SigningFailure.ProviderOutcomeUnknown => "PROVIDER_OUTCOME_UNKNOWN",
        SigningFailure.InvalidPublicMaterial => "INVALID_PUBLIC_MATERIAL",
        SigningFailure.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };

    private async Task<SigningPersistenceResult> WriteAsync(string? bearer, SigningScope scope,
        Guid operationId, Func<Guid, DateTime, Task<SigningPersistenceResult>> action, CancellationToken ct)
    {
        // This boundary owns its transaction; never discard another use case's staged writes.
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Signing repository requires a clean scoped context.");
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (await identity.CurrentUserIdAsync(bearer, ct) is null)
                    return new SigningPersistenceResult(SigningPersistenceStatus.Unauthenticated);
                var actor = await AuthorizeAsync(bearer, scope, true, ct);
                if (actor is null) return new(SigningPersistenceStatus.Forbidden);

                // Serialize claims per memory without process-local locking.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM fiscal_memories WHERE id = {scope.FiscalMemoryId} FOR UPDATE", ct);
                var result = await action(actor.Value, DateTime.UtcNow);
                if (result.Status is SigningPersistenceStatus.Created or SigningPersistenceStatus.Updated)
                {
                    if (await AuthorizeAsync(bearer, scope, true, ct) != actor)
                        return new SigningPersistenceResult(SigningPersistenceStatus.Forbidden);
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct); // Deferred audit/consent guards execute before success.
                return result;
            });
        }
        catch (DbUpdateConcurrencyException) { return new(SigningPersistenceStatus.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        { return new(SigningPersistenceStatus.Conflict); }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure)
        { return new(SigningPersistenceStatus.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        { return new(SigningPersistenceStatus.IntegrityFailure); }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.CheckViolation or PostgresErrorCodes.ForeignKeyViolation)
        { return new(SigningPersistenceStatus.IntegrityFailure); }
        finally { db.ChangeTracker.Clear(); }
    }
}
