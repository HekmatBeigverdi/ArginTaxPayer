using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ParticipantAdministratorScopeTests
{
    [Fact]
    public async Task Level1_can_manage_unassigned_customer_without_profile_but_other_roles_cannot()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(cs).Database);
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        // All fixture changes roll back, including when an assertion fails.
        await using var tx = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        IdentityUser User(string role)
        {
            var id = Guid.NewGuid();
            return new IdentityUser
            {
                Id = id, IsActive = true, CreatedAtUtc = now,
                NormalizedEmail = id.ToString("N") + "@EXAMPLE.INVALID",
                Roles = new List<IdentityUserRole>
                {
                    new() { UserId = id, Role = role, AssignedAtUtc = now }
                }
            };
        }
        var root = User(IdentityRoleNames.Level1);
        var partner = User(IdentityRoleNames.Level2);
        var customer = User(IdentityRoleNames.Customer);
        var other = User(IdentityRoleNames.Customer);
        db.IdentityUsers.AddRange(root, partner, customer, other);
        db.IdentityMfaStates.AddRange(
            new IdentityMfaState { UserId = root.Id, EncryptedSecret = "test-only", EnabledAtUtc = now },
            new IdentityMfaState { UserId = partner.Id, EncryptedSecret = "test-only", EnabledAtUtc = now });
        await db.SaveChangesAsync();
        var scope = new ParticipantScopeService(db);
        Assert.True(await scope.CanManageAsync(root.Id, customer.Id));
        Assert.False(await scope.CanManageAsync(partner.Id, customer.Id));
        Assert.False(await scope.CanManageAsync(other.Id, customer.Id));
        Assert.False(await scope.CanManageAsync(root.Id, Guid.NewGuid()));
        root.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(await scope.CanManageAsync(root.Id, customer.Id));
        root.IsActive = true;
        await db.IdentityMfaStates.Where(m => m.UserId == root.Id).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        Assert.False(await scope.CanManageAsync(root.Id, customer.Id));
    }
}
