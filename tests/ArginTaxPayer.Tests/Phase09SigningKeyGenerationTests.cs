using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningKeyGenerationTests
{
    internal static GenerateSigningKeyRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true);

    [Fact]
    public void Generated_key_has_fixed_strength_and_matching_canonical_public_artifact()
    {
        using var key = new RsaGeneratedKeyFactory().Create();
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(key.PrivateMaterial.Bytes.Span, out var consumed);
        Assert.Equal(key.PrivateMaterial.Bytes.Length, consumed);
        Assert.Equal(2048, rsa.KeySize);
        Assert.Equal(new byte[] { 1, 0, 1 }, rsa.ExportParameters(false).Exponent);
        var artifact = RsaSigningKeyMaterial.Artifact(Guid.NewGuid(), key.PublicSpkiDer, 1, 2);
        Assert.Equal(key.PublicFingerprintSha256, artifact.FingerprintSha256);
        Assert.False(artifact.PortalUploadApproved);
        Assert.Equal("Unverified", artifact.PortalVerification);
        Assert.Contains("-----BEGIN PUBLIC KEY-----\n", artifact.Pem);
        Assert.DoesNotContain("\r", artifact.Pem);
        Assert.DoesNotContain("PRIVATE", artifact.Pem);
        using var publicOnly = RSA.Create();
        publicOnly.ImportFromPem(artifact.Pem);
        Assert.Throws<CryptographicException>(() => publicOnly.ExportPkcs8PrivateKey());
        var message = new byte[] { 1, 2, 3 };
        Assert.True(publicOnly.VerifyData(message, rsa.SignData(message, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        var json = JsonSerializer.Serialize(artifact);
        Assert.DoesNotContain("CustodyReference", json);
        Assert.DoesNotContain("PrivateMaterial", json);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(3072)]
    public void Wrong_strength_public_keys_are_rejected(int bits)
    {
        using var rsa = RSA.Create(bits);
        Assert.Throws<KeyProtectionException>(() =>
            RsaSigningKeyMaterial.Artifact(Guid.NewGuid(), rsa.ExportSubjectPublicKeyInfo(), 1, 2));
    }

    [Fact]
    public void Noncanonical_or_extra_private_and_public_data_are_rejected()
    {
        using var key = new RsaGeneratedKeyFactory().Create();
        Assert.Throws<KeyProtectionException>(() =>
            RsaSigningKeyMaterial.DerivePublic(key.PrivateMaterial.Bytes.ToArray().Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<KeyProtectionException>(() =>
            RsaSigningKeyMaterial.Artifact(Guid.NewGuid(), key.PublicSpkiDer.Concat(new byte[] { 0 }).ToArray(), 1, 2));
    }

    [Fact]
    public void Initial_handoff_builds_matching_public_and_private_key_zip()
    {
        using var key = new RsaGeneratedKeyFactory().Create();
        using var handoff = GeneratedKeyHandoff.Create(key.PrivateMaterial.Bytes.ToArray(), key.PublicSpkiDer);

        Assert.Equal(key.PublicFingerprintSha256, handoff.PublicFingerprintSha256);
        Assert.Contains("-----BEGIN PRIVATE KEY-----", handoff.PrivatePem);
        Assert.Contains("-----BEGIN PUBLIC KEY-----", handoff.PublicPem);

        var zip = SigningKeyDownloadPackage.Create(handoff);
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Assert.Equal(new[] { "private.key", "public.key" }, archive.Entries.Select(x => x.Name).OrderBy(x => x));

        var publicPem = ReadEntry(archive, "public.key");
        var privatePem = ReadEntry(archive, "private.key");
        using var publicRsa = RSA.Create();
        using var privateRsa = RSA.Create();
        publicRsa.ImportFromPem(publicPem);
        privateRsa.ImportFromPem(privatePem);
        Assert.Equal(key.PublicFingerprintSha256,
            RsaSigningKeyMaterial.Fingerprint(publicRsa.ExportSubjectPublicKeyInfo()));
        var message = Encoding.UTF8.GetBytes("phase09-step21");
        Assert.True(publicRsa.VerifyData(message,
            privateRsa.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Disposed_handoff_zeroes_and_rejects_private_material()
    {
        using var key = new RsaGeneratedKeyFactory().Create();
        var handoff = GeneratedKeyHandoff.Create(key.PrivateMaterial.Bytes.ToArray(), key.PublicSpkiDer);
        handoff.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = handoff.PrivatePem);
        Assert.Throws<ObjectDisposedException>(() => SigningKeyDownloadPackage.Create(handoff));
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task Winner_generates_once_then_committed_retry_uses_public_metadata_only()
    {
        var journal = new Journal();
        var custody = new Custody();
        var factory = new Factory();
        var service = new SigningKeyGenerationService(journal, custody, factory);
        var request = Request();
        Assert.Equal(SigningGenerationStatus.Created, (await service.GenerateAsync("token", request)).Status);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.GenerateAsync("token", request)).Status);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(1, custody.Stages);
        Assert.Equal(1, custody.ReadinessCalls);
    }

    [Fact]
    public async Task Initial_handoff_is_available_only_to_the_new_generation_claim_winner()
    {
        var journal = new Journal();
        var custody = new Custody();
        var service = new SigningKeyGenerationService(journal, custody, new Factory());
        var request = Request();

        using var first = await service.GenerateWithInitialHandoffAsync("token", request);
        Assert.Equal(SigningGenerationStatus.Created, first.Result.Status);
        Assert.NotNull(first.Handoff);

        using var replay = await service.GenerateWithInitialHandoffAsync("token", request);
        Assert.Equal(SigningGenerationStatus.Replayed, replay.Result.Status);
        Assert.Null(replay.Handoff);
    }

    [Fact]
    public async Task Lost_acknowledgement_recovers_same_key_without_second_generation()
    {
        var journal = new Journal();
        var custody = new Custody { LoseAcknowledgement = true };
        var factory = new Factory();
        var service = new SigningKeyGenerationService(journal, custody, factory);
        var request = Request();
        Assert.Equal(SigningGenerationStatus.RecoveryRequired, (await service.GenerateAsync("token", request)).Status);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.GenerateAsync("token", request)).Status);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(1, custody.Stages);
    }

    [Fact]
    public async Task Empty_recovery_never_authorizes_another_generation()
    {
        var journal = new Journal { Claimed = true };
        var custody = new Custody();
        var factory = new Factory();
        var service = new SigningKeyGenerationService(journal, custody, factory);
        Assert.Equal(SigningGenerationStatus.RecoveryRequired, (await service.GenerateAsync("token", Request())).Status);
        Assert.Equal(0, factory.Calls);
        Assert.Equal(0, custody.Stages);
    }

    [Fact]
    public async Task Unavailable_provider_does_not_claim_or_generate()
    {
        var journal = new Journal();
        var factory = new Factory();
        var service = new SigningKeyGenerationService(journal, new Custody { Unavailable = true }, factory);
        Assert.Equal(SigningGenerationStatus.Unavailable, (await service.GenerateAsync("token", Request())).Status);
        Assert.False(journal.Claimed);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task Invalid_request_and_owner_denial_never_reach_crypto()
    {
        var journal = new Journal { Deny = true };
        var custody = new Custody();
        var factory = new Factory();
        var service = new SigningKeyGenerationService(journal, custody, factory);
        Assert.Equal(SigningGenerationStatus.Invalid,
            (await service.GenerateAsync("token", Request() with { AcceptServerCustody = false })).Status);
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.GenerateAsync("token", Request())).Status);
        Assert.Equal(0, factory.Calls);
        Assert.Equal(0, custody.ReadinessCalls);
    }

    [Fact]
    public async Task Default_registration_fails_closed_without_database_or_provider()
    {
        var services = new ServiceCollection();
        services.AddPhase09KeyGeneration(new ConfigurationBuilder().Build());
        using var container = services.BuildServiceProvider();
        Assert.Equal(SigningGenerationStatus.Unavailable, (await container.GetRequiredService<ISigningKeyGenerationService>()
            .GenerateAsync(null, Request())).Status);
    }

    internal sealed class Factory : IGeneratedKeyFactory
    {
        public int Calls;
        public GeneratedRsaKey Create() { Interlocked.Increment(ref Calls); return new RsaGeneratedKeyFactory().Create(); }
    }

    internal sealed class Journal : ISigningGenerationJournal
    {
        public bool Claimed;
        public bool Deny;
        public SealedGeneratedKey? Committed;
        public KeyProtectionIdentity Id = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        public Task<GenerationReservation> ReserveGenerationAsync(string? bearer, GenerateSigningKeyRequest request, CancellationToken ct) =>
            Task.FromResult(new GenerationReservation(Deny ? SigningGenerationStatus.Forbidden : SigningGenerationStatus.Replayed,
                new(Id.CustomerUserId, Id.TaxpayerId, Id.FiscalMemoryId), Id.OperationRecordId,
                Committed is null ? SigningOperationState.Reserved : SigningOperationState.Committed));
        public Task<GenerationAdmission> ClaimGenerationAsync(string? bearer, GenerationReservation reservation, Guid operationId, CancellationToken ct)
        {
            var owns = !Claimed;
            Claimed = true;
            return Task.FromResult(new GenerationAdmission(SigningGenerationStatus.Replayed, Id, owns));
        }
        public Task<SigningKeyGenerationResult> CompleteGenerationAsync(string? bearer, Guid operationId,
            SealedGeneratedKey receipt, CancellationToken ct)
        {
            Committed = receipt;
            return Task.FromResult(new SigningKeyGenerationResult(SigningGenerationStatus.Created,
                RsaSigningKeyMaterial.Artifact(Id.KeyVersionId, receipt.PublicSpkiDer, 2, 2)));
        }
        public Task<SigningKeyGenerationResult> ReadGeneratedAsync(string? bearer, SigningScope scope,
            Guid keyId, Guid? operationRecordId, CancellationToken ct) =>
            Task.FromResult(new SigningKeyGenerationResult(SigningGenerationStatus.Replayed,
                Committed is null ? null : RsaSigningKeyMaterial.Artifact(Id.KeyVersionId, Committed.PublicSpkiDer, 2, 2)));
        public Task<SigningKeyGenerationResult> ReadPublicAsync(string? bearer, Guid taxpayerId,
            Guid memoryId, Guid keyId, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class Custody : IGeneratedKeyCustody
    {
        public int Stages;
        public int ReadinessCalls;
        public bool LoseAcknowledgement;
        public bool Unavailable;
        public Func<Task>? AfterStage;
        public SealedGeneratedKey? Stored;
        public Task EnsureReadyAsync(CancellationToken ct)
        {
            ReadinessCalls++;
            if (Unavailable) throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            return Task.CompletedTask;
        }
        public async Task<SealedGeneratedKey> StageAsync(KeyProtectionIdentity id, GeneratedRsaKey key, CancellationToken ct)
        {
            Stages++;
            Stored = new(id, key.PublicSpkiDer, key.PublicFingerprintSha256, "vault:v1");
            if (AfterStage is not null) await AfterStage();
            if (LoseAcknowledgement) throw new KeyProtectionException(KeyProtectionFailure.RecoveryRequired);
            return Stored;
        }
        public Task<SealedGeneratedKey?> RecoverAsync(KeyProtectionIdentity id, CancellationToken ct) =>
            Task.FromResult(Stored);
    }
}
