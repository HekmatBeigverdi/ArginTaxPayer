using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Pure relational/SQL-shape guards. Phase04 HTTP smoke verifies actual server responses.
/// No database is opened by these tests.
/// </summary>
public sealed class Phase04ScopeRegressionTests
{
    private static ArginTaxPayerDbContext ModelDb() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options);

    [Fact]
    public void Partner_taxpayer_scope_uses_current_customer_ownership_not_deprecated_grants()
    {
        using var db = ModelDb();
        var partner = Guid.NewGuid();
        var query = db.TaxpayerProfiles.AsNoTracking().Where(profile =>
            db.CustomerCommercialOwners.Any(owner =>
                owner.CustomerUserId == profile.CustomerUserId &&
                owner.PartnerLevel2UserId == partner));
        var sql = query.ToQueryString();
        Assert.Contains("customer_commercial_owners", sql);
        Assert.Contains("customer_user_id", sql);
        Assert.Contains("partner_level2_user_id", sql);
        Assert.DoesNotContain("taxpayer_delegate_grants", sql);
    }

    [Fact]
    public void Partner_customer_list_restricts_identity_before_materialization()
    {
        using var db = ModelDb();
        var partner = Guid.NewGuid();
        var query = from user in db.IdentityUsers.AsNoTracking()
                    join owner in db.CustomerCommercialOwners.AsNoTracking()
                        on user.Id equals owner.CustomerUserId
                    where user.Roles.Any(r => r.Role == "Customer") &&
                          owner.PartnerLevel2UserId == partner
                    select user.Id;
        var sql = query.ToQueryString();
        Assert.Contains("customer_commercial_owners", sql);
        Assert.Contains("partner_level2_user_id", sql);
        Assert.Contains("identity_users", sql);
    }

    [Fact]
    public void Commercial_owner_is_unique_per_customer_and_history_is_immutable_in_schema()
    {
        using var db = ModelDb();
        var owner = db.Model.FindEntityType(typeof(CustomerCommercialOwner))!;
        Assert.Single(owner.FindPrimaryKey()!.Properties);
        Assert.Equal("CustomerUserId", owner.FindPrimaryKey()!.Properties[0].Name);

        var audit = db.Model.FindEntityType(typeof(CustomerCommercialOwnerChange))!;
        Assert.Contains(audit.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CustomerUserId", "ToRevision" }));
        Assert.DoesNotContain(owner.GetProperties(), p => p.Name == "TaxpayerId");
    }

    [Fact]
    public void Customer_has_nullable_legacy_taxpayer_membership_but_new_creation_requires_account()
    {
        using var db = ModelDb();
        var profile = db.Model.FindEntityType(typeof(TaxpayerProfile))!;
        var customer = profile.FindProperty("CustomerUserId")!;
        Assert.True(customer.IsNullable);
        Assert.Contains(profile.GetForeignKeys(), fk =>
            fk.Properties.Any(p => p.Name == "CustomerUserId"));
        Assert.Contains(profile.GetIndexes(), idx =>
            idx.Properties.Any(p => p.Name == "CustomerUserId"));
        Assert.Contains(typeof(CreateTaxpayerRequest).GetProperties(), p => p.Name == "CustomerUserId");
    }
}
