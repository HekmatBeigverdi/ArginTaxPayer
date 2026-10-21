using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09InvoiceSnapshotSigningTests
{
    [Fact]
    public async Task Invalid_request_is_rejected_before_snapshot_or_custody()
    {
        var source = new Source();
        var custody = new Custody();
        var service = new InvoiceSnapshotSigningService(source, new Audit(), custody, new LegacyOfflineSigningEngine());
        var result = await service.SignAsync("token", new AuthorizedSigningRequest(Guid.Empty, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, new string('a', 64), SigningOperationPolicy.LegacyProfile));
        Assert.Equal(DigitalSigningStatus.Invalid, result.Status);
        Assert.Equal(0, source.Calls);
        Assert.Equal(0, custody.Calls);
    }

    [Fact]
    public async Task Snapshot_hash_and_revision_are_verified_before_custody()
    {
        var f = Fixture.Create();
        f.Source.Snapshot = f.Snapshot with { SnapshotSha256 = new string('0', 64) };
        var result = await f.Service.SignAsync("token", f.Request);
        Assert.Equal(DigitalSigningStatus.IntegrityFailure, result.Status);
        Assert.Equal(0, f.Custody.Calls);
        Assert.Equal(0, f.Audit.BeginCalls);
    }

    [Fact]
    public async Task Durable_intent_precedes_private_material_access_and_audit_completion()
    {
        var f = Fixture.Create();
        var result = await f.Service.SignAsync("token", f.Request);
        Assert.Equal(DigitalSigningStatus.Signed, result.Status);
        Assert.Equal(1, f.Audit.BeginCalls);
        Assert.Equal(1, f.Custody.Calls);
        Assert.Equal(1, f.Audit.CompleteCalls);
        Assert.Equal(f.Snapshot.SnapshotSha256, result.SnapshotSha256);
        Assert.False(result.IsFiscalSubmissionAuthorized);
    }

    [Fact]
    public async Task Failed_completion_does_not_release_signature()
    {
        var f = Fixture.Create();
        f.Audit.Completion = DigitalSigningStatus.Conflict;
        var result = await f.Service.SignAsync("token", f.Request);
        Assert.Equal(DigitalSigningStatus.Conflict, result.Status);
        Assert.Null(result.SignatureBase64);
    }

    private sealed class Source : ITrustedInvoiceSnapshotSource
    {
        public TrustedInvoiceSnapshot? Snapshot; public int Calls;
        public Task<TrustedInvoiceSnapshot?> LoadAsync(string? bearer, AuthorizedSigningRequest request, CancellationToken ct)
        { Calls++; return Task.FromResult(Snapshot); }
    }
    private sealed class Audit : IInvoiceSigningAudit
    {
        public int BeginCalls, CompleteCalls; public DigitalSigningStatus Completion = DigitalSigningStatus.Signed;
        public Task<SigningAdmission> BeginAsync(string? bearer, TrustedInvoiceSnapshot snapshot, CancellationToken ct)
        { BeginCalls++; return Task.FromResult(new SigningAdmission(SigningAdmissionStatus.Allowed, Guid.NewGuid())); }
        public Task<DigitalSigningStatus> CompleteAsync(string? bearer, Guid intentId, TrustedInvoiceSnapshot snapshot, string signatureBase64, CancellationToken ct)
        { CompleteCalls++; return Task.FromResult(Completion); }
    }
    private sealed class Custody : IInvoiceSigningCustody
    {
        public int Calls; private readonly byte[] privateKey;
        public Custody(byte[] privateKey) => this.privateKey = privateKey;
        public Task<SensitiveBytes> UnwrapAsync(TrustedInvoiceSnapshot snapshot, CancellationToken ct)
        { Calls++; return Task.FromResult(SensitiveBytes.TakeOwnership(privateKey.ToArray())); }
    }
    private sealed record Fixture(Source Source, Audit Audit, Custody Custody, TrustedInvoiceSnapshot Snapshot,
        AuthorizedSigningRequest Request, InvoiceSnapshotSigningService Service)
    {
        public static Fixture Create()
        {
            using var rsa = RSA.Create(2048);
            var json = "{\"amount\":100}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
            var privateKey = rsa.ExportPkcs8PrivateKey();
            var snapshot = new TrustedInvoiceSnapshot(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), 4, 2, json, hash, SigningOperationPolicy.LegacyProfile, rsa.ExportSubjectPublicKeyInfo());
            var request = new AuthorizedSigningRequest(snapshot.InvoiceId, snapshot.CustomerUserId, snapshot.TaxpayerId,
                snapshot.FiscalMemoryId, snapshot.KeyVersionId, snapshot.InvoiceRevision, snapshot.KeyRevision,
                snapshot.SnapshotSha256, snapshot.ProfileId);
            var source = new Source { Snapshot = snapshot };
            var audit = new Audit(); var custody = new Custody(privateKey);
            return new(source, audit, custody, snapshot,
                request, new InvoiceSnapshotSigningService(source, audit, custody, new LegacyOfflineSigningEngine()));
        }
    }
}
