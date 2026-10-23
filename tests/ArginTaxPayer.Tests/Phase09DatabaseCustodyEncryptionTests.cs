using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09DatabaseCustodyEncryptionTests
{
    private static IConfiguration Config(byte[] secret) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SigningKeyProtection:DatabaseMasterKey"] = Convert.ToBase64String(secret)
        }).Build();

    private static KeyProtectionIdentity Identity() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Database_wrapping_roundtrip_binds_entire_identity()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var provider = new DatabaseKeyProtectionProvider(Config(secret));
            var identity = Identity();
            var dek = RandomNumberGenerator.GetBytes(32);
            try
            {
                var wrapped = await provider.WrapAsync(identity, dek, default);
                Assert.Equal("db:v1", wrapped.ProtectionKeyVersion);
                Assert.DoesNotContain(Convert.ToBase64String(dek), wrapped.Ciphertext);
                using var recovered = await provider.UnwrapAsync(identity, wrapped, default);
                Assert.Equal(dek, recovered.Bytes.ToArray());
                var other = identity with { TaxpayerId = Guid.NewGuid() };
                await Assert.ThrowsAsync<KeyProtectionException>(
                    () => provider.UnwrapAsync(other, wrapped, default));
            }
            finally { CryptographicOperations.ZeroMemory(dek); }
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    [Fact]
    public async Task Different_master_key_cannot_decrypt_existing_ciphertext()
    {
        var first = RandomNumberGenerator.GetBytes(32);
        var second = RandomNumberGenerator.GetBytes(32);
        try
        {
            using var writer = new DatabaseKeyProtectionProvider(Config(first));
            using var reader = new DatabaseKeyProtectionProvider(Config(second));
            var identity = Identity();
            var dek = RandomNumberGenerator.GetBytes(32);
            try
            {
                var wrapped = await writer.WrapAsync(identity, dek, default);
                await Assert.ThrowsAsync<KeyProtectionException>(
                    () => reader.UnwrapAsync(identity, wrapped, default));
            }
            finally { CryptographicOperations.ZeroMemory(dek); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
        }
    }

    [Fact]
    public void Missing_or_weak_master_key_fails_closed()
    {
        Assert.Throws<KeyProtectionException>(() => new DatabaseKeyProtectionProvider(
            new ConfigurationBuilder().Build()));
        Assert.Throws<KeyProtectionException>(() => new DatabaseKeyProtectionProvider(Config(new byte[16])));
    }
}
