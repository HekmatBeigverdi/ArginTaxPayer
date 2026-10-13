using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class CustomerCommercialOwnershipTests
{
    [Theory]
    [InlineData(CustomerAcquisitionChannel.PublicSelfRegistration, false)]
    [InlineData(CustomerAcquisitionChannel.Level1Created, false)]
    [InlineData(CustomerAcquisitionChannel.Level2Created, true)]
    public void Only_level2_created_customer_has_initial_partner(
        CustomerAcquisitionChannel channel, bool assigned)
    {
        var partner = Guid.NewGuid();
        Assert.Equal(assigned ? partner : (Guid?)null,
            CustomerCommercialOwnershipPolicy.InitialPartner(channel, partner));
    }

    [Fact]
    public void Referral_after_public_signup_requires_level1_approval()
    {
        Assert.True(CustomerCommercialOwnershipPolicy.RequiresLevel1Approval(
            CustomerAcquisitionChannel.Level2ReferralApprovedByLevel1));
        Assert.False(CustomerCommercialOwnershipPolicy.RequiresLevel1Approval(
            CustomerAcquisitionChannel.PublicSelfRegistration));
    }

    [Fact]
    public void Ownership_is_per_customer_and_taxpayers_have_customer_fk()
    {
        var opts = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(opts);
        var owner = db.Model.FindEntityType(typeof(CustomerCommercialOwner))!;
        Assert.Equal("customer_commercial_owners", owner.GetTableName());
        Assert.Equal("CustomerUserId", owner.FindPrimaryKey()!.Properties.Single().Name);
        Assert.All(owner.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        var taxpayer = db.Model.FindEntityType(typeof(TaxpayerProfile))!;
        var membership = taxpayer.GetForeignKeys().Single(fk =>
            fk.Properties.Any(p => p.Name == "CustomerUserId"));
        Assert.Equal(typeof(ArginTaxPayer.Infrastructure.Persistence.Identity.IdentityUser),
            membership.PrincipalEntityType.ClrType);
        Assert.False(membership.IsRequired);
    }

    [Fact]
    public void History_is_versioned_and_not_situated_on_a_taxpayer()
    {
        var opts = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=unused;Password=unused").Options;
        using var db = new ArginTaxPayerDbContext(opts);
        var history = db.Model.FindEntityType(typeof(CustomerCommercialOwnerChange))!;
        Assert.Contains(history.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
                new[] { "CustomerUserId", "ToRevision" }));
        Assert.Null(typeof(CustomerCommercialOwner).GetProperty("TaxpayerId"));
    }
}
