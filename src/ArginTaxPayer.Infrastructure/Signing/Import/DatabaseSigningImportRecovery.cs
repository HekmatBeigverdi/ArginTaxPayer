using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

/// <summary>Recover a previously saved encrypted import without retransmitting key material.</summary>
internal sealed class DatabaseSigningImportRecovery(
    ArginTaxPayerDbContext db, EfSigningOperationRepository journal,
    IGeneratedKeyCustody custody, ISigningImportJournal importJournal) : ISigningImportRecovery
{
    public async Task<SigningGenerationStatus> RecoverAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, CancellationToken ct = default)
    {
        var state = await journal.ReadImportOnboardingAsync(bearer, taxpayerId, fiscalMemoryId, ct);
        if (state.Status != SigningGenerationStatus.Replayed) return state.Status;
        if (!state.FreshLogin) return SigningGenerationStatus.Forbidden;
        var ownerId = await db.TaxpayerProfiles.AsNoTracking()
            .Where(p => p.Id == taxpayerId).Select(p => p.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if (ownerId is null || ownerId == Guid.Empty) return SigningGenerationStatus.NotFound;
        var owner = ownerId.Value;

        var candidate = await (
            from op in db.SigningOperations.AsNoTracking()
            join key in db.SigningKeyVersions.AsNoTracking() on op.Id equals key.OriginOperationRecordId
            join envelope in db.DbSigningKeyEnvelopes.AsNoTracking() on key.Id equals envelope.KeyVersionId
            where op.CustomerUserId == owner && op.ActorUserId == owner &&
                op.TaxpayerId == taxpayerId && op.FiscalMemoryId == fiscalMemoryId &&
                op.OperationKind == 2 && op.State == 5 &&
                op.SafeErrorCode == "PROVIDER_OUTCOME_UNKNOWN" &&
                key.CustomerUserId == owner && key.TaxpayerId == taxpayerId &&
                key.FiscalMemoryId == fiscalMemoryId && key.LifecycleState == 1 &&
                key.PublicSpkiDer == null && key.ProtectionKeyVersion == null &&
                envelope.CustomerUserId == owner && envelope.TaxpayerId == taxpayerId &&
                envelope.FiscalMemoryId == fiscalMemoryId &&
                envelope.OriginOperationRecordId == op.Id
            select new { op.OperationId, op.Id, KeyVersionId = key.Id }).SingleOrDefaultAsync(ct);

        if (candidate is null) return SigningGenerationStatus.NotFound;
        var keyIdentity = new KeyProtectionIdentity(owner, taxpayerId, fiscalMemoryId,
            candidate.KeyVersionId, candidate.Id);
        try
        {
            await custody.EnsureReadyAsync(ct);
            var receipt = await custody.RecoverAsync(keyIdentity, ct);
            if (receipt is null) return SigningGenerationStatus.RecoveryRequired;
            if (receipt.ProtectionKeyVersion != DatabaseKeyProtectionProvider.KeyVersion)
                return SigningGenerationStatus.IntegrityFailure;
            // Journal checks original consent, actor, operation digest, lifecycle and login.
            var done = await importJournal.CompleteImportAsync(bearer, candidate.OperationId, receipt, ct);
            return done.Status;
        }
        catch (KeyProtectionException ex)
        {
            return ex.Failure == KeyProtectionFailure.Unavailable
                ? SigningGenerationStatus.Unavailable : SigningGenerationStatus.RecoveryRequired;
        }
    }
}
