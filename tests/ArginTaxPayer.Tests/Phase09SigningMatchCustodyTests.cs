using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Matching;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningMatchCustodyTests
{
    [Fact]
    public async Task Protected_proof_is_read_only_and_bound_to_complete_identity()
    {
        using var rsa = RSA.Create(2048);
        using var provider = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var protector = new KeyEnvelopeProtector(provider);
        var id = Phase09SigningKeyProtectionTests.Identity();
        using var clear = SensitiveBytes.TakeOwnership(rsa.ExportPkcs8PrivateKey());
        var envelope = await protector.ProtectAsync(id, clear.Bytes);
        var store = new ReadOnlyStore(new(envelope, 1));
        var spki = rsa.ExportSubjectPublicKeyInfo();
        var fp = RsaSigningKeyMaterial.Fingerprint(spki);
        var ticket = new KeyMatchTicket(Guid.NewGuid(), id, 3, spki, fp, fp);
        var proof = new VaultKeyMatchProof(store, protector);
        Assert.True(await proof.ProveAsync(ticket, spki, default));
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() => proof.ProveAsync(
            ticket with { Identity = id with { CustomerUserId = Guid.NewGuid() } }, spki, default));
        Assert.Equal(KeyProtectionFailure.Integrity, error.Failure);
    }

    [Fact]
    public async Task Missing_envelope_is_recovery_and_never_creates_replacement()
    {
        using var rsa = RSA.Create(2048);
        using var provider = new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var spki = rsa.ExportSubjectPublicKeyInfo();
        var ticket = new KeyMatchTicket(Guid.NewGuid(), Phase09SigningKeyProtectionTests.Identity(), 3,
            spki, RsaSigningKeyMaterial.Fingerprint(spki), RsaSigningKeyMaterial.Fingerprint(spki));
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            new VaultKeyMatchProof(new ReadOnlyStore(null), new(provider)).ProveAsync(ticket, spki, default));
        Assert.Equal(KeyProtectionFailure.RecoveryRequired, error.Failure);
    }

    private sealed class ReadOnlyStore(StoredKeyEnvelope? stored) : IProtectedKeyEnvelopeStore
    {
        public Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity id, CancellationToken ct) => Task.FromResult(stored);
        public Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope e, CancellationToken ct) =>
            throw new InvalidOperationException("Comparison must never write custody.");
        public Task<StoredKeyEnvelope> ReplaceAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope e, int version, CancellationToken ct) =>
            throw new InvalidOperationException("Comparison must never replace custody.");
    }
}
