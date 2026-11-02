using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class PartnerSettlementPaginationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Balances_page_in_database_and_keep_totals_within_actor_scope(bool partner)
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Missing explicit test database connection.");
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(connection).Database);
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var actor = Guid.NewGuid();
        var other = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();
        // Every synthetic record is rolled back; temporary financial tables avoid touching
        // immutable real evidence while exercising the production SQL on PostgreSQL.
        db.IdentityUsers.Add(new IdentityUser
        {
            Id = actor, NormalizedEmail = $"settlement-page-{actor:N}@example.invalid",
            IsActive = true, CreatedAtUtc = now,
            Roles = [new IdentityUserRole
            {
                Role = partner ? IdentityRoleNames.Level2 : IdentityRoleNames.Level1,
                AssignedAtUtc = now
            }]
        });
        db.IdentityMfaStates.Add(new IdentityMfaState
        {
            UserId = actor, EncryptedSecret = "test-only", EnabledAtUtc = now
        });
        db.IdentitySessions.Add(new IdentitySession
        {
            Id = Guid.NewGuid(), UserId = actor, AccessTokenHash = digest,
            RefreshTokenHash = Guid.NewGuid().ToString("N"), CreatedAtUtc = now,
            AccessExpiresAtUtc = now.AddMinutes(10), RefreshExpiresAtUtc = now.AddDays(1)
        });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TEMP TABLE commercial_revenue_allocations (
                id uuid PRIMARY KEY, root_user_id uuid, partner_user_id uuid, partner_share_irr bigint);
            CREATE TEMP TABLE partner_settlement_events (allocation_id uuid, kind integer, amount_irr bigint);
            """);
        var root = partner ? other : actor;
        var partnerId = partner ? actor : other;
        for (var i = 1; i <= 23; i++)
        {
            var id = Guid.Parse($"00000000-0000-0000-0000-{i:D12}");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial_revenue_allocations VALUES ({id},{root},{partnerId},1000);
                INSERT INTO partner_settlement_events VALUES ({id},1,300),({id},2,100);
                """);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial_revenue_allocations VALUES ({Guid.NewGuid()},{Guid.NewGuid()},{Guid.NewGuid()},999999);
            """);
        var service = new PartnerSettlementReadService(db,
            new IdentityAuthorizationService(db, new IdentitySessionService(db)));
        var first = await service.GetBalancesAsync(token);
        Assert.Equal(10, first.Items.Length);
        Assert.Equal(23000, first.TotalEarnedIrr);
        Assert.Equal(4600, first.TotalSettledIrr);
        Assert.Equal(18400, first.TotalOutstandingIrr);
        Assert.Equal(23, first.TotalCount);
        Assert.Equal(1, first.Page);
        Assert.Equal(10, first.PageSize);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), first.Items[0].AllocationId);
        var second = await service.GetBalancesAsync(token, page: 2);
        Assert.Equal(10, second.Items.Length);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000011"), second.Items[0].AllocationId);
        Assert.Empty(first.Items.Select(x => x.AllocationId).Intersect(second.Items.Select(x => x.AllocationId)));
        var third = await service.GetBalancesAsync(token, page: 3);
        Assert.Equal(3, third.Items.Length);
        Assert.Equal(23000, third.TotalEarnedIrr);
        Assert.Equal(4600, third.TotalSettledIrr);
        Assert.Equal(18400, third.TotalOutstandingIrr);
        Assert.Equal(23, third.TotalCount);
        Assert.Equal(3, third.Page);
        var beyond = await service.GetBalancesAsync(token, page: 4);
        Assert.Empty(beyond.Items);
        Assert.Equal(23, beyond.TotalCount);
        Assert.Equal(18400, beyond.TotalOutstandingIrr);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetBalancesAsync(token, page: 0));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM commercial_revenue_allocations");
        var empty = await service.GetBalancesAsync(token);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.TotalEarnedIrr);
        Assert.Equal(0, empty.TotalSettledIrr);
        Assert.Equal(0, empty.TotalOutstandingIrr);
        await db.IdentityUsers.Where(u => u.Id == actor).ExecuteUpdateAsync(
            setters => setters.SetProperty(u => u.IsActive, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBalancesAsync(token));
    }
}
