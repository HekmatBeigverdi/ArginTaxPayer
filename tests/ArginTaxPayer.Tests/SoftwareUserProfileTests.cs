using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class SoftwareUserProfileTests
{
    [Fact] public void Empty_profile_is_not_complete()
    {
        Assert.False(SoftwareUserProfileService.Complete(null));
        Assert.False(SoftwareUserProfileService.Complete(new SoftwareUserProfile
        {FirstName="علی",LastName="سهرابی",Address=""}));
        Assert.True(SoftwareUserProfileService.Complete(new SoftwareUserProfile
        {FirstName="علی",LastName="سهرابی",Address="تهران"}));
    }
    [Fact] public void Software_login_profile_is_a_distinct_entity_with_restricted_owner_and_logo()
    {
        using var db=new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=never;Username=never;Password=never").Options);
        var entity=db.Model.FindEntityType(typeof(SoftwareUserProfile))!;
        Assert.Equal("software_user_profiles",entity.GetTableName());
        Assert.Equal("bytea",entity.FindProperty("InvoiceLogoPng")!.GetColumnType());
        Assert.Contains(entity.GetForeignKeys(),fk=>
            fk.PrincipalEntityType.ClrType==typeof(IdentityUser)&&
            fk.DeleteBehavior==DeleteBehavior.Restrict);
    }
}
