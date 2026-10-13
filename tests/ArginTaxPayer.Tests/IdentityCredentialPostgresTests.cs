using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityCredentialPostgresTests
{
    [Fact]
    public async Task CustomerRegistrationAndLoginAreDatabaseBacked()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1")
            return;

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Test connection required.");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Test must target argintaxpayer_test.");

        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        Assert.Contains("20261012010000_AddIdentityFoundation",
            await db.Database.GetAppliedMigrationsAsync());

        await using var tx = await db.Database.BeginTransactionAsync();
        var identity = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
        var email = $"step02-{Guid.NewGuid():N}@example.invalid";
        const string password = "Test-only-Customer-Password-2026!";

        Assert.Equal(IdentityRegistrationResult.Created,
            await identity.RegisterCustomerAsync(email, password));
        Assert.Equal(IdentityRegistrationResult.Conflict,
            await identity.RegisterCustomerAsync(email.ToUpperInvariant(), password));
        Assert.True(await identity.VerifyCredentialsAsync(email, password));
        Assert.False(await identity.VerifyCredentialsAsync(email, "incorrect-secret"));

        var user = await db.IdentityUsers.Include(x => x.Roles)
            .SingleAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
        Assert.Single(user.Roles);
        Assert.Equal(IdentityRoleNames.Customer, user.Roles.Single().Role);
        Assert.DoesNotContain(password, user.PasswordHash);

        user.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(await identity.VerifyCredentialsAsync(email, password));
        // Transaction is rolled back by disposal.
    }
}
