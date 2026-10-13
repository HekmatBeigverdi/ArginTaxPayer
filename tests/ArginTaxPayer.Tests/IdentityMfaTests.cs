using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityMfaUnitTests
{
    [Fact]
    public void Rfc6238TOTPAndReplayProtection()
    {
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        var time = DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime;
        Assert.Equal("287082", IdentityTotp.Generate(secret, time));
        Assert.Equal(1L, IdentityTotp.MatchStep(secret, "287082", time, -1));
        Assert.Null(IdentityTotp.MatchStep(secret, "287082", time, 1));
        Assert.Null(IdentityTotp.MatchStep(secret, "invalid", time, -1));
    }

    [Fact]
    public void EncryptedSecretIsAuthenticatedAndNotStoredInPlaintext()
    {
        var crypto = new IdentityMfaCrypto(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var secret = IdentityTotp.NewSecret();
        var ciphertext = crypto.Encrypt(secret);
        Assert.DoesNotContain(secret, ciphertext);
        Assert.Equal(secret, crypto.Decrypt(ciphertext));
        Assert.Throws<InvalidOperationException>(() => new IdentityMfaCrypto("invalid-key"));
        var tampered = Convert.FromBase64String(ciphertext);
        tampered[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => crypto.Decrypt(Convert.ToBase64String(tampered)));
    }

    [Fact]
    public void MfaEntityMappingsAndMigrationAreRegistered()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);
        Assert.Equal("identity_mfa_states", db.Model.FindEntityType(typeof(IdentityMfaState))!.GetTableName());
        Assert.Equal("identity_recovery_codes", db.Model.FindEntityType(typeof(IdentityRecoveryCode))!.GetTableName());
        Assert.Equal("identity_mfa_challenges", db.Model.FindEntityType(typeof(IdentityMfaChallenge))!.GetTableName());
        Assert.Contains("20261012040000_AddIdentityMfa", db.Database.GetMigrations());
    }
}

public sealed class IdentityMfaPostgresTests
{
    [Fact]
    public async Task EnrollmentChallengeTotpRecoveryAndReplay()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit PostgreSQL test connection required");
        if (!string.Equals(new NpgsqlConnectionStringBuilder(cs).Database,
            "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("MFA test cannot target a non-test database");

        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        Assert.Contains("20261012040000_AddIdentityMfa", await db.Database.GetAppliedMigrationsAsync());
        await using var tx = await db.Database.BeginTransactionAsync();
        var credential = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
        const string password = "Test-Mfa-Only-2026-Password!";
        var email = "mfa-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        Assert.Equal(IdentityRegistrationResult.Created, await credential.RegisterCustomerAsync(email, password));
        var user = await db.IdentityUsers.SingleAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
        var sessions = new IdentitySessionService(db);
        var initial = await sessions.LoginAsync(email, password);
        Assert.NotNull(initial);

        var crypto = new IdentityMfaCrypto(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var mfa = new IdentityMfaService(db, crypto, sessions);
        var pending = await mfa.BeginEnrollmentAsync(user.Id);
        Assert.NotNull(pending);
        Assert.Contains("otpauth://totp/", pending!.ProvisioningUri);
        Assert.Null(await mfa.BeginLoginAsync(email, password)); // unconfirmed MFA not sufficient
        var enrollmentCode = IdentityTotp.Generate(pending.Secret, DateTime.UtcNow);
        var codes = await mfa.ConfirmEnrollmentAsync(user.Id, enrollmentCode);
        Assert.NotNull(codes);
        Assert.Equal(10, codes!.Codes.Length);
        Assert.Null(await sessions.AuthenticateAsync(initial!.AccessToken)); // prior session revoked
        Assert.Null(await sessions.LoginAsync(email, password)); // MFA now mandatory
        Assert.Null(await mfa.BeginEnrollmentAsync(user.Id)); // cannot replace confirmed MFA without reauth
        var challenge = await mfa.BeginLoginAsync(email, password);
        Assert.NotNull(challenge);
        Assert.Null(await mfa.CompleteLoginAsync(challenge!.Challenge, "000000", false));

        // The enrollment time-step is already used: a second-factor code from that
        // same 30-second period must be rejected as replay.
        Assert.Null(await mfa.CompleteLoginAsync(challenge.Challenge, enrollmentCode, false));

        var tokens = await mfa.CompleteLoginAsync(challenge.Challenge, codes.Codes[0], true);
        Assert.NotNull(tokens);
        Assert.NotNull(await sessions.AuthenticateAsync(tokens!.AccessToken));
        Assert.Null(await mfa.CompleteLoginAsync(challenge.Challenge, codes.Codes[1], true));
        var second = await mfa.BeginLoginAsync(email, password);
        Assert.NotNull(second);
        Assert.Null(await mfa.CompleteLoginAsync(second!.Challenge, codes.Codes[0], true));
        var third = await mfa.BeginLoginAsync(email, password);
        Assert.NotNull(third);
        Assert.NotNull(await mfa.CompleteLoginAsync(third!.Challenge, codes.Codes[1], true));
        Assert.Null(await mfa.CompleteLoginAsync(third.Challenge, codes.Codes[1], true));
        // Administrative roles are denied password-only login even when MFA is configured.
        db.IdentityUserRoles.Add(new IdentityUserRole {
            UserId = user.Id, Role = IdentityRoleNames.Level2, AssignedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        Assert.Null(await sessions.LoginAsync(email, password));
        var adminChallenge = await mfa.BeginLoginAsync(email, password);
        Assert.NotNull(adminChallenge);
        var adminTokens = await mfa.CompleteLoginAsync(adminChallenge!.Challenge, codes.Codes[2], true);
        Assert.NotNull(adminTokens);
        Assert.NotNull(await sessions.AuthenticateAsync(adminTokens!.AccessToken));

        // A challenge expires and cannot be exchanged for a valid factor.
        var expired = await mfa.BeginLoginAsync(email, password);
        Assert.NotNull(expired);
        var expiredHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(expired!.Challenge))).ToLowerInvariant();
        await db.IdentityMfaChallenges.Where(x => x.ChallengeHash == expiredHash)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        Assert.Null(await mfa.CompleteLoginAsync(expired.Challenge, codes.Codes[3], true));

        var limited = await mfa.BeginLoginAsync(email, password);
        Assert.NotNull(limited);
        for (var i = 0; i < 5; i++)
            Assert.Null(await mfa.CompleteLoginAsync(limited!.Challenge, "000000", false));
        Assert.Null(await mfa.CompleteLoginAsync(limited!.Challenge, codes.Codes[3], true));
        // Disabling the account immediately invalidates the administrative session.
        user.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await sessions.AuthenticateAsync(adminTokens.AccessToken));
        Assert.Null(await sessions.RefreshAsync(adminTokens.RefreshToken));
        // outer transaction rolled back, including recovery code use
    }
}
