using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Phase18: explicit opt-in and ONLY an externally provisioned disposable TLS
/// PostgreSQL DB. Never uses the shared argintaxpayer_test or Production DB.
/// </summary>
public sealed class Phase18DisposableDatabaseAcceptanceTests
{
    [Fact]
    public async Task Real_sessions_MFA_and_level_permissions_are_isolated()
    {
        if (Environment.GetEnvironmentVariable("RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE") != "1")
            return;
        var connection = Environment.GetEnvironmentVariable("PHASE18_DISPOSABLE_DB_CONNECTION")
            ?? throw new InvalidOperationException("Dedicated disposable TLS database connection missing.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Database != "argin_phase18_acceptance" ||
            parsed.Username != "phase18_acceptance" ||
            parsed.SslMode != SslMode.VerifyFull ||
            parsed.Host is not ("127.0.0.1" or "localhost"))
            throw new InvalidOperationException(
                "Refused: dedicated loopback PostgreSQL database, role and VerifyFull TLS required.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(connection).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        Assert.Empty(pending); // migrations must be completed explicitly outside this test
        Assert.Empty(await db.IdentityUsers.AsNoTracking().Select(u => u.Id).Take(1).ToListAsync());
        var password = "Phase18-Test-Only-Temporary-Password!";
        var ownerId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var users = new[]
        {
            (ownerId, "phase18-owner-" + ownerId.ToString("N") + "@example.invalid", IdentityRoleNames.Level1),
            (partnerId, "phase18-partner-" + partnerId.ToString("N") + "@example.invalid", IdentityRoleNames.Level2),
            (customerId, "phase18-customer-" + customerId.ToString("N") + "@example.invalid", IdentityRoleNames.Customer)
        };
        foreach (var (id, email, role) in users)
        {
            db.IdentityUsers.Add(new IdentityUser {
                Id = id, NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = IdentityPasswordHasher.Hash(password),
                CreatedAtUtc = DateTime.UtcNow, PasswordChangedAtUtc = DateTime.UtcNow,
                IsActive = true, Roles = new List<IdentityUserRole> {
                    new() { UserId = id, Role = role, AssignedAtUtc = DateTime.UtcNow }
                }
            });
        }
        await db.SaveChangesAsync();

        var sessions = new IdentitySessionService(db);
        var authorization = new IdentityAuthorizationService(db, sessions);
        var mfa = new IdentityMfaService(db,
            new IdentityMfaCrypto(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            sessions);
        var owner = users[0];
        var partner = users[1];
        var customer = users[2];
        // Plain password cannot bypass MFA for administrative roles.
        Assert.Null(await sessions.LoginAsync(owner.Item2, password));
        Assert.Null(await sessions.LoginAsync(partner.Item2, password));
        var customerSession = await sessions.LoginAsync(customer.Item2, password);
        Assert.NotNull(customerSession);
        Assert.Equal(IdentityAuthorizationResult.Forbidden,
            await authorization.CheckAsync(customerSession!.AccessToken, IdentityPermissions.ReadAdministration));
        Assert.Equal(IdentityAuthorizationResult.Forbidden,
            await authorization.CheckAsync(customerSession.AccessToken, IdentityPermissions.ManageLevel2));
        Assert.Equal(IdentityAuthorizationResult.Allowed,
            await authorization.CheckAsync(customerSession.AccessToken, IdentityPermissions.ReadCustomerArea));

        foreach (var actor in new[] { owner, partner })
        {
            var enrollment = await mfa.BeginEnrollmentAsync(actor.Item1);
            Assert.NotNull(enrollment);
            var codes = await mfa.ConfirmEnrollmentAsync(actor.Item1,
                IdentityTotp.Generate(enrollment!.Secret, DateTime.UtcNow));
            Assert.NotNull(codes);
            Assert.Null(await sessions.LoginAsync(actor.Item2, password));
            var challenge = await mfa.BeginLoginAsync(actor.Item2, password);
            Assert.NotNull(challenge);
            var session = await mfa.CompleteLoginAsync(challenge!.Challenge, codes!.Codes[0], true);
            Assert.NotNull(session);
            Assert.Equal(IdentityAuthorizationResult.Allowed,
                await authorization.CheckAsync(session!.AccessToken, IdentityPermissions.ReadAdministration));
            Assert.Equal(actor.Item3 == IdentityRoleNames.Level1
                    ? IdentityAuthorizationResult.Allowed : IdentityAuthorizationResult.Forbidden,
                await authorization.CheckAsync(session.AccessToken, IdentityPermissions.ManageLevel2));
            Assert.Equal(IdentityAuthorizationResult.Forbidden,
                await authorization.CheckAsync(session.AccessToken, "roles.assign.level1"));
            Assert.Null(await mfa.CompleteLoginAsync(challenge.Challenge, codes.Codes[0], true));
        }

        Assert.True(await sessions.LogoutAsync(customerSession.AccessToken));
        Assert.Null(await sessions.AuthenticateAsync(customerSession.AccessToken));
        // Database is disposable; operator drops entire dedicated DB after audit.
    }
}
