using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningKeyProtectionTests
{
    internal static KeyProtectionIdentity Identity() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Envelope_roundtrip_uses_fresh_deks_and_nonces_and_redacts_output()
    {
        using var provider = new SyntheticProtectionProvider();
        var protector = new KeyEnvelopeProtector(provider);
        var id = Identity();
        var input = Encoding.UTF8.GetBytes("synthetic-private-material");
        var one = await protector.ProtectAsync(id, input);
        var two = await protector.ProtectAsync(id, input);
        Assert.Equal(12, one.Nonce.Length);
        Assert.Equal(16, one.Tag.Length);
        Assert.False(one.Nonce.SequenceEqual(two.Nonce));
        Assert.False(one.Ciphertext.SequenceEqual(two.Ciphertext));
        Assert.Equal(2, provider.Keys.Count);
        Assert.False(provider.Keys[0].SequenceEqual(provider.Keys[1]));
        using var clear = await protector.UnprotectAsync(id, one);
        Assert.Equal(input, clear.Bytes.ToArray());
        Assert.DoesNotContain("synthetic-private-material", clear.ToString());
        Assert.DoesNotContain(one.WrappedDek, one.ToString());
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("taxpayer")]
    [InlineData("memory")]
    [InlineData("key")]
    [InlineData("operation")]
    public async Task Different_identity_cannot_unseal_envelope(string field)
    {
        using var provider = new SyntheticProtectionProvider();
        var protector = new KeyEnvelopeProtector(provider);
        var id = Identity();
        var envelope = await protector.ProtectAsync(id, new byte[] { 1, 2, 3 });
        var other = field switch
        {
            "customer" => id with { CustomerUserId = Guid.NewGuid() },
            "taxpayer" => id with { TaxpayerId = Guid.NewGuid() },
            "memory" => id with { FiscalMemoryId = Guid.NewGuid() },
            "key" => id with { KeyVersionId = Guid.NewGuid() },
            _ => id with { OperationRecordId = Guid.NewGuid() }
        };
        await Assert.ThrowsAsync<KeyProtectionException>(() => protector.UnprotectAsync(other, envelope));
        // A malicious store can rewrite metadata; Transit/GCM must still authenticate original identity.
        await Assert.ThrowsAsync<KeyProtectionException>(() =>
            protector.UnprotectAsync(other, envelope with { Identity = other }));
    }

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("algorithm")]
    [InlineData("format")]
    [InlineData("wrapped")]
    public async Task Tampering_never_returns_private_material(string field)
    {
        using var provider = new SyntheticProtectionProvider();
        var protector = new KeyEnvelopeProtector(provider);
        var id = Identity();
        var e = await protector.ProtectAsync(id, new byte[] { 1, 2, 3 });
        var changed = field switch
        {
            "ciphertext" => e with { Ciphertext = [9, 2, 3] },
            "nonce" => e with { Nonce = new byte[12] },
            "tag" => e with { Tag = new byte[16] },
            "algorithm" => e with { AlgorithmId = "unknown" },
            "format" => e with { FormatVersion = 2 },
            _ => e with { WrappedDek = "unknown" }
        };
        await Assert.ThrowsAsync<KeyProtectionException>(() => protector.UnprotectAsync(id, changed));
    }

    [Fact]
    public async Task Missing_kek_and_disabled_provider_fail_closed()
    {
        var protector = new KeyEnvelopeProtector(new UnavailableKeyProtectionProvider());
        var error = await Assert.ThrowsAsync<KeyProtectionException>(() =>
            protector.ProtectAsync(Identity(), new byte[] { 1 }));
        Assert.Equal(KeyProtectionFailure.Unavailable, error.Failure);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void Buffer_disposal_clears_owned_storage_and_blocks_reuse()
    {
        var owned = new byte[] { 1, 2, 3 };
        var buffer = SensitiveBytes.TakeOwnership(owned);
        buffer.Dispose();
        Assert.Equal(new byte[3], owned);
        Assert.Throws<ObjectDisposedException>(() => buffer.Bytes.ToArray());
        buffer.Dispose();
    }

    [Fact]
    public void Aad_is_exact_canonical_ascii_and_locator_uses_only_server_ids()
    {
        var id = new KeyProtectionIdentity(
            Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"),
            Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"),
            Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC"),
            Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD"),
            Guid.Parse("EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE"));
        Assert.Equal("argin:key:v1|aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa|bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb|cccccccc-cccc-cccc-cccc-cccccccccccc|dddddddd-dddd-dddd-dddd-dddddddddddd",
            Encoding.ASCII.GetString(id.Aad()));
        Assert.DoesNotContain("..", id.StoragePath());
        Assert.Throws<KeyProtectionException>(() => default(KeyProtectionIdentity).Validate());
    }

    [Fact]
    public async Task Invalid_input_is_rejected_before_wrapping()
    {
        using var provider = new SyntheticProtectionProvider();
        var protector = new KeyEnvelopeProtector(provider);
        await Assert.ThrowsAsync<KeyProtectionException>(() => protector.ProtectAsync(Identity(), Array.Empty<byte>()));
        await Assert.ThrowsAsync<KeyProtectionException>(() => protector.ProtectAsync(Identity(), new byte[16385]));
        Assert.Empty(provider.Keys);
    }

    [Fact]
    public async Task Rewrap_output_must_authenticate_the_original_encrypted_key()
    {
        using var provider = new SyntheticProtectionProvider { CorruptRewrap = true };
        var protector = new KeyEnvelopeProtector(provider);
        var id = Identity();
        var envelope = await protector.ProtectAsync(id, new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<KeyProtectionException>(() => protector.RewrapAsync(id, envelope));
    }

    // Test assembly only: never registered by production code.
    internal sealed class SyntheticProtectionProvider : IKeyProtectionProvider, IDisposable
    {
        private readonly Dictionary<string, (byte[] Key, byte[] Binding)> values = new();
        public List<byte[]> Keys { get; } = new();
        public bool CorruptRewrap { get; set; }
        public Task<WrappedDataKey> WrapAsync(KeyProtectionIdentity id, ReadOnlyMemory<byte> dek, CancellationToken ct)
        {
            var key = dek.ToArray();
            Keys.Add(key);
            var reference = "fixture:" + Guid.NewGuid().ToString("N");
            values[reference] = (key, id.TransitBinding());
            return Task.FromResult(new WrappedDataKey(reference, "vault:v1"));
        }
        public Task<SensitiveBytes> UnwrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct)
        {
            if (!values.TryGetValue(wrapped.Ciphertext, out var stored) ||
                !stored.Binding.SequenceEqual(id.TransitBinding()))
                throw new KeyProtectionException(KeyProtectionFailure.Integrity);
            return Task.FromResult(SensitiveBytes.TakeOwnership(stored.Key.ToArray()));
        }
        public Task<WrappedDataKey> RewrapAsync(KeyProtectionIdentity id, WrappedDataKey wrapped, CancellationToken ct) =>
            CorruptRewrap ? WrapAsync(id, new byte[32], ct) : Task.FromResult(wrapped);
        public void Dispose()
        {
            foreach (var key in Keys) CryptographicOperations.ZeroMemory(key);
        }
    }
}
