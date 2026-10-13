using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityAdminProvisioningTests
{
    [Fact]
    public async Task AnonymousOperatorCannotProvisionCustomers()
    {
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=unused;Password=unused").Options);
        var provisioner = new IdentityAdministratorUserProvisioning(db,
            new IdentitySessionService(db), NullLogger<IdentityAdministratorUserProvisioning>.Instance);
        Assert.Equal(AdminUserCreationStatus.Unauthenticated,
            await provisioner.CreateCustomerAsync(null, "09123456789", null, "TestPassword123!"));
    }

    [Fact]
    public async Task CustomerCannotProvisionOtherCustomersEvenWithValidSession()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test connection required");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test DB");
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options;
        Guid id = Guid.Empty;
        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var email = "admin-create-deny-" + Guid.NewGuid().ToString("N") + "@example.invalid";
            var credentials = new IdentityCredentialService(db);
            Assert.Equal(IdentityRegistrationResult.Created, await credentials.RegisterCustomerAsync(
                email, "Disposable-Password-2026!"));
            id = await db.IdentityUsers.Where(x => x.NormalizedEmail == email.ToUpperInvariant())
                .Select(x => x.Id).SingleAsync();
            var tokens = await new IdentitySessionService(db).LoginAsync(email, "Disposable-Password-2026!");
            Assert.NotNull(tokens);
            var provisioner = new IdentityAdministratorUserProvisioning(db,
                new IdentitySessionService(db), NullLogger<IdentityAdministratorUserProvisioning>.Instance);
            var mobile = "+989" + RandomNumberGenerator.GetInt32(100000000, 999999999);
            Assert.Equal(AdminUserCreationStatus.Forbidden, await provisioner.CreateCustomerAsync(
                tokens!.AccessToken, mobile, null, "Disposable-Password-2026!"));
            Assert.False(await db.IdentityUsers.AnyAsync(x => x.NormalizedMobile == mobile));
        }
        finally
        {
            if (id != Guid.Empty)
            {
                await using var db = new ArginTaxPayerDbContext(options);
                await db.IdentityUsers.Where(x => x.Id == id).ExecuteDeleteAsync();
            }
        }
    }
}
