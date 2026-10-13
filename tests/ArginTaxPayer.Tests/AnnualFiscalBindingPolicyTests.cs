using ArginTaxPayer.Domain.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class AnnualFiscalBindingPolicyTests
{
    [Theory]
    [InlineData(TaxAuthorityConnectivityResult.Success, true)]
    [InlineData(TaxAuthorityConnectivityResult.InvalidCredentialsOrToken, false)]
    [InlineData(TaxAuthorityConnectivityResult.SignatureOrKeyMismatch, false)]
    [InlineData(TaxAuthorityConnectivityResult.AuthorityTimeout, false)]
    [InlineData(TaxAuthorityConnectivityResult.AuthorityServerError, false)]
    [InlineData(TaxAuthorityConnectivityResult.NetworkOrDnsFailure, false)]
    [InlineData(TaxAuthorityConnectivityResult.Inconclusive, false)]
    public void Only_successful_authority_connection_locks_the_annual_binding(
        TaxAuthorityConnectivityResult result, bool expected)
        => Assert.Equal(expected, AnnualTaxpayerBindingPolicy.ShouldLock(result));

    [Fact]
    public void Customer_cannot_switch_taxpayer_after_a_successful_connection()
    {
        Assert.True(AnnualTaxpayerBindingPolicy.CanCustomerChangeTaxpayer(
            AnnualTaxpayerBindingStatus.Unbound));
        Assert.False(AnnualTaxpayerBindingPolicy.CanCustomerChangeTaxpayer(
            AnnualTaxpayerBindingStatus.LockedAfterSuccessfulAuthorityConnection));
        Assert.False(AnnualTaxpayerBindingPolicy.CanCustomerChangeTaxpayer(
            AnnualTaxpayerBindingStatus.ResetApprovedPendingRebind));
    }

    [Theory]
    [InlineData(FiscalBindingChangeScope.ReplaceMemorySameTaxpayer)]
    [InlineData(FiscalBindingChangeScope.ReplaceTaxpayerAndMemory)]
    public void Every_fiscal_binding_reset_requires_manual_level1_approval(
        FiscalBindingChangeScope scope)
        => Assert.True(AnnualTaxpayerBindingPolicy.RequiresLevel1ManualApproval(scope));
}
