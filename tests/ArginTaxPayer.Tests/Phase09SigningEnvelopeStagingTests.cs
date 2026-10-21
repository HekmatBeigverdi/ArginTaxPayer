using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningEnvelopeStagingTests
{
    private sealed class MemoryStore : IProtectedKeyEnvelopeStore
    {
        public StoredKeyEnvelope? Value;
        public bool LoseWriteAcknowledgement;
        public int Writes;
        public Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity id, CancellationToken ct) =>
            Task.FromResult(Value);
        public Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope e, CancellationToken ct)
        {
            if (Value is not null) throw new KeyProtectionException(KeyProtectionFailure.Conflict);
            Value = new(e, 1);
            Writes++;
            if (LoseWriteAcknowledgement)
                throw new KeyProtectionException(KeyProtectionFailure.RecoveryRequired);
            return Task.FromResult(Value!);
        }
        public Task<StoredKeyEnvelope> ReplaceAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope e,
            int expectedVersion, CancellationToken ct)
        {
            if (Value?.Version != expectedVersion) throw new KeyProtectionException(KeyProtectionFailure.Conflict);
            Value = new(e, expectedVersion + 1);
            Writes++;
            return Task.FromResult(Value!);
        }
    }

    [Fact]
    public async Task Same_identity_and_same_material_replays_without_second_encryption()
    {
        using var provider = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var store = new MemoryStore();
        var service = new ProtectedKeyEnvelopeStore(new(provider), store);
        var id = Phase09SigningKeyProtectionTests.Identity();
        var first = await service.StageAsync(id, new byte[] { 1, 2, 3 });
        var replay = await service.StageAsync(id, new byte[] { 1, 2, 3 });
        Assert.Equal(first, replay);
        Assert.Single(provider.Keys);
        Assert.Equal(1, store.Writes);
        var conflict = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            service.StageAsync(id, new byte[] { 4, 5, 6 }));
        Assert.Equal(KeyProtectionFailure.Conflict, conflict.Failure);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task Uncertain_write_is_recovered_by_identity_without_writing_again()
    {
        using var provider = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var store = new MemoryStore { LoseWriteAcknowledgement = true };
        var service = new ProtectedKeyEnvelopeStore(new(provider), store);
        var id = Phase09SigningKeyProtectionTests.Identity();
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() => service.StageAsync(id, new byte[] { 1 }));
        Assert.Equal(KeyProtectionFailure.RecoveryRequired, error.Failure);
        Assert.NotNull(await service.RecoverAsync(id));
        Assert.Equal(1, store.Writes);
        Assert.Single(provider.Keys);
    }

    [Fact]
    public async Task Rewrap_preserves_ciphertext_and_rejects_stale_storage_version()
    {
        using var provider = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var store = new MemoryStore();
        var service = new ProtectedKeyEnvelopeStore(new(provider), store);
        var id = Phase09SigningKeyProtectionTests.Identity();
        var first = await service.StageAsync(id, new byte[] { 1 });
        var next = await service.RewrapAsync(id, 1);
        Assert.Equal(2, next.Version);
        Assert.Equal(first.Envelope.Ciphertext, next.Envelope.Ciphertext);
        Assert.Equal(first.Envelope.Nonce, next.Envelope.Nonce);
        Assert.Single(provider.Keys);
        await Assert.ThrowsAsync<KeyProtectionException>(() => service.RewrapAsync(id, 1));
        Assert.Equal(2, store.Writes);
    }
}
