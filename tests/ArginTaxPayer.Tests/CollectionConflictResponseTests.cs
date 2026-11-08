using System.Text.Json;
using ArginTaxPayer.Api.Subscriptions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CollectionConflictResponseTests
{
    [Theory]
    [InlineData("Payment reference reused with different details.", "collection_reference_reused", "مرجع رسید")]
    [InlineData("Invalid collection amount.", "collection_amount_exceeds_balance", "مانده")]
    [InlineData("Order not collectible.", "collection_order_not_collectible", "سفارش")]
    [InlineData("Order missing.", "collection_order_missing", "سفارش")]
    public void Expected_conflicts_explain_how_to_correct_the_receipt(string reason, string code, string hint)
    {
        var result = Phase12CollectionEndpoints.CollectionConflict(new InvalidOperationException(reason));
        Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var payload = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        Assert.NotNull(payload);
        var json = JsonSerializer.SerializeToElement(payload);
        Assert.Equal(code, json.GetProperty("error").GetString());
        Assert.Contains(hint, json.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Infrastructure_failure_is_not_reported_as_a_financial_conflict_or_leaked()
    {
        var result = Phase12CollectionEndpoints.CollectionConflict(
            new InvalidOperationException("private database details"));
        Assert.Equal(500, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var json = JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.DoesNotContain("private database details", json);
    }
}
