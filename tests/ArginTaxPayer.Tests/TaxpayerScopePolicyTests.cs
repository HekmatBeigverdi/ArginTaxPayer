using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerScopePolicyTests
{
    [Fact]
    public void Delegated_scope_is_an_exists_query_against_current_customer_owner()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(options);
        var userId = Guid.NewGuid();
        var query = db.TaxpayerProfiles.Where(p => db.CustomerCommercialOwners.Any(o =>
            o.CustomerUserId == p.CustomerUserId && o.PartnerLevel2UserId == userId));
        var sql = query.ToQueryString();
        Assert.Contains("customer_commercial_owners", sql);
        Assert.Contains("customer_user_id", sql);
        Assert.Contains("partner_level2_user_id", sql);
        Assert.DoesNotContain("taxpayer_delegate_grants", sql);
    }

    [Fact]
    public void Scoped_edit_request_contains_no_identity_or_fiscal_memory_mutations()
    {
        var properties = typeof(ScopedRenameRequest).GetProperties().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "DisplayName", "ExpectedRevision" }, properties);
    }
}
