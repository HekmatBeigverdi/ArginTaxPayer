using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09VaultFactAttribute : FactAttribute
{
    public Phase09VaultFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_VAULT_INTEGRATION") != "1")
            Skip = "Explicit opt-in: isolated test Vault with trusted TLS and workload Agent token.";
    }
}

public sealed class Phase09SigningVaultIntegrationTests
{
    [Phase09VaultFact]
    public async Task Synthetic_key_is_sealed_recovered_rewrapped_and_cas_protected_in_test_vault()
    {
        // Fixed test mounts and loopback endpoint prevent accidental use of business custody paths.
        var address = Environment.GetEnvironmentVariable("PHASE09_VAULT_ADDRESS");
        var tokenFile = Environment.GetEnvironmentVariable("PHASE09_VAULT_TOKEN_FILE") ?? "";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != "https" || !endpoint.IsLoopback || endpoint.Port != 18200)
            throw new InvalidOperationException("An isolated loopback TLS test Vault on port18200 is required.");
        var options = new VaultProtectionOptions(endpoint, "argin-transit-test", "signing-test-kek",
            "argin-signing-test", tokenFile, VaultProtectionOptions.ReviewedServerVersion);
        options.Validate();
        using var transport = new VaultHttpTransport(options, new VaultAgentTokenSource(tokenFile));
        var provider = new VaultKeyProtectionProvider(transport, options);
        var kv = new VaultProtectedKeyEnvelopeStore(transport, options);
        var protector = new KeyEnvelopeProtector(provider);
        var staging = new ProtectedKeyEnvelopeStore(protector, kv);
        var id = Phase09SigningKeyProtectionTests.Identity();
        using var rsa = RSA.Create(2048); // Synthetic-only fixture; never a taxpayer key.
        var privateBytes = rsa.ExportPkcs8PrivateKey();
        try
        {
            var first = await staging.StageAsync(id, privateBytes);
            Assert.Equal(1, first.Version);
            var replay = await staging.StageAsync(id, privateBytes);
            Assert.Equal(1, replay.Version);
            using var clear = await protector.UnprotectAsync(id, replay.Envelope);
            Assert.True(CryptographicOperations.FixedTimeEquals(privateBytes, clear.Bytes.Span));
            var recovered = await staging.RecoverAsync(id);
            Assert.Equal(1, recovered!.Version);
            var rewrapped = await staging.RewrapAsync(id, 1);
            Assert.Equal(2, rewrapped.Version);
            Assert.Equal(first.Envelope.Ciphertext, rewrapped.Envelope.Ciphertext);
            var conflict = await Assert.ThrowsAsync<KeyProtectionException>(() =>
                kv.CreateAsync(id, first.Envelope, default));
            Assert.Equal(KeyProtectionFailure.Conflict, conflict.Failure);
        }
        finally { CryptographicOperations.ZeroMemory(privateBytes); }
        // Keep encrypted synthetic fixtures/history; runtime identity has no delete/destroy permission.
    }
}
