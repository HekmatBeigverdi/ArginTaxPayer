using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class ParticipantAdministratorScopeTests
{
    [Fact]
    public async Task Scope_requires_completed_profile_current_assignment_and_active_mfa()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test connection required.");
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(cs).Database);
        await using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options);
        // Intentionally use a single transaction; every fixture mutation is rolled back.
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
        var replacement = User(IdentityRoleNames.Level2);
        var customer = User(IdentityRoleNames.Customer);
        var otherCustomer = User(IdentityRoleNames.Customer);
        db.IdentityUsers.AddRange(root, partner, replacement, customer, otherCustomer);
        db.IdentityMfaStates.AddRange(
            new IdentityMfaState { UserId = root.Id, EncryptedSecret = "test-only", EnabledAtUtc = now },
            new IdentityMfaState { UserId = partner.Id, EncryptedSecret = "test-only", EnabledAtUtc = now },
            new IdentityMfaState { UserId = replacement.Id, EncryptedSecret = "test-only", EnabledAtUtc = now });
        db.SoftwareUserProfiles.Add(new SoftwareUserProfile
        {
            UserId = customer.Id, FirstName = "علی", LastName = "سهرابی",
            Address = "تهران", CompletedAtUtc = now, UpdatedAtUtc = now
        });
        var owner = new CustomerCommercialOwner
        {
            CustomerUserId = customer.Id, RootLevel1UserId = root.Id,
            PartnerLevel2UserId = partner.Id,
            AcquisitionChannel = ArginTaxPayer.Domain.Taxpayers.CustomerAcquisitionChannel.Level2Created,
            Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.CustomerCommercialOwners.Add(owner);
        await db.SaveChangesAsync();
        var scope = new ParticipantScopeService(db);
        Assert.True(await scope.CanManageAsync(root.Id, customer.Id));
        Assert.True(await scope.CanManageAsync(partner.Id, customer.Id));
        Assert.True(await scope.CanManageAsync(customer.Id, customer.Id));
        Assert.False(await scope.CanManageAsync(replacement.Id, customer.Id));
        Assert.False(await scope.CanManageAsync(root.Id, otherCustomer.Id));

        owner.PartnerLevel2UserId = replacement.Id;
        owner.Revision++;
        owner.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.False(await scope.CanManageAsync(partner.Id, customer.Id));
        Assert.True(await scope.CanManageAsync(replacement.Id, customer.Id));
        partner.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(await scope.CanManageAsync(partner.Id, customer.Id));
        // Profile completion and MFA are independent mandatory gates.
        var profile = await db.SoftwareUserProfiles.SingleAsync(p => p.UserId == customer.Id);
        db.SoftwareUserProfiles.Remove(profile);
        await db.SaveChangesAsync();
        Assert.False(await scope.CanManageAsync(root.Id, customer.Id));
        Assert.False(await scope.CanManageAsync(replacement.Id, customer.Id));
        await tx.RollbackAsync();
    }

}
