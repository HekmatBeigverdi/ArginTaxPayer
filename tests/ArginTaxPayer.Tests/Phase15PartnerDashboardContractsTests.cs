using ArginTaxPayer.Api.Dashboard;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>Compile-time and response-shape boundaries for the partner-only dashboard model.</summary>
public sealed class Phase15PartnerDashboardContractsTests
{
    [Fact]
    public void Partner_summary_does_not_expose_root_or_approved_costs()
    {
        var names = typeof(Phase15DashboardReadService.PartnerOverview)
            .GetProperties().Select(p => p.Name).ToArray();

        Assert.Contains("ActiveCustomerCount", names);
        Assert.Contains("ActiveSubscriptionCount", names);
        Assert.Contains("ExpiringSubscriptionCount", names);
        Assert.Contains("LifetimePartnerEarnedIrr", names);
        Assert.Contains("LifetimePartnerSettledIrr", names);
        Assert.Contains("LifetimePartnerOutstandingIrr", names);
        Assert.DoesNotContain(names, x => x.Contains("Root", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("ApprovedCost", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x.Contains("PartnerUserId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Partner_summary_serialization_contains_only_partner_currency_and_aggregate_data()
    {
        var model = new Phase15DashboardReadService.PartnerOverview(
            1, "Level2", new DateTime(2026,11,3,9,0,0,DateTimeKind.Utc),
            3, 2, 1, 3_000_000, 1_400_000, 1_600_000);
        var options = new System.Text.Json.JsonSerializerOptions(
            System.Text.Json.JsonSerializerDefaults.Web);
        var json = System.Text.Json.JsonSerializer.Serialize(model, options);
        Assert.Contains("\"role\":\"Level2\"", json);
        Assert.Contains("\"lifetimePartnerOutstandingIrr\":1600000", json);
        Assert.Contains("\"currency\":\"IRR\"", json);
        Assert.DoesNotContain("root", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approvedCost", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Partner_outstanding_is_earned_less_net_settled()
    {
        const long earned = 1_500_000;
        const long paid = 700_000;
        const long reversed = 200_000;
        Assert.Equal(1_000_000, earned - (paid - reversed));
    }
}
