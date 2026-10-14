using ArginTaxPayer.Domain.Customers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerSelfServicePolicyTests
{
    [Theory]
    [InlineData(CustomerSelfServiceAction.ViewOwnDashboard)]
    [InlineData(CustomerSelfServiceAction.ViewOwnProfile)]
    [InlineData(CustomerSelfServiceAction.ViewOwnTaxpayers)]
    [InlineData(CustomerSelfServiceAction.ViewOwnMemberships)]
    public void Only_explicit_own_customer_reads_are_allowed(CustomerSelfServiceAction action)
    {
        Assert.True(CustomerSelfServicePolicy.IsAllowed(action, true));
        Assert.False(CustomerSelfServicePolicy.IsAllowed(action, false));
    }

    [Theory]
    [InlineData(CustomerSelfServiceAction.UpdateOwnProfile)]
    [InlineData(CustomerSelfServiceAction.CreateTaxpayer)]
    [InlineData(CustomerSelfServiceAction.UpdateTaxpayer)]
    [InlineData(CustomerSelfServiceAction.LinkTaxpayer)]
    [InlineData(CustomerSelfServiceAction.UnlinkTaxpayer)]
    [InlineData(CustomerSelfServiceAction.SubmitFiscalInvoice)]
    [InlineData((CustomerSelfServiceAction)999)]
    public void No_customer_write_or_unknown_permission_is_enabled(CustomerSelfServiceAction action)
    {
        Assert.False(CustomerSelfServicePolicy.IsAllowed(action, true));
        Assert.False(CustomerSelfServicePolicy.IsAllowed(action, false));
    }

    [Fact]
    public void Rule_version_is_explicit()
        => Assert.Equal("phase05.selfservice.v1", CustomerSelfServicePolicy.Version);
}
