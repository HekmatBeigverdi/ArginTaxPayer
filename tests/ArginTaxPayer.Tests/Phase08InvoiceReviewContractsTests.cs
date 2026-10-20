using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase08InvoiceReviewContractsTests
{
    private static readonly DateTime Utc = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid InvoiceId = Guid.Parse("24ea879b-ae8e-4b04-b5d2-35da647894b1");
    private static readonly Guid Actor = Guid.Parse("b4e7fa80-4d9e-4a9d-9bb8-34fc677847d0");

    [Fact]
    public void Happy_path_and_changes_resubmission_are_revisioned_and_immutable()
    {
        var draft = InvoiceReviewSnapshot.NewDraft(InvoiceId, Utc);
        var reviewing = draft.Propose(InvoiceReviewAction.SubmitForReview, Actor, 1, Utc.AddMinutes(1));
        var returned = reviewing.Propose(InvoiceReviewAction.RequestChanges, Actor, 2, Utc.AddMinutes(2), "Please revise");
        var resubmitted = returned.Propose(InvoiceReviewAction.SubmitForReview, Actor, 3, Utc.AddMinutes(3));
        var approved = resubmitted.Propose(InvoiceReviewAction.Approve, Actor, 4, Utc.AddMinutes(4));
        Assert.Equal(InvoiceReviewState.Draft, draft.State);
        Assert.Equal(1, draft.Revision);
        Assert.Equal(InvoiceReviewState.InReview, reviewing.State);
        Assert.Equal(InvoiceReviewState.ChangesRequested, returned.State);
        Assert.Equal("Please revise", returned.ChangeReason);
        Assert.Equal(InvoiceReviewState.InReview, resubmitted.State);
        Assert.Null(resubmitted.ChangeReason);
        Assert.Equal(InvoiceReviewState.Approved, approved.State);
        Assert.Equal(5, approved.Revision);
        Assert.Equal(Actor, approved.LastActorUserId);
        Assert.Equal(InvoiceId, approved.InvoiceId);
    }

    [Fact]
    public void Every_illegal_transition_and_approved_replay_is_rejected()
    {
        foreach (var state in Enum.GetValues<InvoiceReviewState>())
            foreach (var action in Enum.GetValues<InvoiceReviewAction>())
            {
                var allowed = (state is InvoiceReviewState.Draft or InvoiceReviewState.ChangesRequested &&
                    action == InvoiceReviewAction.SubmitForReview) ||
                    (state == InvoiceReviewState.InReview &&
                    action is InvoiceReviewAction.RequestChanges or InvoiceReviewAction.Approve);
                Assert.Equal(allowed, InvoiceReviewPolicy.CanApply(state, action));
            }
        var submitted = InvoiceReviewSnapshot.NewDraft(InvoiceId, Utc)
            .Propose(InvoiceReviewAction.SubmitForReview, Actor, 1, Utc.AddMinutes(1));
        var approved = submitted.Propose(InvoiceReviewAction.Approve, Actor, 2, Utc.AddMinutes(2));
        Assert.Throws<InvalidOperationException>(() =>
            approved.Propose(InvoiceReviewAction.Approve, Actor, 3, Utc.AddMinutes(3)));
        Assert.Throws<InvalidOperationException>(() =>
            submitted.Propose(InvoiceReviewAction.SubmitForReview, Actor, 2, Utc.AddMinutes(2)));
    }

    [Fact]
    public void Stale_revision_invalid_actor_clock_or_reason_fail_without_mutation()
    {
        var draft = InvoiceReviewSnapshot.NewDraft(InvoiceId, Utc);
        Assert.Throws<InvalidOperationException>(() => draft.Propose(
            InvoiceReviewAction.SubmitForReview, Actor, 2, Utc));
        Assert.Throws<ArgumentException>(() => draft.Propose(
            InvoiceReviewAction.SubmitForReview, Guid.Empty, 1, Utc));
        Assert.Throws<ArgumentException>(() => draft.Propose(
            InvoiceReviewAction.SubmitForReview, Actor, 1, Utc.AddMinutes(-1)));
        Assert.Throws<ArgumentException>(() => draft.Propose(
            InvoiceReviewAction.SubmitForReview, Actor, 1, Utc, "unexpected"));
        var reviewing = draft.Propose(InvoiceReviewAction.SubmitForReview, Actor, 1, Utc);
        Assert.Throws<ArgumentException>(() => reviewing.Propose(
            InvoiceReviewAction.RequestChanges, Actor, 2, Utc, " "));
        Assert.Throws<ArgumentException>(() => reviewing.Propose(
            InvoiceReviewAction.RequestChanges, Actor, 2, Utc, "bad\nreason"));
        Assert.Equal(InvoiceReviewState.Draft, draft.State);
        Assert.Equal(1, draft.Revision);
        Assert.Equal(InvoiceReviewState.InReview, reviewing.State);
        Assert.Equal(2, reviewing.Revision);
    }

    [Fact]
    public void Review_approval_never_changes_fiscal_or_phase07_lifecycle_state()
    {
        var invoice = InvoiceAggregate.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            null, Utc, Utc, new InvoicePartySnapshot(null, "seller"));
        var approved = InvoiceReviewSnapshot.NewDraft(invoice.Id, Utc)
            .Propose(InvoiceReviewAction.SubmitForReview, Actor, 1, Utc)
            .Propose(InvoiceReviewAction.Approve, Actor, 2, Utc);
        Assert.Equal(InvoiceReviewState.Approved, approved.State);
        Assert.Equal(InvoiceLifecycleState.Draft, invoice.State);
        Assert.Equal(TaxAuthorityProcessingState.NotSubmitted, invoice.AuthorityState);
        Assert.Null(invoice.FinalizationApproval);
    }
}
