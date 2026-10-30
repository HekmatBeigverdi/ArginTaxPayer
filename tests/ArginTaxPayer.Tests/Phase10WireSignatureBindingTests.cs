using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10WireSignatureBindingTests
{
    private static readonly DateTime IssueAt = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

    private static InvoiceDispatchPreparedSnapshot Prepared()
    {
        var pair = new InvoiceTypePattern(ElectronicInvoiceType.First, 4);
        var edition = OfficialV79InvoicePatternMatrix.Edition;
        var ctx = new InvoiceDispatchPreflightContext(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3,
            InvoiceLifecycleState.Finalized, InvoiceReviewState.Approved, pair,
            InvoiceSubjectSelection.Original(), IssueAt, edition,
            new string('a', 64), new[] { Guid.NewGuid() });
        var input = new InvoiceWireInput(pair, InvoiceSubject.Original,
            "AAAAAAAAAAAAAAAAAAAAAA",
            new DateTimeOffset(IssueAt).ToUnixTimeMilliseconds(),
            "1234567890",
            new[] { new InvoiceWireLine("1234567890123", 2, 100, 200, 20,
                180, 10, 18, 198, "service", "1") },
            200, 20, 180, 18, 198, ContractRegistrationNumber: "123456");
        var source = new RuleSetPrimarySource("RC_IITP.IS", edition, "table",
            new string('b', 64));
        var rules = new InvoiceRuleSetRegistry(new[] {
            new InvoiceRuleBundle(OfficialV79RuleSet.Scope, source,
                new RuleSetValidity(IssueAt.AddDays(-1), null, true, source),
                new InvoiceRuleEngine(edition, Array.Empty<IInvoiceRule>()),
                new[] { "invoice.pattern.combination" }, coverageReviewed: true)
        });
        var patterns = new InvoicePatternRegistry(new[] {
            new InvoicePatternMatrix(new InvoicePatternSource(
                "RC_IITP.IS", edition, "table", IssueAt.AddDays(-1), null, true),
                new[] { pair })
        });
        return Assert.IsType<InvoiceDispatchPreparedSnapshot>(
            Phase10DispatchPreflight.Evaluate(ctx, input, rules, patterns).Snapshot);
    }

    private static (InvoiceWireSigningIntent Intent, AuthorizedSigningRequest Request,
        SigningResult Result) Signing(InvoiceDispatchPreparedSnapshot p)
    {
        var key = Guid.NewGuid();
        const string profile = "legacy-synthetic-profile";
        var intent = new InvoiceWireSigningIntent(key, 5, profile);
        var request = new AuthorizedSigningRequest(p.InvoiceId, p.CustomerUserId,
            p.TaxpayerId, p.FiscalMemoryId, key, p.InvoiceRevision, 5,
            p.WirePayloadSha256, profile);
        var result = new SigningResult(DigitalSigningStatus.Signed,
            Convert.ToBase64String(new byte[256]), p.WirePayloadSha256, key, profile);
        return (intent, request, result);
    }

    [Fact]
    public void Matching_metadata_is_still_not_a_verified_wire_signature()
    {
        var p = Prepared();
        var s = Signing(p);
        var binding = Phase10WireSignatureBinding.Check(p, s.Intent, s.Request, s.Result);
        Assert.Equal(InvoiceWireSignatureBindingStatus.MetadataMatchedButWireUnverified,
            binding.Status);
        Assert.False(binding.IsWireSignatureVerified);
        Assert.False(binding.IsFiscalSubmissionAuthorized);
        Assert.Equal(p.WirePayloadSha256, binding.WireSha256);
    }

    [Fact]
    public void Phase09_business_snapshot_hash_cannot_be_substituted_for_wire_hash()
    {
        var p = Prepared();
        var s = Signing(p);
        var wrong = s.Request with { SnapshotSha256 = p.ApprovedSnapshotSha256 };
        Assert.Equal(InvoiceWireSignatureBindingStatus.HashMismatch,
            Phase10WireSignatureBinding.Check(p, s.Intent, wrong, s.Result).Status);
        Assert.Equal(InvoiceWireSignatureBindingStatus.HashMismatch,
            Phase10WireSignatureBinding.Check(p, s.Intent, s.Request,
                s.Result with { SnapshotSha256 = new string('f', 64) }).Status);
    }

    [Fact]
    public void Cross_tenant_revision_or_key_mismatch_is_denied()
    {
        var p = Prepared();
        var s = Signing(p);
        Assert.Equal(InvoiceWireSignatureBindingStatus.ScopeMismatch,
            Phase10WireSignatureBinding.Check(p, s.Intent,
                s.Request with { TaxpayerId = Guid.NewGuid() }, s.Result).Status);
        Assert.Equal(InvoiceWireSignatureBindingStatus.ScopeMismatch,
            Phase10WireSignatureBinding.Check(p, s.Intent,
                s.Request with { ExpectedInvoiceRevision = 4 }, s.Result).Status);
        Assert.Equal(InvoiceWireSignatureBindingStatus.KeyVersionMismatch,
            Phase10WireSignatureBinding.Check(p,
                s.Intent with { KeyVersionId = Guid.NewGuid() }, s.Request, s.Result).Status);
        Assert.Equal(InvoiceWireSignatureBindingStatus.UnsupportedProfile,
            Phase10WireSignatureBinding.Check(p, s.Intent,
                s.Request with { ProfileId = "other" }, s.Result).Status);
    }

    [Fact]
    public void Invalid_signature_and_unsigned_result_are_not_accepted()
    {
        var p = Prepared();
        var s = Signing(p);
        Assert.Equal(InvoiceWireSignatureBindingStatus.InvalidSignatureEnvelope,
            Phase10WireSignatureBinding.Check(p, s.Intent, s.Request,
                s.Result with { SignatureBase64 = "invalid" }).Status);
        Assert.Equal(InvoiceWireSignatureBindingStatus.SigningFailed,
            Phase10WireSignatureBinding.Check(p, s.Intent, s.Request,
                s.Result with { Status = DigitalSigningStatus.IntegrityFailure }).Status);
    }
}
