using ArginTaxPayer.Application.Signing;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed record SigningImportOnboardingState(SigningGenerationStatus Status,
    bool FreshLogin = false, Guid? OperationId = null, bool HasKey = false,
    string? FingerprintSha256 = null, short? LifecycleState = null,
    bool RecoveryAvailable = false, bool ReplacementApproved = false,
    Guid? GenerationOperationId = null);

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
            .Where(k => k.LifecycleState == 2 || k.LifecycleState == 4)
            .OrderByDescending(k => k.VersionNumber).FirstOrDefaultAsync(ct);
        // Recover a claimed operation after a refresh/timeout; never allocate a replacement.
        var pendingKey = await db.SigningKeyVersions.AsNoTracking()
            .Where(k => k.CustomerUserId == actor.Value && k.TaxpayerId == taxpayerId &&
                k.FiscalMemoryId == memoryId && k.LifecycleState == 1)
            .OrderByDescending(k => k.VersionNumber).FirstOrDefaultAsync(ct);
        var originId = pendingKey?.OriginOperationRecordId;
        var operation = pendingKey is null ? null : await db.SigningOperations.AsNoTracking()
            .Where(o => o.CustomerUserId == actor.Value && o.TaxpayerId == taxpayerId &&
                o.FiscalMemoryId == memoryId && o.Id == originId)
            .OrderByDescending(o => o.CreatedAtUtc).FirstOrDefaultAsync(ct);
        var recoveryAvailable = pendingKey is not null &&
            operation is { State: 5, OperationKind: 2, SafeErrorCode: "PROVIDER_OUTCOME_UNKNOWN" } &&
            await db.DbSigningKeyEnvelopes.AsNoTracking().AnyAsync(e =>
                e.KeyVersionId == pendingKey.Id && e.CustomerUserId == actor.Value &&
                e.TaxpayerId == taxpayerId && e.FiscalMemoryId == memoryId &&
                e.OriginOperationRecordId == operation.Id, ct);
        var replacementApproved = key is { LifecycleState: 2 or 4 } &&
            await db.SigningChangeRequests.AsNoTracking().AnyAsync(r =>
                r.CustomerUserId == actor.Value && r.TaxpayerId == taxpayerId &&
                r.FiscalMemoryId == memoryId && (r.Kind == 2 || r.Kind == 3) && r.State == 2 &&
                r.CurrentKeyVersionId == key.Id && r.ExpectedKeyRevision == key.Revision &&
                r.ExpiresAtUtc > DateTime.UtcNow &&
                r.ConsumedAtUtc == null && r.ConsumedByOperationRecordId == null, ct);
        return new(SigningGenerationStatus.Replayed, fresh, operation?.OperationKind == 2 ? operation.OperationId : null,
            key is not null, key?.PublicFingerprintSha256, key?.LifecycleState,
            recoveryAvailable, replacementApproved,
            operation is { OperationKind: 1, State: 5 } ? operation.OperationId : null);
    }
}
