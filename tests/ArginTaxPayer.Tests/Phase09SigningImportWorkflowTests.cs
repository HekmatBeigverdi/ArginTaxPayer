using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Import;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningImportWorkflowTests
{
    internal static ImportSigningKeyRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true);

    [Fact]
    public async Task Same_public_key_replays_across_supported_encodings_and_changed_key_conflicts()
    {
        var journal = new Journal();
        var custody = new Phase09SigningKeyGenerationTests.Custody();
        var service = new SigningKeyImportService(journal, custody, new());
        using var rsa = RSA.Create(2048);
        var request = Request();
        var input = Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Pem);
        var owned = input.KeyBytes;
        Assert.Equal(SigningGenerationStatus.Created, (await service.ImportAsync("token", request, input)).Status);
        Assert.All(owned.ToArray(), b => Assert.Equal((byte)0, b));
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.ImportAsync("token", request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.EncryptedPkcs8Der))).Status);
        using var other = RSA.Create(2048);
        Assert.Equal(SigningGenerationStatus.Conflict, (await service.ImportAsync("token", request,
            Phase09SigningKeyImportTests.Input(other, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        Assert.Equal(1, custody.Stages);
    }

    [Fact]
    public async Task Lost_acknowledgement_recovers_without_second_private_encryption()
    {
        var journal = new Journal();
        var custody = new Phase09SigningKeyGenerationTests.Custody { LoseAcknowledgement = true };
        var service = new SigningKeyImportService(journal, custody, new());
        using var rsa = RSA.Create(2048);
        var request = Request();
        Assert.Equal(SigningGenerationStatus.RecoveryRequired, (await service.ImportAsync("token", request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        Assert.Equal(SigningGenerationStatus.Replayed, (await service.ImportAsync("token", request,
            Phase09SigningKeyImportTests.Input(rsa, SigningPrivateKeyFormat.Pkcs8Der))).Status);
        Assert.Equal(1, custody.Stages);
    }

    [Fact]
    public async Task Owner_denial_precedes_parsing_and_invalid_key_never_reserves_operation()
    {
        var journal = new Journal { Deny = true };
        var service = new SigningKeyImportService(journal, new Phase09SigningKeyGenerationTests.Custody(), new());
        var input = new SensitiveKeyInput(SigningPrivateKeyFormat.Pkcs8Der, new byte[] { 1, 2 });
        Assert.Equal(SigningGenerationStatus.Forbidden, (await service.ImportAsync("token", Request(), input)).Status);
        Assert.Throws<ObjectDisposedException>(() => input.KeyBytes);
        journal.Deny = false;
        Assert.Equal(SigningGenerationStatus.Invalid, (await service.ImportAsync("token", Request(),
            new(SigningPrivateKeyFormat.Pkcs8Der, new byte[] { 1, 2 }))).Status);
        Assert.Equal(0, journal.Reservations);
    }

    [Fact]
    public async Task Cancellation_clears_input_and_password()
    {
        var input = new SensitiveKeyInput(SigningPrivateKeyFormat.EncryptedPkcs8Der, new byte[] { 1, 2 }, "secret");
        var bytes = input.KeyBytes;
        var password = input.Password;
        var service = new SigningKeyImportService(new Journal(), new Phase09SigningKeyGenerationTests.Custody(), new());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(SigningGenerationStatus.RecoveryRequired,
            (await service.ImportAsync("token", Request(), input, cancellation.Token)).Status);
        Assert.All(bytes.ToArray(), b => Assert.Equal((byte)0, b));
        Assert.All(password.ToArray(), c => Assert.Equal('\0', c));
    }

    [Fact]
    public async Task Disabled_registration_clears_input_and_fails_closed()
    {
        var services = new ServiceCollection();
        services.AddPhase09KeyImport(new ConfigurationBuilder().Build());
        using var container = services.BuildServiceProvider();
        var input = new SensitiveKeyInput(SigningPrivateKeyFormat.Pkcs8Der, new byte[] { 1 });
        Assert.Equal(SigningGenerationStatus.Unavailable,
            (await container.GetRequiredService<ISigningKeyImportService>().ImportAsync(null, Request(), input)).Status);
        Assert.Throws<ObjectDisposedException>(() => input.KeyBytes);
    }

    internal sealed class Journal : ISigningImportJournal
    {
        public bool Deny;
        public int Reservations;
        private string? fingerprint;
        private readonly Phase09SigningKeyGenerationTests.Journal core = new();
        public Task<GenerationReservation> AuthorizeImportAsync(string? bearer, ImportSigningKeyRequest request, CancellationToken ct) =>
            Task.FromResult(new GenerationReservation(Deny ? SigningGenerationStatus.Forbidden : SigningGenerationStatus.Replayed,
                new(core.Id.CustomerUserId, core.Id.TaxpayerId, core.Id.FiscalMemoryId), Guid.Empty, default));
        public Task<GenerationReservation> ReserveImportAsync(string? bearer, ImportSigningKeyRequest request, string fp, CancellationToken ct)
        {
            Reservations++;
            if (fingerprint is not null && fingerprint != fp)
                return Task.FromResult(new GenerationReservation(SigningGenerationStatus.Conflict, default, Guid.Empty, default));
            fingerprint = fp;
            return core.ReserveGenerationAsync(bearer, new(request.TaxpayerId, request.FiscalMemoryId,
                request.OperationId, request.ProfileId, request.ConsentVersion, request.AcceptServerCustody), ct);
        }
        public Task<GenerationAdmission> ClaimImportAsync(string? bearer, GenerationReservation r, Guid op, string fp, CancellationToken ct) =>
            core.ClaimGenerationAsync(bearer, r, op, ct);
        public Task<SigningKeyGenerationResult> CompleteImportAsync(string? bearer, Guid op, SealedGeneratedKey receipt, CancellationToken ct) =>
            core.CompleteGenerationAsync(bearer, op, receipt, ct);
        public Task<SigningKeyGenerationResult> ReadImportAsync(string? bearer, GenerationReservation r, CancellationToken ct) =>
            core.ReadGeneratedAsync(bearer, r.Scope, Guid.Empty, r.OperationRecordId, ct);
    }
}
