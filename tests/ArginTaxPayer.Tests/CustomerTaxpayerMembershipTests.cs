using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerTaxpayerMembershipTests
{
    [Fact]
    public void Membership_is_a_distinct_entity_with_restrictive_foreign_keys()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        var entity = db.Model.FindEntityType(typeof(CustomerTaxpayerMembership))!;
        Assert.Equal("customer_taxpayer_memberships", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(CustomerTaxpayerMembership.Revision))!.IsConcurrencyToken);
        Assert.Equal(4, entity.GetForeignKeys().Count());
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void Only_one_active_link_per_customer_and_taxpayer()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        var entity = db.Model.FindEntityType(typeof(CustomerTaxpayerMembership))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "unlinked_at_utc IS NULL" &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(CustomerTaxpayerMembership.CustomerUserId),
                  nameof(CustomerTaxpayerMembership.TaxpayerId) }));
        Assert.True(entity.FindProperty(nameof(CustomerTaxpayerMembership.UnlinkedAtUtc))!.IsNullable);
    }
}
