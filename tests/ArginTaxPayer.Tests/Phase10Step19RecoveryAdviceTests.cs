using ArginTaxPayer.Application.Invoices;
using Xunit;
namespace ArginTaxPayer.Tests;

public sealed class Phase10Step19RecoveryAdviceTests
{
    [Theory]
    [InlineData("Sending",InvoiceRecoveryNextAction.AwaitReconciliation)]
    [InlineData("Reserved",InvoiceRecoveryNextAction.AwaitReconciliation)]
    [InlineData("Indeterminate",InvoiceRecoveryNextAction.AwaitReconciliation)]
    [InlineData("RetryEligible",InvoiceRecoveryNextAction.ManualReview)]
    [InlineData("FailedTerminal",InvoiceRecoveryNextAction.NoFurtherSend)]
    [InlineData("Acknowledged",InvoiceRecoveryNextAction.RefreshOnly)]
    [InlineData("Prepared",InvoiceRecoveryNextAction.AwaitPreparation)]
    [InlineData("BadState",InvoiceRecoveryNextAction.ManualReview)]
    public void Recovery_suggestions_never_authorize_retries_or_live_inquiry(
        string status,InvoiceRecoveryNextAction expected)
    {
        var advice=Phase10InvoiceRecoveryAdvice.ForState(status,false);
        Assert.Equal(expected,advice.NextAction);
        Assert.False(advice.MayRetry);
        Assert.False(advice.MaySend);
        Assert.False(advice.MayInquire);
        Assert.False(advice.IsFiscalAccepted);
    }

    [Fact]
    public void Draft_requires_completion_not_dispatch()
    {
        var advice=Phase10InvoiceRecoveryAdvice.ForState("Indeterminate",true);
        Assert.Equal(InvoiceRecoveryNextAction.CompleteDraft,advice.NextAction);
        Assert.False(advice.MaySend);
    }
}
