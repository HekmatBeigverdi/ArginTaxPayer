using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityResourceScopeUnitTests
{
    [Fact]
    public async Task UnknownOrUnauthenticatedResourcesFailClosedWithoutDatabaseConnection()
    {
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=argintaxpayer_test;Username=x;Password=x").Options);
        var resolver = new IdentityResourceOwnershipResolver(db);
        Assert.False(await resolver.ExistsAsync((IdentityResourceKind)999, Guid.NewGuid()));
        Assert.False(await resolver.IsOwnedByAsync((IdentityResourceKind)999,
            Guid.NewGuid(), Guid.NewGuid()));

        var sessions = new IdentitySessionService(db);
        var permissions = new IdentityAuthorizationService(db, sessions);
        var scopes = new IdentityResourceAuthorizationService(db, permissions, resolver);
        Assert.Equal(ResourceAccessDecision.Unauthenticated,
            await scopes.CheckAsync("invalid", IdentityPermissions.ReadOwnIdentity,
                IdentityResourceKind.IdentityUser, Guid.NewGuid()));
        Assert.Null(await scopes.ListOwnSessionIdsAsync("invalid"));
        Assert.Equal(ResourceAccessDecision.Unauthenticated,
            await scopes.RevokeOwnSessionAsync("invalid", Guid.NewGuid()));
    }
}

public sealed class IdentityResourceScopePostgresTests
{
    [Fact]
    public async Task OwnResourcesAllowedForeignResourcesHiddenAndRevocationIsScoped()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test database connection required.");
        if (!string.Equals(new NpgsqlConnectionStringBuilder(cs).Database,
            "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing non-test database.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, o => o.EnableRetryOnFailure(maxRetryCount: 3)).Options;
        var ids = new List<Guid>();
        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var credentials = new IdentityCredentialService(db);
            var sessions = new IdentitySessionService(db);
            var permissions = new IdentityAuthorizationService(db, sessions);
            var resolver = new IdentityResourceOwnershipResolver(db);
            var scopes = new IdentityResourceAuthorizationService(db, permissions, resolver);
            var a = "scope-a-" + Guid.NewGuid().ToString("N") + "@example.invalid";
            var b = "scope-b-" + Guid.NewGuid().ToString("N") + "@example.invalid";
            const string password = "Scope-Test-Password-2026";
            Assert.Equal(IdentityRegistrationResult.Created, await credentials.RegisterCustomerAsync(a, password));
            Assert.Equal(IdentityRegistrationResult.Created, await credentials.RegisterCustomerAsync(b, password));
            var aId = await db.IdentityUsers.Where(x => x.NormalizedEmail == a.ToUpperInvariant())
                .Select(x => x.Id).SingleAsync();
            var bId = await db.IdentityUsers.Where(x => x.NormalizedEmail == b.ToUpperInvariant())
                .Select(x => x.Id).SingleAsync();
            ids.AddRange([aId, bId]);
            var aToken = await sessions.LoginAsync(a, password);
            var bToken = await sessions.LoginAsync(b, password);
            Assert.NotNull(aToken);
            Assert.NotNull(bToken);
            var aBearer = aToken!.AccessToken;
            var bBearer = bToken!.AccessToken;

            var aSession = await db.IdentitySessions
                .Where(x => x.UserId == aId && x.RevokedAtUtc == null)
                .Select(x => x.Id).SingleAsync();
            var bSession = await db.IdentitySessions
                .Where(x => x.UserId == bId && x.RevokedAtUtc == null)
                .Select(x => x.Id).SingleAsync();

            Assert.Equal(ResourceAccessDecision.Allowed,
                await scopes.CheckAsync(aBearer, IdentityPermissions.ReadOwnIdentity,
                    IdentityResourceKind.IdentityUser, aId));
            Assert.Equal(ResourceAccessDecision.NotFound,
                await scopes.CheckAsync(aBearer, IdentityPermissions.ReadOwnIdentity,
                    IdentityResourceKind.IdentityUser, bId));
            Assert.Equal(ResourceAccessDecision.NotFound,
                await scopes.CheckAsync(aBearer, IdentityPermissions.ReadOwnIdentity,
                    IdentityResourceKind.IdentitySession, bSession));
            Assert.Equal(ResourceAccessDecision.NotFound,
                await scopes.CheckAsync(aBearer, IdentityPermissions.ReadOwnIdentity,
                    IdentityResourceKind.IdentityUser, Guid.NewGuid()));
            Assert.Equal(ResourceAccessDecision.Forbidden,
                await scopes.CheckAsync(aBearer, IdentityPermissions.ReadAdministration,
                    IdentityResourceKind.IdentityUser, aId));
            Assert.Equal(ResourceAccessDecision.Allowed,
                await scopes.CheckAsync(aBearer, IdentityPermissions.ReadOwnIdentity,
                    IdentityResourceKind.IdentitySession, aSession));

            var ownIds = await scopes.ListOwnSessionIdsAsync(aBearer);
            Assert.NotNull(ownIds);
            Assert.Contains(aSession, ownIds!);
            Assert.DoesNotContain(bSession, ownIds);
            Assert.Equal(ResourceAccessDecision.NotFound,
                await scopes.RevokeOwnSessionAsync(aBearer, bSession));
            Assert.NotNull(await sessions.AuthenticateAsync(bBearer));
            Assert.Equal(ResourceAccessDecision.Allowed,
                await scopes.RevokeOwnSessionAsync(aBearer, aSession));
            Assert.Null(await sessions.AuthenticateAsync(aBearer));
            Assert.Null(await sessions.RefreshAsync(aToken.RefreshToken));
            Assert.Equal(ResourceAccessDecision.Unauthenticated,
                await scopes.RevokeOwnSessionAsync(aBearer, aSession));
            Assert.NotNull(await sessions.AuthenticateAsync(bBearer));
        }
        finally
        {
            if (ids.Count != 0)
            {
                await using var cleanup = new ArginTaxPayerDbContext(options);
                await cleanup.IdentityUsers.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            }
        }
    }
}
