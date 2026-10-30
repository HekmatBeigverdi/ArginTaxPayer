using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10DispatchContractsTests
{
    private static InvoiceDispatchSnapshot New() => InvoiceDispatchSnapshot.Prepare(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        7, new string('A', 64));

    [Fact]
    public void New_preparation_preserves_frozen_identity_and_lowercase_digest()
    {
        var s = New();
        Assert.Equal(InvoiceDispatchState.Prepared, s.State);
        Assert.Equal(7, s.InvoiceRevision);
        Assert.Equal(new string('a', 64), s.PayloadSha256);
        Assert.Equal(1, s.Revision);
    }

    [Fact]
    public void Normal_dispatch_reaches_receipt_without_claiming_fiscal_acceptance()
    {
        var s = New();
        foreach (var evt in new[] { InvoiceDispatchEvent.Sign, InvoiceDispatchEvent.Reserve,
            InvoiceDispatchEvent.BeginSend, InvoiceDispatchEvent.ReceiptReceived })
            s = s.Propose(evt, s.Revision);

        Assert.Equal(InvoiceDispatchState.Acknowledged, s.State);
        Assert.Equal(5, s.Revision);
        Assert.True(InvoiceDispatchPolicy.IsReceiptOnly(s.State));
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            s.State, InvoiceDispatchEvent.BeginSend, out _));
        Assert.Equal(7, s.InvoiceRevision);
    }

    [Fact]
    public void Timeout_requires_reconciliation_before_another_send()
    {
        var s = New();
        foreach (var evt in new[] { InvoiceDispatchEvent.Sign, InvoiceDispatchEvent.Reserve,
            InvoiceDispatchEvent.BeginSend, InvoiceDispatchEvent.OutcomeUnknown })
            s = s.Propose(evt, s.Revision);

        Assert.True(InvoiceDispatchPolicy.RequiresReconciliation(s.State));
        Assert.Throws<InvalidOperationException>(() =>
            s.Propose(InvoiceDispatchEvent.BeginSend, s.Revision));
        Assert.Throws<InvalidOperationException>(() =>
            s.Propose(InvoiceDispatchEvent.RetryFromEvidence, s.Revision));
        s = s.Propose(InvoiceDispatchEvent.ReconcileNotReceived, s.Revision);
        Assert.Equal(InvoiceDispatchState.RetryEligible, s.State);
        s = s.Propose(InvoiceDispatchEvent.RetryFromEvidence, s.Revision);
        Assert.Equal(InvoiceDispatchState.Reserved, s.State);
    }

    [Fact]
    public void Reconciliation_with_existing_receipt_never_reenters_queue()
    {
        var s = New();
        foreach (var evt in new[] { InvoiceDispatchEvent.Sign, InvoiceDispatchEvent.Reserve,
            InvoiceDispatchEvent.BeginSend, InvoiceDispatchEvent.OutcomeUnknown,
            InvoiceDispatchEvent.ReconcileReceipt })
            s = s.Propose(evt, s.Revision);
        Assert.Equal(InvoiceDispatchState.Acknowledged, s.State);
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            s.State, InvoiceDispatchEvent.RetryFromEvidence, out _));
    }

    [Fact]
    public void A_definitive_rejection_is_terminal()
    {
        var s = New();
        foreach (var evt in new[] { InvoiceDispatchEvent.Sign, InvoiceDispatchEvent.Reserve,
            InvoiceDispatchEvent.BeginSend, InvoiceDispatchEvent.DefinitiveFailure })
            s = s.Propose(evt, s.Revision);
        Assert.Equal(InvoiceDispatchState.FailedTerminal, s.State);
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            s.State, InvoiceDispatchEvent.BeginSend, out _));
    }

    [Fact]
    public void Stale_revision_is_rejected_without_source_mutation()
    {
        var s = New();
        var next = s.Propose(InvoiceDispatchEvent.Sign, 1);
        Assert.Equal(InvoiceDispatchState.Prepared, s.State);
        Assert.Equal(1, s.Revision);
        Assert.Throws<InvalidOperationException>(() =>
            next.Propose(InvoiceDispatchEvent.Reserve, 1));
        Assert.Equal(InvoiceDispatchState.Signed, next.State);
    }

    [Fact]
    public void Invalid_identity_hash_and_unknown_enums_fail_closed()
    {
        Assert.Throws<ArgumentException>(() => InvoiceDispatchSnapshot.Prepare(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, new string('a', 64)));
        Assert.Throws<ArgumentException>(() => InvoiceDispatchSnapshot.Prepare(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            0, "abc"));
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            (InvoiceDispatchState)999, InvoiceDispatchEvent.Sign, out _));
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            InvoiceDispatchState.Prepared, (InvoiceDispatchEvent)999, out _));
    }

    [Fact]
    public void Typed_errors_expose_stable_safe_keys_without_raw_provider_data()
    {
        var e = InvoiceDispatchError.FromCode(InvoiceDispatchFailureCode.Timeout);
        Assert.Equal(InvoiceDispatchErrorCategory.Uncertain, e.Category);
        Assert.Equal("invoice.dispatch.outcome_unknown", e.SafeMessageKey);
        Assert.True(e.RequiresOperatorReview);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InvoiceDispatchError.FromCode(InvoiceDispatchFailureCode.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InvoiceDispatchError.FromCode((InvoiceDispatchFailureCode)999));
    }
}
