using ArginTaxPayer.Application.Signing;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed record SigningImportOnboardingState(SigningGenerationStatus Status,
    bool FreshLogin = false, Guid? OperationId = null, bool HasKey = false,
    string? FingerprintSha256 = null, short? LifecycleState = null,
    bool RecoveryAvailable = false, bool ReplacementApproved = false);

public sealed partial class EfSigningOperationRepository
{
    // Change requests need owner authorization, but do not depend on the import onboarding workflow.
    public async Task<Guid?> AuthorizeOwnerAsync(string? bearer, Guid taxpayerId,
        Guid memoryId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return null;
        var scope = new SigningScope(actor.Value, taxpayerId, memoryId);
        return await AuthorizeAsync(bearer, scope, true, ct) is not null ? actor : null;
    }

    // Owner-only, including for administrators: commercial scope is not custody permission.
    public async Task<SigningImportOnboardingState> ReadImportOnboardingAsync(string? bearer,
        Guid taxpayerId, Guid memoryId, CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct);
        if (actor is null) return new(SigningGenerationStatus.Unauthenticated);
        var scope = new SigningScope(actor.Value, taxpayerId, memoryId);
        if (await AuthorizeAsync(bearer, scope, true, ct) is null)
            return new(SigningGenerationStatus.Forbidden);
        var fresh = await RecentLoginAsync(bearer, actor.Value, DateTime.UtcNow, ct) is not null;
        var key = await db.SigningKeyVersions.AsNoTracking()
            .Where(k => k.CustomerUserId == actor.Value && k.TaxpayerId == taxpayerId && k.FiscalMemoryId == memoryId)
            .OrderByDescending(k => k.VersionNumber).FirstOrDefaultAsync(ct);
        // Recover the original import after a refresh/timeout; never allocate a replacement.
        var originId = key?.OriginOperationRecordId;
        var operation = await db.SigningOperations.AsNoTracking()
            .Where(o => o.CustomerUserId == actor.Value && o.TaxpayerId == taxpayerId &&
                o.FiscalMemoryId == memoryId && o.OperationKind == 2 &&
                (originId == null || o.Id == originId))
            .OrderByDescending(o => o.CreatedAtUtc).FirstOrDefaultAsync(ct);
        var recoveryAvailable = key is not null && key.LifecycleState == 1 &&
            operation is { State: 5, OperationKind: 2, SafeErrorCode: "PROVIDER_OUTCOME_UNKNOWN" } &&
            await db.DbSigningKeyEnvelopes.AsNoTracking().AnyAsync(e =>
                e.KeyVersionId == key.Id && e.CustomerUserId == actor.Value &&
                e.TaxpayerId == taxpayerId && e.FiscalMemoryId == memoryId &&
                e.OriginOperationRecordId == operation.Id, ct);
        var replacementApproved = key is { LifecycleState: 2 or 4 } &&
            await db.SigningChangeRequests.AsNoTracking().AnyAsync(r =>
                r.CustomerUserId == actor.Value && r.TaxpayerId == taxpayerId &&
                r.FiscalMemoryId == memoryId && r.Kind == 3 && r.State == 2 &&
                r.CurrentKeyVersionId == key.Id && r.ExpectedKeyRevision == key.Revision &&
                r.ExpiresAtUtc > DateTime.UtcNow &&
                r.ConsumedAtUtc == null && r.ConsumedByOperationRecordId == null, ct);
        return new(SigningGenerationStatus.Replayed, fresh, operation?.OperationId,
            key is not null, key?.PublicFingerprintSha256, key?.LifecycleState,
            recoveryAvailable, replacementApproved);
    }
}
