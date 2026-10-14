using System.Net;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerProfileApiTests
{
    [Fact]
    public async Task Customer_profile_route_is_not_exposed_without_database()
    {
        using var factory = new DatabaseUnconfiguredApiFactory();
        using var client = factory.CreateClient();
        var result = await client.GetAsync($"/api/dev/customers/{Guid.NewGuid()}/profile");
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }
}
