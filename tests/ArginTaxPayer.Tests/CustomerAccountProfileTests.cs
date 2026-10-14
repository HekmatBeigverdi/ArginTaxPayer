using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerAccountProfileTests
{
    [Fact]
    public void Profile_is_separate_from_login_and_party_with_restrictive_keys()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        var model = db.Model.FindEntityType(typeof(CustomerAccountProfile))!;
        Assert.Equal("customer_account_profiles", model.GetTableName());
        Assert.Equal(nameof(CustomerAccountProfile.CustomerUserId), model.FindPrimaryKey()!.Properties.Single().Name);
        Assert.True(model.FindProperty(nameof(CustomerAccountProfile.Revision))!.IsConcurrencyToken);
        Assert.True(model.FindProperty(nameof(CustomerAccountProfile.PreferredPartyId))!.IsNullable);
        Assert.Equal(2, model.GetForeignKeys().Count());
        Assert.All(model.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Equal(256, model.FindProperty(nameof(CustomerAccountProfile.DisplayName))!.GetMaxLength());
    }

    [Fact]
    public void Profile_status_is_independent_of_identity_active_flag()
    {
        Assert.Equal(1, (int)CustomerAccountProfileStatus.Active);
        Assert.Equal(2, (int)CustomerAccountProfileStatus.Suspended);
    }
}
