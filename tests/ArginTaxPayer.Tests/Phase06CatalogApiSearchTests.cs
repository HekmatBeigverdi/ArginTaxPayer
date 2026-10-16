using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase06CatalogApiSearchTests
{
    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 101, false)]
    [InlineData(-1, 20, false)]
    [InlineData(100001, 20, false)]
    [InlineData(100000, 100, true)]
    [InlineData(1, 0, false)]
    public void Paging_contract_rejects_invalid_requests(int page, int size, bool expected)
        => Assert.Equal(expected, CatalogSearchService.ValidPage(page, size, null));

    [Fact]
    public void Overlong_search_is_rejected()
    {
        Assert.True(CatalogSearchService.ValidPage(1, 20, "نصب کولر"));
        Assert.True(CatalogSearchService.ValidPage(1, 20, null));
        Assert.False(CatalogSearchService.ValidPage(1, 20, new string('x', 121)));
    }

    [Fact]
    public void Existing_customer_services_and_products_have_separate_result_models()
    {
        Assert.NotEqual(typeof(CustomerProductView), typeof(CustomerServiceView));
        Assert.NotEqual(typeof(CustomerProductResult), typeof(CustomerServiceResult));
        Assert.Equal(CatalogQueryDecision.Unauthenticated,
            new CatalogPage<OfficialCatalogHit>(CatalogQueryDecision.Unauthenticated, [], 1, 20, false).Decision);
    }

    [Fact]
    public void Official_results_include_version_id_without_exposing_internal_customer_codes()
    {
        var fields = typeof(OfficialCatalogHit).GetProperties()
            .Select(x => x.Name).ToArray();
        Assert.Contains("ReleaseId", fields);
        Assert.Contains("Code", fields);
        Assert.Contains("Kind", fields);
        Assert.DoesNotContain("CustomerUserId", fields);
        Assert.DoesNotContain("InternalCode", fields);
    }
}
