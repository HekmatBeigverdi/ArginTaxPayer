using ArginTaxPayer.Infrastructure.Signing.Protection;

namespace ArginTaxPayer.Infrastructure.Signing.Recovery;

internal sealed class SigningKeyRecoveryService(ProtectedKeyEnvelopeStore store) : ISigningKeyRecoveryService
{
    public async Task<SigningRecoveryResult> RewrapAsync(IReadOnlyList<SigningRecoveryItem> items,
        SigningRecoveryCheckpoint checkpoint, CancellationToken ct = default)
    {
        if (items is null || checkpoint is null || checkpoint.NextIndex < 0 || checkpoint.Completed < 0 ||
            checkpoint.NextIndex > items.Count || checkpoint.Completed > checkpoint.NextIndex)
            return new(SigningRecoveryStatus.Invalid, checkpoint ?? new(0, 0, null));
        var completed = checkpoint.Completed;
        for (var index = checkpoint.NextIndex; index < items.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var item = items[index];
            try { await store.RewrapAsync(item.Identity, item.ExpectedEnvelopeVersion, ct); }
            catch (KeyProtectionException error) when (error.Failure == KeyProtectionFailure.Unavailable)
            { return new(SigningRecoveryStatus.MissingKeyProtection, new(index, completed, item.Identity.StoragePath())); }
            catch (KeyProtectionException error) when (error.Failure == KeyProtectionFailure.Conflict)
            { return new(SigningRecoveryStatus.Conflict, new(index, completed, item.Identity.StoragePath())); }
            completed++;
        }
        return new(SigningRecoveryStatus.Completed, new(items.Count, completed,
            items.Count == 0 ? checkpoint.LastPath : items[^1].Identity.StoragePath()));
    }
}
