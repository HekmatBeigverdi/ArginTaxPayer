using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentitySessionTests
{
    [Fact]
    public void SessionSchemaHasUniqueTokenDigestsAndMigration()
    {
        using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);
        var entity = db.Model.FindEntityType(typeof(IdentitySession))!;
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.Properties.Single().Name == nameof(IdentitySession.AccessTokenHash));
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique &&
            i.Properties.Single().Name == nameof(IdentitySession.RefreshTokenHash));
        Assert.Contains("20261012030000_AddIdentitySessions", db.Database.GetMigrations());
    }

    [Fact]
    public async Task InvalidTokensNeverAuthenticateOrRefresh()
    {
        using var db = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);
        var service = new IdentitySessionService(db);
        Assert.Null(await service.AuthenticateAsync("invalid"));
        Assert.Null(await service.RefreshAsync("invalid"));
        Assert.False(await service.LogoutAsync("invalid"));
    }
}

/// <summary>Only runs when the dedicated PostgreSQL integration flag is set.</summary>
public sealed class IdentitySessionPostgresTests
{
    [Fact]
    public async Task SessionRotationReplayRevocationAndLogout()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test connection required.");
        var cs = new NpgsqlConnectionStringBuilder(connection);
        if (cs.Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test database.");
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options);
        Assert.Contains("20261012030000_AddIdentitySessions", await db.Database.GetAppliedMigrationsAsync());
        await using var tx = await db.Database.BeginTransactionAsync();
        var credentialService = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
        var email = $"phase03session-{Guid.NewGuid():N}@example.invalid";
        const string password = "Session-Test-Only-2026-Secret!";
        Assert.Equal(IdentityRegistrationResult.Created, await credentialService.RegisterCustomerAsync(email, password));
        var service = new IdentitySessionService(db);
        Assert.Null(await service.LoginAsync(email, "wrong"));
        var first = await service.LoginAsync(email, password);
        Assert.NotNull(first);
        Assert.NotNull(await service.AuthenticateAsync(first!.AccessToken));
        Assert.Null(await service.AuthenticateAsync(first.RefreshToken));

        var rotated = await service.RefreshAsync(first.RefreshToken);
        Assert.NotNull(rotated);
        Assert.Null(await service.AuthenticateAsync(first.AccessToken));
        Assert.NotNull(await service.AuthenticateAsync(rotated!.AccessToken));
        Assert.Null(await service.RefreshAsync(first.RefreshToken)); // Replay revokes session
        Assert.Null(await service.AuthenticateAsync(rotated.AccessToken));
        Assert.Null(await service.RefreshAsync(rotated.RefreshToken));

        var second = await service.LoginAsync(email, password);
        Assert.NotNull(second);
        Assert.True(await service.LogoutAsync(second!.AccessToken));
        Assert.False(await service.LogoutAsync(second.AccessToken));
        Assert.Null(await service.AuthenticateAsync(second.AccessToken));
        Assert.Null(await service.RefreshAsync(second.RefreshToken));

        var third = await service.LoginAsync(email, password);
        var user = await db.IdentityUsers.SingleAsync(x => x.NormalizedEmail == email.ToUpperInvariant());
        user.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Null(await service.AuthenticateAsync(third!.AccessToken));
        Assert.Null(await service.RefreshAsync(third.RefreshToken));
        // Expiration is enforced on the server, not by trusting the caller.
        user.IsActive = true;
        await db.SaveChangesAsync();
        var expiring = await service.LoginAsync(email, password);
        Assert.NotNull(expiring);
        var accessHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(expiring!.AccessToken))).ToLowerInvariant();
        await db.IdentitySessions.Where(x => x.AccessTokenHash == accessHash)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AccessExpiresAtUtc, DateTime.UtcNow.AddSeconds(-5)));
        Assert.Null(await service.AuthenticateAsync(expiring.AccessToken));
        var restored = await service.RefreshAsync(expiring.RefreshToken);
        Assert.NotNull(restored);
        var newAccessHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(restored!.AccessToken))).ToLowerInvariant();
        await db.IdentitySessions.Where(x => x.AccessTokenHash == newAccessHash)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RefreshExpiresAtUtc, DateTime.UtcNow.AddSeconds(-5)));
        Assert.Null(await service.RefreshAsync(restored.RefreshToken));
        Assert.Null(await service.AuthenticateAsync(restored.AccessToken));
        // Test transaction always rolls back.
    }
}
