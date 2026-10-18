using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class TaxpayerCreationRetryTests
{
    [Fact]
    public async Task Creation_with_retry_enabled_persists_once_and_rejects_duplicate()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(cs).Database);
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, p => p.EnableRetryOnFailure(3)).Options;
        var customer = Guid.NewGuid();
        var email = InitialAdministratorBootstrap.NormalizeEmail(
            customer.ToString("N") + "@example.invalid");
        Assert.Equal(email, InitialAdministratorBootstrap.NormalizeEmail(email));
        const string password = "Disposable-taxpayer-retry-2026!";
        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var now = DateTime.UtcNow;
            db.IdentityUsers.Add(new IdentityUser
            {
                Id = customer, NormalizedEmail = email, IsActive = true, CreatedAtUtc = now,
                PasswordHash = IdentityPasswordHasher.Hash(password), PasswordChangedAtUtc = now,
                Roles = new List<IdentityUserRole>
                {
                    new() { UserId = customer, Role = IdentityRoleNames.Customer, AssignedAtUtc = now }
                }
            });
            db.SoftwareUserProfiles.Add(new SoftwareUserProfile
            {
                UserId = customer, FirstName = "تست", LastName = "ثبت مؤدی", Address = "تهران",
                CompletedAtUtc = now, UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
            var sessions = new IdentitySessionService(db);
            var token = await sessions.LoginAsync(email, password);
            Assert.NotNull(token);
            var service = new TaxpayerCreationService(db,
                new IdentityAuthorizationService(db, sessions), new ParticipantScopeService(db));
            var identifier = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();
            var input = new CreateTaxpayerRequest(TaxpayerKind.LegalEntity, identifier, "Retry test", customer);
            var result = await service.CreateAsync(token!.AccessToken, input);
            Assert.Equal(CreateTaxpayerStatus.Created, result.Status);
            Assert.NotNull(result.Taxpayer);
            Assert.Equal(customer, (await db.TaxpayerProfiles.AsNoTracking()
                .SingleAsync(t => t.Id == result.Taxpayer!.Id)).CustomerUserId);
            Assert.Equal(CreateTaxpayerStatus.Conflict,
                (await service.CreateAsync(token.AccessToken, input)).Status);
            Assert.Equal(1, await db.TaxpayerProfiles.CountAsync(t => t.CustomerUserId == customer));
            Assert.Equal(CreateTaxpayerStatus.Forbidden,
                (await service.CreateAsync(token.AccessToken, input with { CustomerUserId = Guid.NewGuid() })).Status);
        }
        finally
        {
            await using var cleanup = new ArginTaxPayerDbContext(options);
            await cleanup.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await cleanup.Database.BeginTransactionAsync();
                await cleanup.TaxpayerProfiles.Where(t => t.CustomerUserId == customer).ExecuteDeleteAsync();
                await cleanup.SoftwareUserProfiles.Where(p => p.UserId == customer).ExecuteDeleteAsync();
                await cleanup.IdentitySessions.Where(s => s.UserId == customer).ExecuteDeleteAsync();
                await cleanup.IdentityUsers.Where(u => u.Id == customer).ExecuteDeleteAsync();
                await tx.CommitAsync();
            });
        }
    }
}
