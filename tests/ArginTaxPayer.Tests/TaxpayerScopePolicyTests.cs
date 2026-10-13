using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerScopePolicyTests
{
    [Fact]
    public void Delegated_scope_is_an_exists_query_against_live_grants()
    {
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(options);
        var userId = Guid.NewGuid();
        var query = db.TaxpayerProfiles.Where(p => db.TaxpayerDelegateGrants.Any(g =>
            g.TaxpayerId == p.Id && g.DelegateUserId == userId && g.RevokedAtUtc == null));
        var sql = query.ToQueryString();
        Assert.Contains("taxpayer_delegate_grants", sql);
        Assert.Contains("revoked_at_utc", sql);
        Assert.Contains("delegate_user_id", sql);
    }

    [Fact]
    public void Scoped_edit_request_contains_no_identity_or_fiscal_memory_mutations()
    {
        var properties = typeof(ScopedRenameRequest).GetProperties().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "DisplayName", "ExpectedRevision" }, properties);
    }
}
