using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Domain.Invoices;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase10RetrySafetyTests
{
    [Theory]
    [InlineData(InvoiceTransportFailureKind.BeforeNetworkAttempt)]
    [InlineData(InvoiceTransportFailureKind.LocalPreparationFailed)]
    public void Proven_pre_network_failure_is_eligible_but_does_not_automatically_send(
        InvoiceTransportFailureKind failure)
    {
        var result = Phase10RetrySafetyPolicy.Classify(InvoiceDispatchState.Reserved, failure);
        Assert.Equal(InvoiceRetryDisposition.SafeToQueueWithoutNetworkAttempt, result.Disposition);
        Assert.Equal(InvoiceDispatchEvent.FailBeforeSend, result.ProposedEvent);
        Assert.Equal(InvoiceDispatchState.RetryEligible, result.ProposedState);
        Assert.False(result.MayAutomaticallyResend);
        Assert.False(result.IsFiscalSubmissionAuthorized);
    }

    [Theory]
    [InlineData(InvoiceTransportFailureKind.TimeoutAfterStart)]
    [InlineData(InvoiceTransportFailureKind.ConnectionLostAfterStart)]
    [InlineData(InvoiceTransportFailureKind.ProviderUnavailableAfterStart)]
    [InlineData(InvoiceTransportFailureKind.RateLimitedAfterStart)]
    [InlineData(InvoiceTransportFailureKind.MalformedProviderResponse)]
    [InlineData(InvoiceTransportFailureKind.ProcessInterrupted)]
    [InlineData(InvoiceTransportFailureKind.UnknownAfterStart)]
    public void Ambiguous_network_outcome_always_requires_reconciliation(
        InvoiceTransportFailureKind failure)
    {
        var result = Phase10RetrySafetyPolicy.Classify(InvoiceDispatchState.Sending, failure);
        Assert.Equal(InvoiceRetryDisposition.ReconciliationRequired, result.Disposition);
        Assert.Equal(InvoiceDispatchEvent.OutcomeUnknown, result.ProposedEvent);
        Assert.Equal(InvoiceDispatchState.Indeterminate, result.ProposedState);
        Assert.False(result.MayAutomaticallyResend);
        Assert.True(Phase10RetrySafetyPolicy.TryPlanPersistedTransition(
            InvoiceDispatchState.Sending, InvoiceDispatchEvent.OutcomeUnknown, 4, out var next));
        Assert.Equal(InvoiceDispatchState.Indeterminate, next);
    }

    [Fact]
    public void Reserved_but_ambiguous_preparation_cannot_skip_sending_state()
    {
        var result = Phase10RetrySafetyPolicy.Classify(
            InvoiceDispatchState.Reserved, InvoiceTransportFailureKind.TimeoutAfterStart);
        Assert.Equal(InvoiceRetryDisposition.ReconciliationRequired, result.Disposition);
        Assert.Null(result.ProposedEvent);
        Assert.Null(result.ProposedState);
    }

    [Fact]
    public void Indeterminate_must_never_be_retried_blindly()
    {
        var r = Phase10RetrySafetyPolicy.Classify(
            InvoiceDispatchState.Indeterminate, InvoiceTransportFailureKind.ProcessInterrupted);
        Assert.Equal(InvoiceRetryDisposition.ReconciliationRequired, r.Disposition);
        Assert.Null(r.ProposedEvent);
        Assert.False(r.MayAutomaticallyResend);
        Assert.False(Phase10RetrySafetyPolicy.TryPlanPersistedTransition(
            InvoiceDispatchState.Indeterminate, InvoiceDispatchEvent.RetryFromEvidence, 2, out _));
        Assert.False(InvoiceDispatchPolicy.TryTransition(
            InvoiceDispatchState.Indeterminate, InvoiceDispatchEvent.BeginSend, out _));
    }

    [Theory]
    [InlineData(InvoiceDispatchState.Acknowledged)]
    [InlineData(InvoiceDispatchState.FailedTerminal)]
    public void Terminal_states_never_retry(InvoiceDispatchState state)
    {
        var result = Phase10RetrySafetyPolicy.Classify(state,
            InvoiceTransportFailureKind.TimeoutAfterStart);
        Assert.Equal(InvoiceRetryDisposition.NoRetryTerminal, result.Disposition);
        Assert.Null(result.ProposedEvent);
    }

    [Fact]
    public void Invalid_event_and_revision_fail_closed()
    {
        Assert.False(Phase10RetrySafetyPolicy.TryPlanPersistedTransition(
            InvoiceDispatchState.Reserved, InvoiceDispatchEvent.BeginSend, 1, out _));
        Assert.False(Phase10RetrySafetyPolicy.TryPlanPersistedTransition(
            InvoiceDispatchState.Reserved, InvoiceDispatchEvent.FailBeforeSend, 0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Phase10RetrySafetyPolicy.Classify(InvoiceDispatchState.Sending,
                (InvoiceTransportFailureKind)12345));
    }

    [Fact]
    public void Database_transition_record_has_no_send_or_retry_authorization()
    {
        Assert.Equal(1, (int)InvoiceDispatchState.Prepared);
        Assert.Equal(6, (int)InvoiceDispatchState.Indeterminate);
        Assert.True(InvoiceDispatchStoragePolicy.RequiresExternalResolution(
            InvoiceDispatchState.Indeterminate));
        Assert.False(InvoiceDispatchStoragePolicy.CanRedriveOutboxWithoutReconciliation(
            InvoiceDispatchState.Indeterminate));
    }
}
