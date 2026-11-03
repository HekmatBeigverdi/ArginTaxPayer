using ArginTaxPayer.Api.Dashboard;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase15SubscriptionWatchContractsTests
{
    [Fact]
    public void Subscription_watch_response_is_aggregate_only_and_has_no_customer_secrets()
    {
        var itemFields=typeof(Phase15DashboardReadService.SubscriptionWatchItem)
            .GetProperties().Select(p=>p.Name).ToArray();
        Assert.Contains("SubscriptionId",itemFields);
        Assert.Contains("RemainingInvoices",itemFields);
        Assert.Contains("RemainingDays",itemFields);
        Assert.DoesNotContain(itemFields,x=>x.Contains("Email",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(itemFields,x=>x.Contains("Mobile",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(itemFields,x=>x.Contains("CustomerUser",StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(itemFields,x=>x.Contains("RootShare",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Watchlist_serializes_exhausted_quantity_without_negative_quota()
    {
        var entry=new Phase15DashboardReadService.SubscriptionWatchItem(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),2,
            new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc),12,0,"quota_exhausted");
        var view=new Phase15DashboardReadService.SubscriptionWatch(
            1,"Level2",new DateTime(2026,11,3,0,0,0,DateTimeKind.Utc),
            4,1,2,1,new[]{entry});
        var json=System.Text.Json.JsonSerializer.Serialize(view,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"exhaustedQuantityPlans\":1",json);
        Assert.Contains("\"remainingInvoices\":0",json);
        Assert.Contains("\"status\":\"quota_exhausted\"",json);
        Assert.DoesNotContain("normalizedEmail",json,StringComparison.OrdinalIgnoreCase);
    }
}
