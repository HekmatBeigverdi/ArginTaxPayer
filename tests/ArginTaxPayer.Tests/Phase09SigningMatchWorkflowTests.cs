using System.Security.Cryptography;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Matching;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09SigningMatchWorkflowTests
{
    internal static MatchSigningKeyRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2,
        SigningPublicKeyFormat.SpkiDer);
    [Fact]
    public async Task Denied_owner_is_checked_before_parsing_or_private_access()
    {
        var journal = new Journal { Authorization = SigningKeyMatchStatus.Forbidden };
        var proof = new Proof();
        var result = await new SigningKeyMatchService(journal, proof, new()).MatchAsync("token", Request(), new byte[] { 1 });
        Assert.Equal(SigningKeyMatchStatus.Forbidden, result.Status);
        Assert.Equal(0, journal.Begins);
        Assert.Equal(0, proof.Calls);
    }
    [Theory]
    [InlineData(2, SigningKeyMatchStatus.Unavailable)]
    [InlineData(1, SigningKeyMatchStatus.IntegrityFailure)]
    [InlineData(4, SigningKeyMatchStatus.RecoveryRequired)]
    public async Task Provider_failure_never_becomes_mismatch(int failure, SigningKeyMatchStatus expected)
    {
        using var rsa = RSA.Create(2048);
        var journal = new Journal();
        var result = await new SigningKeyMatchService(journal, new Proof { Failure = (KeyProtectionFailure)failure }, new())
            .MatchAsync("token", Request(), rsa.ExportSubjectPublicKeyInfo());
        Assert.Equal(expected, result.Status);
        Assert.Equal(0, journal.Completions);
        Assert.Null(result.StoredFingerprintSha256);
    }
    [Fact]
    public async Task No_match_result_is_released_when_commit_authorization_is_lost()
    {
        using var rsa = RSA.Create(2048);
        var journal = new Journal { Completion = SigningKeyMatchStatus.Forbidden };
        var result = await new SigningKeyMatchService(journal, new Proof(), new())
            .MatchAsync("token", Request(), rsa.ExportSubjectPublicKeyInfo());
        Assert.Equal(SigningKeyMatchStatus.Forbidden, result.Status);
        Assert.Null(result.ProvidedFingerprintSha256);
    }
    [Fact]
    public async Task Cancellation_after_durable_intent_returns_recovery_without_completion()
    {
        using var rsa = RSA.Create(2048);
        var journal = new Journal();
        var proof = new Proof { Cancel = true };
        var result = await new SigningKeyMatchService(journal, proof, new())
            .MatchAsync("token", Request(), rsa.ExportSubjectPublicKeyInfo());
        Assert.Equal(SigningKeyMatchStatus.RecoveryRequired, result.Status);
        Assert.Equal(0, journal.Completions);
    }
    [Fact]
    public async Task Disabled_registration_requires_no_database_or_provider()
    {
        var services = new ServiceCollection();
        services.AddPhase09KeyMatching(new ConfigurationBuilder().Build());
        using var container = services.BuildServiceProvider();
        var result = await container.GetRequiredService<ISigningKeyMatchService>().MatchAsync(null, Request(), new byte[] { 1 });
        Assert.Equal(SigningKeyMatchStatus.Unavailable, result.Status);
        Assert.False(result.PortalRegistrationProven);
    }
    [Fact]
    public async Task Invalid_public_input_never_creates_intent_or_reads_private_custody()
    {
        var journal = new Journal();
        var proof = new Proof();
        var result = await new SigningKeyMatchService(journal, proof, new()).MatchAsync("token", Request(), new byte[] { 1 });
        Assert.Equal(SigningKeyMatchStatus.Invalid, result.Status);
        Assert.Equal(0, journal.Begins);
        Assert.Equal(0, proof.Calls);
    }
    [Fact]
    public void Public_result_contains_no_proof_material_or_registration_claim()
    {
        var result = new SigningKeyMatchResult(SigningKeyMatchStatus.Matched, Guid.NewGuid(), new string('a', 64), new string('a', 64), 4);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        foreach (var name in new[] { "Signature", "Challenge", "Private", "Password", "Custody", "Token" })
            Assert.DoesNotContain(name, json);
        Assert.False(result.PortalRegistrationProven);
        Assert.Equal("Unverified", result.PortalVerification);
    }
    private sealed class Journal : ISigningKeyMatchJournal
    {
        public SigningKeyMatchStatus Authorization = SigningKeyMatchStatus.Matched;
        public SigningKeyMatchStatus Completion = SigningKeyMatchStatus.Matched;
        public int Begins, Completions;
        public Task<SigningKeyMatchStatus> AuthorizeMatchAsync(string? b, MatchSigningKeyRequest r, CancellationToken ct) =>
            Task.FromResult(Authorization);
        public Task<KeyMatchAdmission> BeginMatchAsync(string? b, MatchSigningKeyRequest r, string fp, CancellationToken ct)
        {
            Begins++;
            return Task.FromResult(new KeyMatchAdmission(SigningKeyMatchStatus.Matched,
                new(Guid.NewGuid(), new(Guid.NewGuid(), r.TaxpayerId, r.FiscalMemoryId, r.KeyVersionId, Guid.NewGuid()),
                    r.ExpectedRevision + 1, Array.Empty<byte>(), fp, fp)));
        }
        public Task<SigningKeyMatchResult> CompleteMatchAsync(string? b, KeyMatchTicket ticket, bool matched, CancellationToken ct)
        {
            Completions++;
            return Task.FromResult(new SigningKeyMatchResult(Completion));
        }
    }
    private sealed class Proof : IKeyMatchProof
    {
        public KeyProtectionFailure? Failure;
        public bool Cancel;
        public int Calls;
        public Task<bool> ProveAsync(KeyMatchTicket ticket, byte[] spki, CancellationToken ct)
        {
            Calls++;
            if (Cancel) throw new OperationCanceledException();
            if (Failure is { } failure) throw new KeyProtectionException(failure);
            return Task.FromResult(true);
        }
    }
}
