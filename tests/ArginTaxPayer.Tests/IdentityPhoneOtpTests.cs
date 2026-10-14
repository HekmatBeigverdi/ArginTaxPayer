using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task RegistrationPasswordlessLoginReplayAndResetUseOneTimeOtp()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")
            ?? throw new InvalidOperationException("Explicit test DB required.");
        if (new NpgsqlConnectionStringBuilder(cs).Database != "argintaxpayer_test")
            throw new InvalidOperationException("Refusing non-test database.");

        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(cs, n => n.EnableRetryOnFailure()).Options;
        var phone = "+989" + RandomNumberGenerator.GetInt32(100000000, 999999999);
        var fake = new FakeOtpSender();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> {
                ["Identity:OtpPepper"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["Ghasedak:EnableLiveSending"] = "true"
            }).Build();
        Guid userId = Guid.Empty;

        try
        {
            await using var db = new ArginTaxPayerDbContext(options);
            var sessions = new IdentitySessionService(db);
            var phones = new IdentityPhoneService(db, fake, sessions, settings,
                NullLogger<IdentityPhoneService>.Instance,
                IdentityTestServices.CreateOwnership(db, sessions, settings));
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
            Assert.False(await phones.ConfirmRegistrationAsync(phone, fake.LastCode, null));
            var alreadyRegistered = await phones.RequestDetailedAsync(phone, "register");
            Assert.Equal(PhoneOtpRequestStatus.AlreadyRegistered, alreadyRegistered.Status);
            userId = await db.IdentityUsers.Where(x => x.NormalizedMobile == phone)
                .Select(x => x.Id).SingleAsync();
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
            await using var cleanup = new ArginTaxPayerDbContext(options);
            if (userId != Guid.Empty)
                await IdentityTestServices.CleanupCustomerFixturesAsync(cleanup, new[] { userId });
            await cleanup.IdentityPhoneChallenges.Where(x => x.Mobile == phone).ExecuteDeleteAsync();
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
