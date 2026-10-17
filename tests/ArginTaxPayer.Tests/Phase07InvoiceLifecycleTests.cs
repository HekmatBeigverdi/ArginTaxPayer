using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase07InvoiceLifecycleTests
{
    private static readonly DateTime At = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);
    private static InvoiceAggregate Draft() =>
        InvoiceAggregate.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, At, At, new InvoicePartySnapshot(null, "فروشنده"));

    private static InvoiceValidationReceipt Valid(InvoiceAggregate invoice) =>
        new(invoice.Id, invoice.Revision, OfficialV79InvoicePatternMatrix.Edition,
            new string('a', 64), At.AddMinutes(2), false);

    private static InvoiceInternalFinalizationApproval Approval(InvoiceAggregate invoice) =>
        new(invoice.Id, invoice.Revision, Guid.NewGuid(), "local-approval-001",
            At.AddMinutes(3));

    [Fact]
    public void New_invoice_is_draft_and_authority_unsubmitted()
    {
        var invoice = Draft();
        Assert.Equal(InvoiceLifecycleState.Draft, invoice.State);
        Assert.Equal(TaxAuthorityProcessingState.NotSubmitted, invoice.AuthorityState);
        Assert.Null(invoice.LastValidation);
        Assert.Null(invoice.FinalizationApproval);
        Assert.True(InvoiceLifecyclePolicy.CanTransition(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Validated));
        Assert.False(InvoiceLifecyclePolicy.CanTransition(InvoiceLifecycleState.Draft, InvoiceLifecycleState.Finalized));
    }

    [Fact]
    public void Local_validation_and_internal_finalization_are_revision_pinned()
    {
        var invoice = Draft();
        var originalRevision = invoice.Revision;
        invoice.MarkLocallyValidated(Valid(invoice));
        Assert.Equal(InvoiceLifecycleState.Validated, invoice.State);
        Assert.Equal(originalRevision + 1, invoice.Revision);
        Assert.Equal(originalRevision, invoice.LastValidation!.Revision);
        invoice.FinalizeInternally(Approval(invoice));
        Assert.Equal(InvoiceLifecycleState.Finalized, invoice.State);
        Assert.Equal(originalRevision + 2, invoice.Revision);
        Assert.NotNull(invoice.FinalizationApproval);
        Assert.Equal(TaxAuthorityProcessingState.NotSubmitted, invoice.AuthorityState);
    }

    [Fact]
    public void Stale_cross_invoice_and_error_receipts_are_rejected_without_mutation()
    {
        var invoice = Draft();
        var bad = new InvoiceValidationReceipt(Guid.NewGuid(), invoice.Revision,
            "v1", new string('a', 64), At.AddMinutes(2), false);
        Assert.Throws<InvalidOperationException>(() => invoice.MarkLocallyValidated(bad));
        Assert.Throws<InvalidOperationException>(() => invoice.MarkLocallyValidated(
            new InvoiceValidationReceipt(invoice.Id, invoice.Revision - 0 + 1,
                "v1", new string('a', 64), At.AddMinutes(2), false)));
        Assert.Throws<InvalidOperationException>(() => invoice.MarkLocallyValidated(
            new InvoiceValidationReceipt(invoice.Id, invoice.Revision,
                "v1", new string('a', 64), At.AddMinutes(2), true)));
        Assert.Equal(InvoiceLifecycleState.Draft, invoice.State);
        Assert.Equal(1, invoice.Revision);
    }

    [Fact]
    public void Validated_document_may_be_reopened_and_old_validation_is_discarded()
    {
        var invoice = Draft();
        invoice.MarkLocallyValidated(Valid(invoice));
        invoice.ReopenValidatedDraft();
        Assert.Equal(InvoiceLifecycleState.Draft, invoice.State);
        Assert.Null(invoice.LastValidation);
        Assert.Equal(3, invoice.Revision);
        invoice.UpdateDraftDetails(At.AddHours(1), new InvoicePartySnapshot(null, "فروشنده"), null);
        Assert.Equal(4, invoice.Revision);
        Assert.Throws<InvalidOperationException>(() => invoice.FinalizeInternally(Approval(invoice)));
    }

    [Fact]
    public void Finalized_invoice_cannot_be_mutated_or_reopened()
    {
        var invoice = Draft();
        invoice.MarkLocallyValidated(Valid(invoice));
        invoice.FinalizeInternally(Approval(invoice));
        var revision = invoice.Revision;
        Assert.Throws<InvalidOperationException>(() => invoice.ReopenValidatedDraft());
        Assert.Throws<InvalidOperationException>(() => invoice.UpdateDraftDetails(
            At, new InvoicePartySnapshot(null, "فروشنده جدید"), null));
        Assert.Throws<InvalidOperationException>(() => invoice.UpdateDraftSubject(
            InvoiceSubjectSelection.Original()));
        Assert.Throws<InvalidOperationException>(() => invoice.RemoveDraftLine(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => invoice.UpdateDraftLineQuantity(Guid.NewGuid(), 2m));
        Assert.Throws<InvalidOperationException>(() => invoice.PreviewDraftFinancials(
            Array.Empty<InvoiceFinancialInput>(),
            new InvoiceTypePattern(ElectronicInvoiceType.First, 1),
            new InvoiceRoundingPolicy(OfficialV79InvoicePatternMatrix.Edition)));
        Assert.Equal(revision, invoice.Revision);
        Assert.Equal(TaxAuthorityProcessingState.NotSubmitted, invoice.AuthorityState);
    }

    [Fact]
    public void Finalization_with_wrong_invoice_or_revision_is_rejected_atomically()
    {
        var invoice = Draft();
        invoice.MarkLocallyValidated(Valid(invoice));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.FinalizeInternally(new InvoiceInternalFinalizationApproval(
                Guid.NewGuid(), invoice.Revision, Guid.NewGuid(), "approval", At.AddMinutes(3))));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.FinalizeInternally(new InvoiceInternalFinalizationApproval(
                invoice.Id, invoice.Revision - 1, Guid.NewGuid(), "approval", At.AddMinutes(3))));
        Assert.Equal(InvoiceLifecycleState.Validated, invoice.State);
        Assert.Null(invoice.FinalizationApproval);
    }

    [Fact]
    public void Validation_cannot_be_reused_or_reapplied_after_state_change()
    {
        var invoice = Draft();
        var receipt = Valid(invoice);
        invoice.MarkLocallyValidated(receipt);
        Assert.Throws<InvalidOperationException>(() => invoice.MarkLocallyValidated(receipt));
        invoice.ReopenValidatedDraft();
        Assert.Throws<InvalidOperationException>(() => invoice.MarkLocallyValidated(receipt));
    }

    [Fact]
    public void Internal_finalization_is_not_tax_authority_acceptance()
    {
        var invoice = Draft();
        invoice.MarkLocallyValidated(Valid(invoice));
        invoice.FinalizeInternally(Approval(invoice));
        Assert.Equal(TaxAuthorityProcessingState.NotSubmitted, invoice.AuthorityState);
        Assert.NotEqual((int)InvoiceSubject.Original, (int)InvoiceLifecycleState.Finalized);
        Assert.False(InvoiceLifecyclePolicy.CanTransition(
            InvoiceLifecycleState.Finalized, InvoiceLifecycleState.Draft));
    }

    [Fact]
    public void Invalid_validation_hash_and_nonutc_approval_are_refused()
    {
        var invoice = Draft();
        Assert.Throws<ArgumentException>(() => new InvoiceValidationReceipt(
            invoice.Id, 1, "edition", "bad", At, false));
        Assert.Throws<ArgumentException>(() => new InvoiceInternalFinalizationApproval(
            invoice.Id, 1, Guid.NewGuid(), "reference", DateTime.SpecifyKind(At, DateTimeKind.Local)));
    }
}
