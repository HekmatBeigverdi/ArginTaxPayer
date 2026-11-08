using System.Security.Cryptography;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

/// <summary>
/// Real persisted tenant-scoping against the dedicated TLS-only disposable QA
/// database. Uses synthetic GUIDs and never touches customer or tax authority data.
/// </summary>
public sealed class Phase18TenantIsolationDatabaseTests
{
    [Fact]
    public async Task Customer_partner_and_root_taxpayer_scope_changes_with_live_commercial_owner()
    {
        if (Environment.GetEnvironmentVariable("RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE") != "1") return;
        var connection = Environment.GetEnvironmentVariable("PHASE18_DISPOSABLE_DB_CONNECTION")
            ?? throw new InvalidOperationException("Missing Phase18 disposable connection.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host is not ("localhost" or "127.0.0.1") ||
            parsed.Port != 15433 ||
            parsed.Database != "argin_phase18_acceptance" ||
            parsed.Username != "phase18_acceptance" ||
            parsed.SslMode != SslMode.VerifyFull ||
            string.IsNullOrWhiteSpace(parsed.RootCertificate))
            throw new InvalidOperationException("Refusing any database other than dedicated Phase18 VerifyFull QA.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var rootId = Guid.NewGuid();
        var partnerAId = Guid.NewGuid();
        var partnerBId = Guid.NewGuid();
        var customerAId = Guid.NewGuid();
        var customerBId = Guid.NewGuid();
        var taxpayerAId = Guid.NewGuid();
        var taxpayerBId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        const string password = "Phase18-Test-Tenancy-Only-Password!";
        foreach (var (id, role) in new[]
        {
            (rootId, IdentityRoleNames.Level1),
            (partnerAId, IdentityRoleNames.Level2),
            (partnerBId, IdentityRoleNames.Level2),
            (customerAId, IdentityRoleNames.Customer),
            (customerBId, IdentityRoleNames.Customer)
        })
        {
            db.IdentityUsers.Add(new IdentityUser
            {
                Id = id,
                NormalizedEmail = $"phase18-{id:N}@example.invalid".ToUpperInvariant(),
                PasswordHash = IdentityPasswordHasher.Hash(password),
                IsActive = true,
                CreatedAtUtc = now,
                PasswordChangedAtUtc = now,
                Roles = new List<IdentityUserRole>
                {
                    new() { UserId = id, Role = role, AssignedAtUtc = now }
                }
            });
        }
        foreach (var (customer, partner) in new[]
        {
            (customerAId, partnerAId),
            (customerBId, partnerBId)
        })
        {
            db.CustomerCommercialOwners.Add(new CustomerCommercialOwner
            {
                CustomerUserId = customer, RootLevel1UserId = rootId,
                PartnerLevel2UserId = partner,
                AcquisitionChannel = CustomerAcquisitionChannel.Level2Created,
                Revision = 1, CreatedAtUtc = now, UpdatedAtUtc = now
            });
        }
        db.TaxpayerProfiles.AddRange(
            new TaxpayerProfile {
                Id = taxpayerAId, CustomerUserId = customerAId,
                Kind = TaxpayerKind.NaturalPerson,
                NormalizedIdentifier = "18" + RandomNumberGenerator.GetInt32(10000000, 99999999),
                DisplayName = "Phase18 isolated taxpayer A",
                Status = TaxpayerStatus.Active, Revision = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now
            },
            new TaxpayerProfile {
                Id = taxpayerBId, CustomerUserId = customerBId,
                Kind = TaxpayerKind.NaturalPerson,
                NormalizedIdentifier = "19" + RandomNumberGenerator.GetInt32(10000000, 99999999),
                DisplayName = "Phase18 isolated taxpayer B",
                Status = TaxpayerStatus.Active, Revision = 1,
                CreatedAtUtc = now, UpdatedAtUtc = now
            });
        await db.SaveChangesAsync();

        var sessions = new IdentitySessionService(db);
        var authorization = new IdentityAuthorizationService(db, sessions);
        var scope = new TaxpayerScopeService(db, authorization, new ParticipantScopeService(db));
        var mfa = new IdentityMfaService(db,
            new IdentityMfaCrypto(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            sessions);
        var customerAToken = (await sessions.LoginAsync($"phase18-{customerAId:N}@example.invalid", password))!.AccessToken;
        var customerBToken = (await sessions.LoginAsync($"phase18-{customerBId:N}@example.invalid", password))!.AccessToken;

        async Task<string> AdminToken(Guid id)
        {
            var email = $"phase18-{id:N}@example.invalid";
            Assert.Null(await sessions.LoginAsync(email, password));
            var enrollment = await mfa.BeginEnrollmentAsync(id);
            Assert.NotNull(enrollment);
            var confirmed = await mfa.ConfirmEnrollmentAsync(id,
                IdentityTotp.Generate(enrollment!.Secret, DateTime.UtcNow));
            Assert.NotNull(confirmed);
            var challenge = await mfa.BeginLoginAsync(email, password);
            Assert.NotNull(challenge);
            var tokens = await mfa.CompleteLoginAsync(challenge!.Challenge, confirmed!.Codes[0], true);
            Assert.NotNull(tokens);
            return tokens!.AccessToken;
        }
        var rootToken = await AdminToken(rootId);
        var partnerAToken = await AdminToken(partnerAId);
        var partnerBToken = await AdminToken(partnerBId);

        Assert.Equal(TaxpayerScopeDecision.Allowed,
            (await scope.GetAsync(customerAToken, taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.NotFound,
            (await scope.GetAsync(customerAToken, taxpayerBId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.NotFound,
            (await scope.GetAsync(customerBToken, taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.NotFound,
            (await scope.GetAsync(partnerAToken, taxpayerBId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.Allowed,
            (await scope.GetAsync(partnerAToken, taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.Allowed,
            (await scope.GetAsync(partnerBToken, taxpayerBId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.NotFound,
            (await scope.GetAsync(partnerBToken, taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.Allowed,
            (await scope.GetAsync(rootToken, taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.Allowed,
            (await scope.GetAsync(rootToken, taxpayerBId)).Decision);

        var aList = await scope.ListAsync(customerAToken);
        var partnerList = await scope.ListAsync(partnerAToken);
        Assert.Equal(TaxpayerScopeDecision.Allowed, aList.Decision);
        Assert.Equal(TaxpayerScopeDecision.Allowed, partnerList.Decision);
        Assert.Contains(aList.Profiles!, p => p.Id == taxpayerAId);
        Assert.DoesNotContain(aList.Profiles!, p => p.Id == taxpayerBId);
        Assert.Contains(partnerList.Profiles!, p => p.Id == taxpayerAId);
        Assert.DoesNotContain(partnerList.Profiles!, p => p.Id == taxpayerBId);

        // Live ownership changes must take effect without renewing the partner token.
        var ownership = new CustomerCommercialOwnershipService(
            db, authorization, new ConfigurationBuilder().Build());
        var transfer = await ownership.TransferAsync(rootToken, customerAId,
            new TransferCustomerOwnerRequest(partnerBId, 1,
                CustomerOwnershipChangeReason.ManualCorrection,
                "Phase18 synthetic owner transfer acceptance"));
        Assert.Equal(CustomerOwnershipDecision.Success, transfer.Decision);
        Assert.Equal(TaxpayerScopeDecision.NotFound,
            (await scope.GetAsync(partnerAToken, taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.Allowed,
            (await scope.GetAsync(partnerBToken, taxpayerAId)).Decision);

        Assert.Equal(TaxpayerScopeDecision.Unauthenticated,
            (await scope.GetAsync("invalid", taxpayerAId)).Decision);
        Assert.Equal(TaxpayerScopeDecision.NotFound,
            (await scope.GetAsync(customerAToken, Guid.NewGuid())).Decision);

        // Fixture remains exclusively in disposable DB for explicit teardown.
        Assert.NotNull(await sessions.AuthenticateAsync(customerBToken));
    }
}
