using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10DispatchLineagePolicyTests
{
    private const string CurrentTaxId = "A123456789012345678901";
    private const string PriorTaxId = "B123456789012345678901";
    private static readonly InvoiceTypePattern Classification =
        new(ElectronicInvoiceType.First, 1);

    private static InvoiceDispatchLineageContext Current(InvoiceSubject subject,
        Guid? predecessorId = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            subject == InvoiceSubject.Original
                ? InvoiceSubjectSelection.Original()
                : InvoiceSubjectSelection.Create(subject,
                    new InvoiceReference(PriorTaxId, predecessorId)),
            Classification, CurrentTaxId);

    private static InvoiceDispatchPredecessorEvidence Evidence(
        InvoiceDispatchLineageContext c) =>
        new(c.Selection.Reference!.LocalInvoiceId ?? Guid.NewGuid(),
            c.CustomerUserId, c.TaxpayerId, c.FiscalMemoryId, PriorTaxId,
            InvoiceSubject.Original, true, false, Classification);

    [Fact]
    public void Original_does_not_need_a_predecessor()
    {
        var d = Phase10DispatchLineagePolicy.Check(Current(InvoiceSubject.Original), null, null);
        Assert.True(d.IsEligibleCandidate);
        Assert.False(d.IsFiscalSubmissionAuthorized);
    }

    [Theory]
    [InlineData(InvoiceSubject.Correction)]
    [InlineData(InvoiceSubject.Cancellation)]
    [InlineData(InvoiceSubject.SalesReturn)]
    public void Non_original_requires_a_verified_same_scope_predecessor(InvoiceSubject subject)
    {
        var c = Current(subject);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorNotResolved,
            Phase10DispatchLineagePolicy.Check(c, null, null).Code);
        var e = Evidence(c);
        var d = Phase10DispatchLineagePolicy.Check(c, e, null);
        Assert.True(d.IsEligibleCandidate);
        Assert.False(d.IsFiscalSubmissionAuthorized);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorNotAuthorityAccepted,
            Phase10DispatchLineagePolicy.Check(c,
                e with { IsAuthorityAcceptanceVerified = false }, null).Code);
    }

    [Fact]
    public void Wrong_customer_memory_or_tax_id_never_crosses_scope()
    {
        var c = Current(InvoiceSubject.Correction);
        var p = Evidence(c);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorScopeMismatch,
            Phase10DispatchLineagePolicy.Check(c,
                p with { CustomerUserId = Guid.NewGuid() }, null).Code);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorScopeMismatch,
            Phase10DispatchLineagePolicy.Check(c,
                p with { FiscalMemoryId = Guid.NewGuid() }, null).Code);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorScopeMismatch,
            Phase10DispatchLineagePolicy.Check(c,
                p with { TaxId = CurrentTaxId }, null).Code);
    }

    [Fact]
    public void Canceled_superseded_and_wrong_pattern_are_denied()
    {
        var c = Current(InvoiceSubject.SalesReturn);
        var p = Evidence(c);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorInactive,
            Phase10DispatchLineagePolicy.Check(c,
                p with { IsCanceledOrSuperseded = true }, null).Code);
        Assert.Equal(InvoiceDispatchLineageCode.PredecessorTypeMismatch,
            Phase10DispatchLineagePolicy.Check(c,
                p with { Classification = new(ElectronicInvoiceType.First, 4) }, null).Code);
    }

    [Fact]
    public void Correction_and_return_compete_for_same_active_claim()
    {
        var c = Current(InvoiceSubject.SalesReturn);
        var p = Evidence(c);
        var other = new InvoiceDispatchReferenceClaim(Guid.NewGuid(),
            c.CustomerUserId, c.TaxpayerId, PriorTaxId, InvoiceSubject.Correction, true);
        Assert.Equal(InvoiceDispatchLineageCode.ReferenceClaimConflict,
            Phase10DispatchLineagePolicy.Check(c, p, new[] { other }).Code);
        Assert.True(Phase10DispatchLineagePolicy.Check(c, p,
            new[] { other with { Active = false } }).IsEligibleCandidate);
    }

    [Fact]
    public void Cancellation_has_separate_exclusive_claim_slot()
    {
        var c = Current(InvoiceSubject.Cancellation);
        var p = Evidence(c);
        var correction = new InvoiceDispatchReferenceClaim(Guid.NewGuid(),
            c.CustomerUserId, c.TaxpayerId, PriorTaxId, InvoiceSubject.Correction, true);
        Assert.True(Phase10DispatchLineagePolicy.Check(c, p,
            new[] { correction }).IsEligibleCandidate);
        Assert.Equal(InvoiceDispatchLineageCode.ReferenceClaimConflict,
            Phase10DispatchLineagePolicy.Check(c, p,
                new[] { correction with { Subject = InvoiceSubject.Cancellation } }).Code);
    }

    [Fact]
    public void Self_reference_is_denied_even_if_predecessor_metadata_exists()
    {
        var c = Current(InvoiceSubject.Correction);
        var self = c with { Selection = InvoiceSubjectSelection.Create(
            InvoiceSubject.Correction, new InvoiceReference(CurrentTaxId)) };
        Assert.Equal(InvoiceDispatchLineageCode.SelfReference,
            Phase10DispatchLineagePolicy.Check(self, Evidence(c), null).Code);
    }

    [Fact]
    public void Official_unsupported_subject_pattern_is_denied()
    {
        var c = Current(InvoiceSubject.SalesReturn) with {
            Classification = new InvoiceTypePattern(ElectronicInvoiceType.First, 7)
        };
        Assert.Equal(InvoiceDispatchLineageCode.UnsupportedSubjectPattern,
            Phase10DispatchLineagePolicy.Check(c, Evidence(c), null).Code);
    }
}
