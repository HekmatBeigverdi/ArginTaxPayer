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
        var sessions = new IdentitySessionService(db);
        var provisioner = new IdentityAdministratorUserProvisioning(db, sessions,
            NullLogger<IdentityAdministratorUserProvisioning>.Instance,
            IdentityTestServices.CreateOwnership(db, sessions));
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
            var credentials = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
            Assert.Equal(IdentityRegistrationResult.Created, await credentials.RegisterCustomerAsync(
                email, "Disposable-Password-2026!"));
            id = await db.IdentityUsers.Where(x => x.NormalizedEmail == email.ToUpperInvariant())
                .Select(x => x.Id).SingleAsync();
            var tokens = await new IdentitySessionService(db).LoginAsync(email, "Disposable-Password-2026!");
            Assert.NotNull(tokens);
            var sessions = new IdentitySessionService(db);
            var provisioner = new IdentityAdministratorUserProvisioning(db, sessions,
                NullLogger<IdentityAdministratorUserProvisioning>.Instance,
                IdentityTestServices.CreateOwnership(db, sessions));
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
                await IdentityTestServices.CleanupCustomerFixturesAsync(db, new[] { id });
            }
        }
    }
    [Theory]
    [InlineData(IdentityRoleNames.Level1)]
    [InlineData(IdentityRoleNames.Level2)]
    public async Task MfaEnabledAdministratorCanProvisionCustomerWithoutSmsAndCannotDuplicate(string role)
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Dedicated test DB connection is required.");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test database.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, o => o.EnableRetryOnFailure()).Options;
        var created = new List<Guid>();
        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var sessions = new IdentitySessionService(db);
            var credentials = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
            var email = $"step10-admin-{Guid.NewGuid():N}@example.invalid";
            const string password = "Step10-Admin-Password-2026!";
            Assert.Equal(IdentityRegistrationResult.Created,
                await credentials.RegisterCustomerAsync(email, password));
            var administratorId = await db.IdentityUsers
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .Select(u => u.Id).SingleAsync();
            created.Add(administratorId);

            // Establish MFA while a normal Customer, then simulate only trusted
            // existing administrator bootstrap (never via public registration).
            var crypto = new IdentityMfaCrypto(
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var mfa = new IdentityMfaService(db, crypto, sessions);
            var enrollment = await mfa.BeginEnrollmentAsync(administratorId);
            Assert.NotNull(enrollment);
            var codes = await mfa.ConfirmEnrollmentAsync(administratorId,
                IdentityTotp.Generate(enrollment!.Secret, DateTime.UtcNow));
            Assert.NotNull(codes);
            db.IdentityUserRoles.Add(new IdentityUserRole {
                UserId = administratorId, Role = role, AssignedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Assert.Null(await sessions.LoginAsync(email, password));

            var challenge = await mfa.BeginLoginAsync(email, password);
            Assert.NotNull(challenge);
            var operatorTokens = await mfa.CompleteLoginAsync(
                challenge!.Challenge, codes!.Codes[0], true);
            Assert.NotNull(operatorTokens);

            var provisioner = new IdentityAdministratorUserProvisioning(db, sessions,
                NullLogger<IdentityAdministratorUserProvisioning>.Instance,
                IdentityTestServices.CreateOwnership(db, sessions));
            var mobile = "+989" + RandomNumberGenerator.GetInt32(100000000, 999999999);
            const string customerPassword = "Step10-Customer-Password-2026!";
            Assert.Equal(AdminUserCreationStatus.Invalid,
                await provisioner.CreateCustomerAsync(operatorTokens!.AccessToken,
                    "not-a-phone", null, customerPassword));
            Assert.Equal(AdminUserCreationStatus.Created,
                await provisioner.CreateCustomerAsync(operatorTokens.AccessToken,
                    mobile, null, customerPassword));
            var customer = await db.IdentityUsers.Include(u => u.Roles)
                .SingleAsync(u => u.NormalizedMobile == mobile);
            created.Add(customer.Id);
            Assert.Null(customer.NormalizedEmail);
            Assert.NotNull(customer.MobileVerifiedAtUtc);
            Assert.Single(customer.Roles);
            Assert.Equal(IdentityRoleNames.Customer, customer.Roles.Single().Role);
            Assert.Equal(AdminUserCreationStatus.Conflict,
                await provisioner.CreateCustomerAsync(operatorTokens.AccessToken,
                    mobile, null, customerPassword));
            Assert.NotNull(await sessions.LoginAsync(mobile, customerPassword));

            // An expired/revoked operator token cannot create another customer.
            Assert.True(await sessions.LogoutAsync(operatorTokens.AccessToken));
            var nextMobile = "+989" + RandomNumberGenerator.GetInt32(100000000, 999999999);
            Assert.Equal(AdminUserCreationStatus.Unauthenticated,
                await provisioner.CreateCustomerAsync(operatorTokens.AccessToken,
                    nextMobile, null, customerPassword));
            Assert.False(await db.IdentityUsers.AnyAsync(x => x.NormalizedMobile == nextMobile));
        }
        finally
        {
            if (created.Count > 0)
            {
                await using var cleanup = new ArginTaxPayerDbContext(options);
                await IdentityTestServices.CleanupCustomerFixturesAsync(cleanup, created);
            }
        }
    }

}
