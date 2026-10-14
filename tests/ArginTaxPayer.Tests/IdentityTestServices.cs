using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Tests;

/// <summary>Test-only root selection and FK-aware fixture disposal. Production remains fail-closed.</summary>
internal static class IdentityTestServices
{
    public static CustomerCommercialOwnershipService CreateOwnership(
        ArginTaxPayerDbContext db,
        IdentitySessionService? sessions = null,
        IConfiguration? configuration = null)
    {
        var sessionService = sessions ?? new IdentitySessionService(db);
        // Integration fixtures may coexist with multiple privileged Level1 identities.
        // Explicitly choose a current Level1 in the test database; never weaken
        // the production ResolveRootAsync ambiguity guard.
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
        var isRealIntegration = Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") == "1" &&
            connection.Database == "argintaxpayer_test" &&
            connection.Username is not ("unused" or "x");
        if (isRealIntegration && string.IsNullOrWhiteSpace(configuration?["Ownership:DefaultLevel1UserId"]))
        {
            var root = db.IdentityUsers.AsNoTracking()
                .Where(u => u.IsActive &&
                    u.Roles.Any(r => r.Role == IdentityRoleNames.Level1))
                .OrderBy(u => u.Id).Select(u => (Guid?)u.Id).FirstOrDefault();
            if (root is null)
                throw new InvalidOperationException(
                    "Phase04 PostgreSQL identity integration fixtures require an existing active Level1 root.");
            var builder = new ConfigurationBuilder();
            if (configuration is not null) builder.AddConfiguration(configuration);
            configuration = builder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Ownership:DefaultLevel1UserId"] = root.Value.ToString()
                }).Build();
        }
        return new CustomerCommercialOwnershipService(
            db, new IdentityAuthorizationService(db, sessionService), configuration);
    }

    public static async Task CleanupCustomerFixturesAsync(
        ArginTaxPayerDbContext db, IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0) return;
        // Delete only rows owned by these disposable test user IDs.
        // Real customer/partner data and other test suites remain untouched.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.CustomerCommercialOwnerChanges
                .Where(x => userIds.Contains(x.CustomerUserId))
                .ExecuteDeleteAsync(ct);
            await db.CustomerCommercialOwners
                .Where(x => userIds.Contains(x.CustomerUserId))
                .ExecuteDeleteAsync(ct);
            await db.IdentityUsers.Where(x => userIds.Contains(x.Id))
                .ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
