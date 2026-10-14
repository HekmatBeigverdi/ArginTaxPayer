using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Regression for EF Core production-configured retry strategy:
/// MFA invalid codes must return failure, not throw transaction strategy errors.
/// </summary>
public sealed class IdentityMfaRetryStrategyPostgresTests
{
    [Fact]
    public async Task InvalidEnrollmentAndChallengeAreRejectedWithRetryEnabled()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test database connection required.");
        if (!string.Equals(new NpgsqlConnectionStringBuilder(cs).Database,
            "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Must use argintaxpayer_test.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, provider => provider.EnableRetryOnFailure(maxRetryCount: 3)).Options;

        var email = "retry-mfa-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Guid userId = default;

        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var credentials = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
            const string password = "Disposable-MFA-Retry-Password-2026!";
            Assert.Equal(IdentityRegistrationResult.Created,
                await credentials.RegisterCustomerAsync(email, password));
            var user = await db.IdentityUsers.SingleAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
            userId = user.Id;

            var sessions = new IdentitySessionService(db);
            var mfa = new IdentityMfaService(db, new IdentityMfaCrypto(key), sessions);
            var enrollment = await mfa.BeginEnrollmentAsync(userId);
            Assert.NotNull(enrollment);
            // This previously threw InvalidOperationException with EnableRetryOnFailure.
            Assert.Null(await mfa.ConfirmEnrollmentAsync(userId, "invalid"));
            var recovery = await mfa.ConfirmEnrollmentAsync(
                userId, IdentityTotp.Generate(enrollment!.Secret, DateTime.UtcNow));
            Assert.NotNull(recovery);

            var challenge = await mfa.BeginLoginAsync(email, password);
            Assert.NotNull(challenge);
            Assert.Null(await mfa.CompleteLoginAsync(challenge!.Challenge, "000000", false));
            var tokens = await mfa.CompleteLoginAsync(
                challenge.Challenge, recovery!.Codes[0], true);
            Assert.NotNull(tokens);
            Assert.NotNull(await sessions.AuthenticateAsync(tokens!.AccessToken));
            Assert.Null(await mfa.CompleteLoginAsync(challenge.Challenge, recovery.Codes[1], true));
        }
        finally
        {
            if (userId != Guid.Empty)
            {
                await using var cleanup = new ArginTaxPayerDbContext(options);
                await IdentityTestServices.CleanupCustomerFixturesAsync(cleanup, new[] { userId });
            }
        }
    }
}
