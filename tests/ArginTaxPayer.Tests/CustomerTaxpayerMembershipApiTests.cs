using System.Net;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerTaxpayerMembershipApiTests
{
    [Fact]
    public async Task Membership_routes_are_not_exposed_without_dev_database()
    {
        using var factory = new DatabaseUnconfiguredApiFactory();
        using var client = factory.CreateClient();
        var customerId = Guid.NewGuid();
        var response = await client.GetAsync(
            $"/api/dev/customers/{customerId}/taxpayer-memberships");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
