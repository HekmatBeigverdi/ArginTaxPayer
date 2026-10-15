using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalDirectorySeparationTests
{
    [Fact]
    public void Fiscal_buyers_sellers_and_login_accounts_are_distinct_tables()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        Assert.Equal("identity_users", db.Model.FindEntityType(typeof(IdentityUser))!.GetTableName());
        Assert.Equal("parties", db.Model.FindEntityType(typeof(Party))!.GetTableName());
        Assert.Equal("taxpayer_profiles", db.Model.FindEntityType(typeof(TaxpayerProfile))!.GetTableName());
        Assert.Equal("customer_party_links", db.Model.FindEntityType(typeof(CustomerPartyLink))!.GetTableName());
        var buyers = db.Model.FindEntityType(typeof(Party))!;
        var sellers = db.Model.FindEntityType(typeof(TaxpayerProfile))!;
        Assert.Contains(buyers.GetIndexes(), i =>
            i.GetDatabaseName() == "ix_parties_fiscal_directory");
        Assert.Contains(sellers.GetIndexes(), i =>
            i.GetDatabaseName() == "ix_taxpayer_profiles_fiscal_directory");
    }

    [Fact]
    public void Buyer_link_and_fiscal_seller_owner_both_restrict_deletion()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        var buyerLink = db.Model.FindEntityType(typeof(CustomerPartyLink))!;
        Assert.Equal(2, buyerLink.GetForeignKeys().Count());
        Assert.All(buyerLink.GetForeignKeys(),
            fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(db.Model.FindEntityType(typeof(TaxpayerProfile))!.GetForeignKeys(),
            fk => fk.DeleteBehavior == DeleteBehavior.Restrict &&
                fk.PrincipalEntityType.ClrType == typeof(IdentityUser));
    }
}
