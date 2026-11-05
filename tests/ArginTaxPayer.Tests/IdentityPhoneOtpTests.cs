using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityPhoneOtpTests
{
    [Fact]
    public void CodesArePurposeAndChallengeBound()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var id = Guid.NewGuid();
        var digest = IdentityPhoneService.CodeDigest(key, id, "+989123456789", "login", "123456");
        Assert.Equal(64, digest.Length);
        Assert.NotEqual(digest, IdentityPhoneService.CodeDigest(key, id, "+989123456789", "reset", "123456"));
        Assert.NotEqual(digest, IdentityPhoneService.CodeDigest(key, Guid.NewGuid(), "+989123456789", "login", "123456"));
        Assert.NotEqual(digest, IdentityPhoneService.CodeDigest(key, id, "+989123456780", "login", "123456"));
        Assert.DoesNotContain("123456", digest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistrationPasswordlessLoginReplayAndResetUseOneTimeOtp(bool retryAfterInsert)
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test DB required.");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test database.");

        // Isolate committed, append-only credit evidence from shared test data.
        var schema = "phone_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(cs);
        await admin.OpenAsync();
        await using (var create = admin.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA {schema}";
            await create.ExecuteNonQueryAsync();
            foreach (var table in new[] { "identity_users", "identity_user_roles", "identity_sessions",
                "identity_mfa_states", "identity_phone_challenges", "customer_commercial_owners",
                "sms_credit_accounts", "sms_credit_events" })
            {
                create.CommandText = $"CREATE TABLE {schema}.{table} (LIKE public.{table} INCLUDING ALL)";
                await create.ExecuteNonQueryAsync();
            }
        }
        var isolated = new NpgsqlConnectionStringBuilder(cs) { SearchPath = schema }.ConnectionString;
        var retry = new FailFirstAccountInsert(retryAfterInsert);
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(isolated, n => n.EnableRetryOnFailure()).AddInterceptors(retry).Options;
        var phone = "+989" + RandomNumberGenerator.GetInt32(100000000, 999999999);
        var rootId = Guid.NewGuid();
        var fake = new FakeOtpSender();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["Identity:OtpPepper"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["Ownership:DefaultLevel1UserId"] = rootId.ToString(),
                ["Ghasedak:EnableLiveSending"] = "true"
            }).Build();
        Guid userId = Guid.Empty;

        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var now = DateTime.UtcNow;
            db.IdentityUsers.Add(new IdentityUser {
                Id = rootId, IsActive = true, CreatedAtUtc = now,
                NormalizedEmail = "PHONE-TEST-ROOT@EXAMPLE.INVALID",
                Roles = new List<IdentityUserRole> { new() {
                    UserId = rootId, Role = IdentityRoleNames.Level1, AssignedAtUtc = now
                } }
            });
            await db.SaveChangesAsync();
            var sessions = new IdentitySessionService(db);
            var credit = new SmsCreditService(db, new IdentityAuthorizationService(db, sessions));
            var phones = new IdentityPhoneService(db, fake, sessions, settings,
                NullLogger<IdentityPhoneService>.Instance,
                IdentityTestServices.CreateOwnership(db, sessions, settings), credit);
            Assert.True(await phones.RequestAsync(phone, "register"));
            var firstCooldown = await phones.RequestDetailedAsync(phone, "register");
            Assert.Equal(PhoneOtpRequestStatus.RateLimited, firstCooldown.Status);
            Assert.InRange(firstCooldown.RetryAfterSeconds, 175, 180);
            await db.IdentityPhoneChallenges.Where(x => x.Mobile == phone)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc,
                    DateTime.UtcNow.AddSeconds(-30)));
            var remainingCooldown = await phones.RequestDetailedAsync(phone, "register");
            Assert.Equal(PhoneOtpRequestStatus.RateLimited, remainingCooldown.Status);
            Assert.InRange(remainingCooldown.RetryAfterSeconds, 145, 151);
            Assert.NotNull(fake.LastCode);
            Assert.NotEqual(fake.LastCode, (await db.IdentityPhoneChallenges.SingleAsync(x => x.Mobile == phone)).CodeDigest);
            Assert.False(await phones.ConfirmRegistrationAsync(phone,
                fake.LastCode == "000000" ? "111111" : "000000", null));
            Assert.True(await phones.ConfirmRegistrationAsync(phone, fake.LastCode, null));
            Assert.Equal(retryAfterInsert, retry.Injected);
            Assert.False(await phones.ConfirmRegistrationAsync(phone, fake.LastCode, null));
            var alreadyRegistered = await phones.RequestDetailedAsync(phone, "register");
            Assert.Equal(PhoneOtpRequestStatus.AlreadyRegistered, alreadyRegistered.Status);
            userId = await db.IdentityUsers.Where(x => x.NormalizedMobile == phone)
                .Select(x => x.Id).SingleAsync();
            Assert.True(await db.CustomerCommercialOwners.AnyAsync(x => x.CustomerUserId == userId));
            var balance = await credit.ReadCustomerBalanceAsync(userId);
            Assert.Equal(2_000_000L, balance.GrantedIrr);
            Assert.Equal(0L, balance.SpentIrr);
            Assert.Null(await sessions.LoginAsync(phone, "any password"));
            await db.IdentityPhoneChallenges.Where(x => x.Mobile == phone)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-4)));
            Assert.True(await phones.RequestAsync(phone, "login"));
            var token = await phones.CompleteLoginAsync(phone, fake.LastCode);
            Assert.NotNull(token);
            Assert.NotNull(await sessions.AuthenticateAsync(token!.AccessToken));
            Assert.Null(await phones.CompleteLoginAsync(phone, fake.LastCode));
            await db.IdentityPhoneChallenges.Where(x => x.Mobile == phone)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-4)));
            Assert.True(await phones.RequestAsync(phone, "reset"));
            Assert.True(await phones.ResetPasswordAsync(phone, fake.LastCode, "Mobile-Reset-Password-2026"));
            Assert.False(await phones.ResetPasswordAsync(phone, fake.LastCode, "Mobile-Reset-Password-2026"));
            Assert.Null(await sessions.AuthenticateAsync(token.AccessToken));
            Assert.NotNull(await sessions.LoginAsync(phone, "Mobile-Reset-Password-2026"));
            // A verified Customer who now has a password may still choose SMS OTP.
            await db.IdentityPhoneChallenges.Where(x => x.Mobile == phone)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-4)));
            Assert.True(await phones.RequestAsync(phone, "login"));
            var passwordAccountOtp = fake.LastCode;
            var passwordAccountSession = await phones.CompleteLoginAsync(phone, passwordAccountOtp);
            Assert.NotNull(passwordAccountSession);
            Assert.NotNull(await sessions.AuthenticateAsync(passwordAccountSession!.AccessToken));
            Assert.Null(await phones.CompleteLoginAsync(phone, passwordAccountOtp));
            Assert.NotNull(await sessions.LoginAsync(phone, "Mobile-Reset-Password-2026"));
            // Enabled MFA must still prevent a direct OTP-only session.
            var mfaState = new IdentityMfaState {
                UserId = userId, EncryptedSecret = "test-only-unusable",
                EnabledAtUtc = DateTime.UtcNow
            };
            db.IdentityMfaStates.Add(mfaState);
            await db.SaveChangesAsync();
            await db.IdentityPhoneChallenges.Where(x => x.Mobile == phone)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-4)));
            Assert.False(await phones.RequestAsync(phone, "login"));
            Assert.Null(await sessions.CreatePhoneSessionAsync(userId));
        }
        finally
        {
            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP SCHEMA {schema} CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    // Fail after the real INSERT succeeds, so retry must restore the entire
    // rolled-back account transaction without re-consuming the one-time proof.
    private sealed class FailFirstAccountInsert(bool enabled) : SaveChangesInterceptor
    {
        public bool Injected { get; private set; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (enabled && !Injected && eventData.Context!.ChangeTracker.Entries<IdentityUser>()
                .Any(x => x.Entity.NormalizedMobile is not null))
            {
                Injected = true;
                throw new NpgsqlException("Simulated transient failure after account insert.",
                    new TimeoutException());
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FakeOtpSender : IIdentityOtpSender
    {
        public string? LastCode { get; private set; }
        public Task<bool> SendAsync(string mobile, string code, string purpose, Guid deliveryId,
            CancellationToken cancellationToken = default)
        {
            LastCode = code;
            return Task.FromResult(true);
        }
    }
}
