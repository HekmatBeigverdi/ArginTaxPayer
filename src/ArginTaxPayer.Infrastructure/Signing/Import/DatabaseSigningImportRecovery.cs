using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

/// <summary>Recover a previously saved encrypted import without retransmitting key material.</summary>
internal sealed class DatabaseSigningImportRecovery(
    ArginTaxPayerDbContext db, EfSigningOperationRepository journal,
    IProtectedKeyEnvelopeStore envelopes, KeyEnvelopeProtector protector,
    ISigningImportJournal importJournal) : ISigningImportRecovery
{
    public async Task<SigningImportRecoveryResult> RecoverAsync(string? bearer, Guid taxpayerId,
        Guid fiscalMemoryId, CancellationToken ct = default)
    {
        var state = await journal.ReadImportOnboardingAsync(bearer, taxpayerId, fiscalMemoryId, ct);
        if (state.Status != SigningGenerationStatus.Replayed) return new(state.Status, "authorization");
        if (!state.FreshLogin) return new(SigningGenerationStatus.Forbidden, "fresh_login");
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

        if (candidate is null) return new(SigningGenerationStatus.NotFound, "pending_record");
        var keyIdentity = new KeyProtectionIdentity(owner, taxpayerId, fiscalMemoryId,
            candidate.KeyVersionId, candidate.Id);
        // Expose only which check failed, never the envelope, PKCS8, token or exception text.
        StoredKeyEnvelope? stored;
        try { stored = await envelopes.FindAsync(keyIdentity, ct); }
        catch (KeyProtectionException) { return new(SigningGenerationStatus.IntegrityFailure, "envelope_format"); }
        if (stored is null) return new(SigningGenerationStatus.RecoveryRequired, "envelope_missing");

        SensitiveBytes privateKey;
        try { privateKey = await protector.UnprotectAsync(keyIdentity, stored.Envelope, ct); }
        catch (KeyProtectionException ex)
        {
            return new(ex.Failure == KeyProtectionFailure.Unavailable
                ? SigningGenerationStatus.Unavailable : SigningGenerationStatus.IntegrityFailure, "decrypt");
        }
        using (privateKey)
        {
            byte[] publicDer;
            try { publicDer = RsaSigningKeyMaterial.DerivePublic(privateKey.Bytes); }
            catch (KeyProtectionException) { return new(SigningGenerationStatus.IntegrityFailure, "pkcs8"); }
            try
            {
                var receipt = new SealedGeneratedKey(keyIdentity, publicDer,
                    RsaSigningKeyMaterial.Fingerprint(publicDer), stored.Envelope.ProtectionKeyVersion);
                if (receipt.ProtectionKeyVersion != DatabaseKeyProtectionProvider.KeyVersion)
                    return new(SigningGenerationStatus.IntegrityFailure, "provider_version");

                var done = await importJournal.CompleteImportAsync(bearer, candidate.OperationId, receipt, ct);
                return new(done.Status, done.Status is SigningGenerationStatus.Created or
                    SigningGenerationStatus.Replayed ? "complete" : "journal");
            }
            catch (KeyProtectionException)
            { return new(SigningGenerationStatus.IntegrityFailure, "journal"); }
            finally { CryptographicOperations.ZeroMemory(publicDer); }
        }
    }
}
