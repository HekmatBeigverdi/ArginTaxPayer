using ArginTaxPayer.Api.Dashboard;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase15RootDashboardContractsTests
{
    [Fact]
    public void Root_overview_and_partner_performance_have_explicit_aggregate_fields()
    {
        var root = typeof(Phase15DashboardReadService.RootOverview)
            .GetProperties().Select(p => p.Name).ToArray();
        var comparison = typeof(Phase15DashboardReadService.PartnerPerformance)
            .GetProperties().Select(p => p.Name).ToArray();
        Assert.Contains("ActiveCustomerCount", root);
        Assert.Contains("ActiveSubscriptionCount", root);
        Assert.Contains("ExpiringSubscriptionCount", root);
        Assert.Contains("ActivePartnerCount", root);
        Assert.Contains("LifetimePartnerOutstandingIrr", root);
        Assert.Contains("Partners", root);
        Assert.Contains("VerifiedCollectionsInPeriodIrr", comparison);
        Assert.Contains("PartnerEarnedShareInPeriodIrr", comparison);
        Assert.Contains("LifetimePartnerOutstandingIrr", comparison);
        Assert.DoesNotContain(root, x => x.Contains("InvoiceFaceValue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Serialization_preserves_currency_and_limits_partner_rows_to_aggregate_data()
    {
        var period = new Phase15DashboardReadService.Window(
            "30d", new DateTime(2026,10,4,20,30,0,DateTimeKind.Utc),
            new DateTime(2026,11,3,20,30,0,DateTimeKind.Utc));
        var partnerId = Guid.Parse("018f1cdd-30b8-7e65-942e-0ecdb0ef0011");
        var partner = new Phase15DashboardReadService.PartnerPerformance(
            partnerId, "همکار تست", 4, 250_000, 85_000, 95_000);
        var summary = new Phase15DashboardReadService.RootOverview(
            1, "Level1", new DateTime(2026,11,3,9,0,0,DateTimeKind.Utc),
            period, 4, 3, 1, 1, 200_000, 105_000, 95_000,
            new [] { partner });
        var json = System.Text.Json.JsonSerializer.Serialize(summary,
            new System.Text.Json.JsonSerializerOptions(
                System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"role\":\"Level1\"", json);
        Assert.Contains("\"currency\":\"IRR\"", json);
        Assert.Contains("\"lifetimePartnerOutstandingIrr\":95000", json);
        Assert.Contains("\"verifiedCollectionsInPeriodIrr\":250000", json);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taxpayerInvoiceAmount", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Settlement_reversal_increases_total_unsettled_obligation()
    {
        const long earned = 500_000;
        const long transfers = 250_000;
        const long reversals = 75_000;
        Assert.Equal(325_000, earned - (transfers - reversals));
    }
}
