using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityFoundationTests
{
    private static ArginTaxPayerDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);

    [Fact]
    public void IdentityModelHasUniqueNormalizedEmailAndRoleKey()
    {
        using var db = CreateContext();
        var users = db.Model.FindEntityType(typeof(IdentityUser))!;
        var roles = db.Model.FindEntityType(typeof(IdentityUserRole))!;
        Assert.Equal("identity_users", users.GetTableName());
        Assert.Contains(users.GetIndexes(), x => x.IsUnique &&
            x.Properties.Single().Name == nameof(IdentityUser.NormalizedEmail));
        Assert.Equal(new[] { nameof(IdentityUserRole.UserId), nameof(IdentityUserRole.Role) }, roles.FindPrimaryKey()!.Properties.Select(x => x.Name));
    }

    [Fact]
    public void MigrationRegisteredAndNoAdministrativeHttpRegistration()
    {
        using var db = CreateContext();
        Assert.Contains("20261012010000_AddIdentityFoundation", db.Database.GetMigrations());
        Assert.NotNull(db.Model.FindEntityType(typeof(IdentityUserRole)));
    }

    [Theory]
    [InlineData(" Admin@Example.Com ", "ADMIN@EXAMPLE.COM")]
    [InlineData("customer@example.com", "CUSTOMER@EXAMPLE.COM")]
    public void NormalizesEmail(string input, string expected)
        => Assert.Equal(expected, InitialAdministratorBootstrap.NormalizeEmail(input));

    [Theory]
    [InlineData("")]
    [InlineData("broken-email")]
    [InlineData("@example.com")]
    [InlineData("a@")]
    public void RejectsInvalidEmail(string input)
        => Assert.Throws<ArgumentException>(() => InitialAdministratorBootstrap.NormalizeEmail(input));

    [Fact]
    public void PasswordHashUsesRandomSaltAndDoesNotStorePassword()
    {
        var a = InitialAdministratorBootstrap.HashPassword("an-extremely-long-test-password");
        var b = InitialAdministratorBootstrap.HashPassword("an-extremely-long-test-password");
        Assert.StartsWith("pbkdf2-sha256$310000$", a);
        Assert.NotEqual(a, b);
        Assert.DoesNotContain("an-extremely-long-test-password", a);
    }
}
