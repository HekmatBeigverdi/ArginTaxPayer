using ArginTaxPayer.Application.Signing;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed record SigningImportOnboardingState(SigningGenerationStatus Status,
    bool FreshLogin = false, Guid? OperationId = null, bool HasKey = false,
    string? FingerprintSha256 = null, short? LifecycleState = null);

public sealed partial class EfSigningOperationRepository
{
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
        return new(SigningGenerationStatus.Replayed, fresh, operation?.OperationId,
            key is not null, key?.PublicFingerprintSha256, key?.LifecycleState);
    }
}
