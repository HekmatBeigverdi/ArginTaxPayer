using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10SandboxSubmissionAdmissionTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Taxpayer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Invoice = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Memory = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 14, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', 64);

    private static SandboxSubmissionAdmissionContext Valid() => new(
        Owner, Owner, Taxpayer, Invoice, Memory, 4, InvoiceLifecycleState.Finalized,
        InvoiceReviewState.Approved, Hash, Hash, "Sandbox",
        new SandboxSingleInvoiceConsent(Owner, Taxpayer, Invoice, Memory, 4,
            Hash, Now.AddMinutes(2), true, "Sandbox"), Now);

    [Fact]
    public void Matching_owner_finalized_revision_and_consent_is_admitted()
    {
        var result = SandboxSubmissionAdmissionPolicy.Evaluate(Valid());

        Assert.True(result.IsAdmitted);
        Assert.Empty(result.BlockingCodes);
    }

    [Theory]
    [InlineData(InvoiceLifecycleState.Draft, "invoice.not_finalized")]
    [InlineData(InvoiceLifecycleState.Validated, "invoice.not_finalized")]
    public void Non_finalized_invoice_is_rejected_without_admission(
        InvoiceLifecycleState state, string expectedCode)
    {
        var result = SandboxSubmissionAdmissionPolicy.Evaluate(
            Valid() with { InvoiceState = state });

        Assert.False(result.IsAdmitted);
        Assert.Contains(expectedCode, result.BlockingCodes);
    }

    [Fact]
    public void Owner_review_revision_or_consent_mismatch_is_rejected()
    {
        Assert.Contains("owner.mismatch", SandboxSubmissionAdmissionPolicy.Evaluate(
            Valid() with { AuthenticatedActorId = Guid.NewGuid() }).BlockingCodes);
        Assert.Contains("review.not_approved", SandboxSubmissionAdmissionPolicy.Evaluate(
            Valid() with { ReviewState = InvoiceReviewState.InReview }).BlockingCodes);
        Assert.Contains("revision.mismatch", SandboxSubmissionAdmissionPolicy.Evaluate(
            Valid() with { Consent = Valid().Consent! with { InvoiceRevision = 3 } }).BlockingCodes);
        Assert.Contains("snapshot_hash.mismatch", SandboxSubmissionAdmissionPolicy.Evaluate(
            Valid() with { ServerSnapshotSha256 = new('b', 64) }).BlockingCodes);
    }
}
