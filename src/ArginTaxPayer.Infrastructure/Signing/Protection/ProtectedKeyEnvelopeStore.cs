using System.Security.Cryptography;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

// Internal staging/reconciliation only. The higher use case must reserve/advance the Step04 journal
// before calling this service and commit metadata/audit before allowing signing. No DB transaction here.
internal sealed class ProtectedKeyEnvelopeStore(KeyEnvelopeProtector protector, IProtectedKeyEnvelopeStore store)
{
    public async Task<StoredKeyEnvelope> StageAsync(KeyProtectionIdentity id, ReadOnlyMemory<byte> pkcs8,
        CancellationToken ct = default)
    {
        id.Validate();
        if (pkcs8.Length is < 1 or > KeyEnvelopeProtector.MaximumKeyBytes)
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        var previous = await store.FindAsync(id, ct);
        if (previous is not null) return await MatchAsync(id, previous, pkcs8, ct);
        var envelope = await protector.ProtectAsync(id, pkcs8, ct);
        try { return await store.CreateAsync(id, envelope, ct); }
        catch (KeyProtectionException error) when (error.Failure == KeyProtectionFailure.Conflict)
        {
            // A concurrent create may have won. Never overwrite its envelope.
            var winner = await store.FindAsync(id, ct);
            if (winner is null) throw new KeyProtectionException(KeyProtectionFailure.RecoveryRequired);
            return await MatchAsync(id, winner, pkcs8, ct);
        }
        // Unknown outcomes deliberately propagate. RecoverAsync is a separate, read-only operation.
    }

    private async Task<StoredKeyEnvelope> MatchAsync(KeyProtectionIdentity id, StoredKeyEnvelope stored,
        ReadOnlyMemory<byte> expected, CancellationToken ct)
    {
        using var clear = await protector.UnprotectAsync(id, stored.Envelope, ct);
        if (!CryptographicOperations.FixedTimeEquals(clear.Bytes.Span, expected.Span))
            throw new KeyProtectionException(KeyProtectionFailure.Conflict);
        return stored;
    }

    public async Task<StoredKeyEnvelope?> RecoverAsync(KeyProtectionIdentity id, CancellationToken ct = default)
    {
        id.Validate();
        var stored = await store.FindAsync(id, ct);
        if (stored is null) return null; // Absence does NOT authorize another key generation.
        using var clear = await protector.UnprotectAsync(id, stored.Envelope, ct);
        return stored;
    }

    public async Task<StoredKeyEnvelope> RewrapAsync(KeyProtectionIdentity id, int expectedVersion,
        CancellationToken ct = default)
    {
        if (expectedVersion < 1 || expectedVersion == int.MaxValue)
            throw new KeyProtectionException(KeyProtectionFailure.InvalidInput);
        var stored = await store.FindAsync(id, ct);
        if (stored is null || stored.Version != expectedVersion)
            throw new KeyProtectionException(KeyProtectionFailure.Conflict);
        var next = await protector.RewrapAsync(id, stored.Envelope, ct);
        return await store.ReplaceAsync(id, next, expectedVersion, ct);
    }
}
