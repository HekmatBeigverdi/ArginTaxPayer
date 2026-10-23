using ArginTaxPayer.Domain.Taxpayers;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase09TaxpayerCapacityPolicyTests
{
    [Fact]
    public void Capacity_is_temporarily_unrestricted_before_plan_enforcement()
    {
        var allowed = TaxpayerCapacityPolicy.IsAllowed(
            planCapacityConfigured: false,
            activeTaxpayerCount: 25,
            maximumTaxpayers: null);

        Assert.True(allowed);
    }

    [Fact]
    public void Configured_single_taxpayer_plan_allows_the_first_taxpayer()
    {
        var allowed = TaxpayerCapacityPolicy.IsAllowed(
            planCapacityConfigured: true,
            activeTaxpayerCount: 0,
            maximumTaxpayers: 1);

        Assert.True(allowed);
    }

    [Fact]
    public void Configured_single_taxpayer_plan_blocks_the_second_taxpayer()
    {
        var allowed = TaxpayerCapacityPolicy.IsAllowed(
            planCapacityConfigured: true,
            activeTaxpayerCount: 1,
            maximumTaxpayers: 1);

        Assert.False(allowed);
    }

    [Fact]
    public void Configured_multi_taxpayer_plan_uses_the_configured_limit()
    {
        Assert.True(TaxpayerCapacityPolicy.IsAllowed(true, 2, 3));
        Assert.False(TaxpayerCapacityPolicy.IsAllowed(true, 3, 3));
    }

    [Fact]
    public void Invalid_active_count_fails_closed()
    {
        var allowed = TaxpayerCapacityPolicy.IsAllowed(
            planCapacityConfigured: false,
            activeTaxpayerCount: -1,
            maximumTaxpayers: null);

        Assert.False(allowed);
    }

    [Fact]
    public void Invalid_configured_limit_fails_closed()
    {
        var allowed = TaxpayerCapacityPolicy.IsAllowed(
            planCapacityConfigured: true,
            activeTaxpayerCount: 0,
            maximumTaxpayers: 0);

        Assert.False(allowed);
    }
}
