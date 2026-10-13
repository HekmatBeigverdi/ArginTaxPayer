using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityRolePolicyTests
{
    [Fact]
    public void PermissionsAreFailClosedAndOnlyLevel1CanManageLevel2()
    {
        Assert.True(IdentityPermissions.Grants(IdentityRoleNames.Level1,
            IdentityPermissions.ManageLevel2));
        Assert.False(IdentityPermissions.Grants(IdentityRoleNames.Level2,
            IdentityPermissions.ManageLevel2));
        Assert.False(IdentityPermissions.Grants(IdentityRoleNames.Customer,
            IdentityPermissions.ManageLevel2));
        Assert.True(IdentityPermissions.Grants(IdentityRoleNames.Level2,
            IdentityPermissions.ReadAdministration));
        Assert.False(IdentityPermissions.Grants(IdentityRoleNames.Customer,
            IdentityPermissions.ReadAdministration));
        Assert.False(IdentityPermissions.Grants("Level3", IdentityPermissions.ReadAdministration));
        Assert.False(IdentityPermissions.Grants(IdentityRoleNames.Level1, "roles.assign.level1"));
    }

    [Fact]
    public async Task InvalidBearerCannotGrantPermissions()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);
        var service = new IdentityAuthorizationService(db, new IdentitySessionService(db));
        Assert.Equal(IdentityAuthorizationResult.Unauthenticated,
            await service.CheckAsync("invalid-token", IdentityPermissions.ReadAdministration));
        Assert.Equal(Level2RoleGrantResult.Unauthenticated,
            await service.GrantLevel2Async("invalid-token", Guid.NewGuid()));
    }
}

public sealed class IdentityRolePostgresTests
{
    [Fact]
    public async Task CustomerCannotElevateAndLevel1MfaCanGrantLevel2WithRevocation()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test database connection required.");
        if (!string.Equals(new NpgsqlConnectionStringBuilder(cs).Database,
            "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing non-test database.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, n => n.EnableRetryOnFailure(maxRetryCount: 3)).Options;
        var ids = new List<Guid>();
        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var credential = new IdentityCredentialService(db);
            var sessions = new IdentitySessionService(db);
            var authorization = new IdentityAuthorizationService(db, sessions);
            var crypto = new IdentityMfaCrypto(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            var mfa = new IdentityMfaService(db, crypto, sessions);
            const string password = "Disposable-Role-Test-2026!";
            var actorEmail = "rbac-actor-" + Guid.NewGuid().ToString("N") + "@example.invalid";
            var targetEmail = "rbac-target-" + Guid.NewGuid().ToString("N") + "@example.invalid";
            Assert.Equal(IdentityRegistrationResult.Created,
                await credential.RegisterCustomerAsync(actorEmail, password));
            Assert.Equal(IdentityRegistrationResult.Created,
                await credential.RegisterCustomerAsync(targetEmail, password));
            var actorId = await db.IdentityUsers.Where(u => u.NormalizedEmail == actorEmail.ToUpperInvariant())
                .Select(u => u.Id).SingleAsync();
            var targetId = await db.IdentityUsers.Where(u => u.NormalizedEmail == targetEmail.ToUpperInvariant())
                .Select(u => u.Id).SingleAsync();
            ids.AddRange([actorId, targetId]);
            var customerToken = await sessions.LoginAsync(actorEmail, password);
            Assert.NotNull(customerToken);
            Assert.Equal(IdentityAuthorizationResult.Forbidden,
                await authorization.CheckAsync(customerToken!.AccessToken,
                    IdentityPermissions.ManageLevel2));
            Assert.Equal(Level2RoleGrantResult.Forbidden,
                await authorization.GrantLevel2Async(customerToken.AccessToken, targetId));
            Assert.Equal(Level2RoleGrantResult.Forbidden,
                await authorization.GrantLevel2Async(customerToken.AccessToken, actorId));
            Assert.Equal(IdentityAuthorizationResult.Forbidden,
                await authorization.CheckAsync(customerToken.AccessToken,
                    IdentityPermissions.ReadAdministration));

            // Simulate trusted operator bootstrap: MFA first, then privileged Level 1 grant.
            var actorEnrollment = await mfa.BeginEnrollmentAsync(actorId);
            Assert.NotNull(actorEnrollment);
            var actorRecovery = await mfa.ConfirmEnrollmentAsync(actorId,
                IdentityTotp.Generate(actorEnrollment!.Secret, DateTime.UtcNow));
            Assert.NotNull(actorRecovery);
            db.IdentityUserRoles.Add(new IdentityUserRole
            {
                UserId = actorId, Role = IdentityRoleNames.Level1, AssignedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Assert.Null(await sessions.LoginAsync(actorEmail, password));
            var actorChallenge = await mfa.BeginLoginAsync(actorEmail, password);
            Assert.NotNull(actorChallenge);
            var actorSession = await mfa.CompleteLoginAsync(
                actorChallenge!.Challenge, actorRecovery!.Codes[0], true);
            Assert.NotNull(actorSession);
            Assert.Equal(IdentityAuthorizationResult.Allowed,
                await authorization.CheckAsync(actorSession!.AccessToken,
                    IdentityPermissions.ManageLevel2));
            // Cannot elevate an account without confirmed MFA.
            Assert.Equal(Level2RoleGrantResult.Forbidden,
                await authorization.GrantLevel2Async(actorSession.AccessToken, targetId));
            var targetEnrollment = await mfa.BeginEnrollmentAsync(targetId);
            Assert.NotNull(targetEnrollment);
            var targetCodes = await mfa.ConfirmEnrollmentAsync(targetId,
                IdentityTotp.Generate(targetEnrollment!.Secret, DateTime.UtcNow));
            Assert.NotNull(targetCodes);
            var targetChallenge = await mfa.BeginLoginAsync(targetEmail, password);
            Assert.NotNull(targetChallenge);
            var targetSession = await mfa.CompleteLoginAsync(
                targetChallenge!.Challenge, targetCodes!.Codes[0], true);
            Assert.NotNull(targetSession);

            Assert.Equal(Level2RoleGrantResult.Forbidden,
                await authorization.GrantLevel2Async(targetSession!.AccessToken, actorId));
            Assert.Equal(Level2RoleGrantResult.Granted,
                await authorization.GrantLevel2Async(actorSession.AccessToken, targetId));
            Assert.Null(await sessions.AuthenticateAsync(targetSession.AccessToken));
            Assert.Null(await sessions.RefreshAsync(targetSession.RefreshToken));
            Assert.Equal(Level2RoleGrantResult.Conflict,
                await authorization.GrantLevel2Async(actorSession.AccessToken, targetId));
            Assert.Equal(1, await db.IdentityUserRoles.CountAsync(r =>
                r.UserId == targetId && r.Role == IdentityRoleNames.Level2));
            Assert.Equal(IdentityAuthorizationResult.Unauthenticated,
                await authorization.CheckAsync(targetSession.AccessToken,
                    IdentityPermissions.ReadAdministration));
            var newChallenge = await mfa.BeginLoginAsync(targetEmail, password);
            Assert.NotNull(newChallenge);
            var elevated = await mfa.CompleteLoginAsync(
                newChallenge!.Challenge, targetCodes.Codes[1], true);
            Assert.NotNull(elevated);
            Assert.Equal(IdentityAuthorizationResult.Allowed,
                await authorization.CheckAsync(elevated!.AccessToken,
                    IdentityPermissions.ReadAdministration));
            Assert.Equal(IdentityAuthorizationResult.Forbidden,
                await authorization.CheckAsync(elevated.AccessToken,
                    IdentityPermissions.ManageLevel2));
            Assert.Equal(Level2RoleGrantResult.Forbidden,
                await authorization.GrantLevel2Async(elevated.AccessToken, actorId));
        }
        finally
        {
            if (ids.Count != 0)
            {
                await using var cleanup = new ArginTaxPayerDbContext(options);
                await cleanup.IdentityUsers.Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync();
            }
        }
    }
}
