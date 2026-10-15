using System.Net;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerSelfServiceApiTests
{
    [Fact]
    public async Task Endpoint_is_absent_without_a_configured_development_database()
    {
        using var factory = new DatabaseUnconfiguredApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/dev/customer/self-service");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
