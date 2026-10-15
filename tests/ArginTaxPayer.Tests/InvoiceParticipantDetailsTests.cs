using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class InvoiceParticipantDetailsTests
{
    [Fact]
    public void Buyers_and_sellers_are_separately_scoped_and_revision_guarded()
    {
        using var db = new ArginTaxPayerDbContext(
            new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql("Host=localhost;Database=not_connected;Username=never;Password=never").Options);
        var type = db.Model.FindEntityType(typeof(InvoiceParticipantDetails))!;
        Assert.Equal("invoice_participant_details", type.GetTableName());
        Assert.True(type.FindProperty(nameof(InvoiceParticipantDetails.Revision))!.IsConcurrencyToken);
        Assert.Equal(3, type.GetForeignKeys().Count());
        Assert.All(type.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.Contains(type.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "party_id IS NOT NULL");
        Assert.Contains(type.GetIndexes(), i => i.IsUnique &&
            i.GetFilter() == "taxpayer_id IS NOT NULL");
    }

    [Fact]
    public async Task Buyer_details_save_works_with_the_configured_retry_strategy()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test database connection required.");
        if (!string.Equals(new NpgsqlConnectionStringBuilder(cs).Database,
            "argintaxpayer_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Must use argintaxpayer_test.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, provider => provider.EnableRetryOnFailure(maxRetryCount: 3)).Options;
        var customerId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var email = "invoice-details-retry-" + Guid.NewGuid().ToString("N") + "@example.invalid";
        const string password = "Invoice-Details-Retry-Password-2026!";

        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var now = DateTime.UtcNow;
            db.IdentityUsers.Add(new IdentityUser
            {
                Id = customerId,
                NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = IdentityPasswordHasher.Hash(password),
                IsActive = true,
                CreatedAtUtc = now,
                PasswordChangedAtUtc = now,
                Roles = new List<IdentityUserRole>
                {
                    new() { UserId = customerId, Role = IdentityRoleNames.Customer, AssignedAtUtc = now }
                }
            });
            db.SoftwareUserProfiles.Add(new SoftwareUserProfile { UserId = customerId, FirstName = "Test", LastName = "Customer", Address = "Tehran", CompletedAtUtc = now, UpdatedAtUtc = now });
            db.Parties.Add(new Party
            {
                Id = partyId, Kind = PartyKind.NaturalPerson,
                NormalizedIdentifier = "0012345678", DisplayName = "Retry Buyer",
                Status = PartyStatus.Active, Revision = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now
            });
            db.CustomerPartyLinks.Add(new CustomerPartyLink
            {
                Id = Guid.NewGuid(), CustomerUserId = customerId, PartyId = partyId,
                LinkedAtUtc = now
            });
            await db.SaveChangesAsync();

            var sessions = new IdentitySessionService(db);
            var tokens = await sessions.LoginAsync(email, password);
            Assert.NotNull(tokens);
            var service = new InvoiceParticipantDetailsService(
                db, new IdentityAuthorizationService(db, sessions), new ParticipantScopeService(db));

            var result = await service.SaveAsync(tokens!.AccessToken, customerId, partyId, false,
                new InvoiceParticipantWrite(null, null, null, "تهران", null, null, null,
                    null, null, null, 0));

            Assert.Equal(InvoiceParticipantOutcome.Success, result.Outcome);
            Assert.Equal("تهران", result.Details!.Province);
        }
        finally
        {
            await using var cleanup = new ArginTaxPayerDbContext(options);
            await cleanup.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await cleanup.Database.BeginTransactionAsync();
                await cleanup.InvoiceParticipantDetails.Where(x => x.CustomerUserId == customerId).ExecuteDeleteAsync();
                await cleanup.CustomerPartyLinks.Where(x => x.CustomerUserId == customerId).ExecuteDeleteAsync();
                await cleanup.Parties.Where(x => x.Id == partyId).ExecuteDeleteAsync();
                await cleanup.SoftwareUserProfiles.Where(x => x.UserId == customerId).ExecuteDeleteAsync();
                await cleanup.IdentitySessions.Where(x => x.UserId == customerId).ExecuteDeleteAsync();
                await cleanup.IdentityUsers.Where(x => x.Id == customerId).ExecuteDeleteAsync();
                await tx.CommitAsync();
            });
        }
    }
}
