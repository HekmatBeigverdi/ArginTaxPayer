using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class IdentityMfaOnboardingTests
{
    [Fact]
    public async Task OnlyNominatedUsersCanEnrollAndProofActivatesAdminWithoutPasswordBypass()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(cs).Database);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var ids = new List<Guid>();
        try
        {
            var credentials = new IdentityCredentialService(db, IdentityTestServices.CreateOwnership(db));
            var sessions = new IdentitySessionService(db);
            var mfa = new IdentityMfaService(db, new IdentityMfaCrypto(key), sessions);
            var password = "Mfa-Onboarding-Test-Only-2026!";
            var ownerEmail = $"mfa-owner-{Guid.NewGuid():N}@example.invalid";
            var targetEmail = $"mfa-target-{Guid.NewGuid():N}@example.invalid";
            foreach (var email in new[] { ownerEmail, targetEmail })
            {
                Assert.Equal(IdentityRegistrationResult.Created, await credentials.RegisterCustomerAsync(email, password));
                ids.Add(await db.IdentityUsers.Where(u => u.NormalizedEmail == email.ToUpperInvariant()).Select(u => u.Id).SingleAsync());
            }
            var ownerId = ids[0]; var targetId = ids[1];
            var mobile = "+989" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
            await db.IdentityUsers.Where(u => u.Id == targetId).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.NormalizedMobile, mobile).SetProperty(u => u.MobileVerifiedAtUtc, DateTime.UtcNow));
            var original = (await sessions.LoginAsync(targetEmail, password))!;
            using var factory = new MfaApiFactory(cs, key);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", original.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/dev/identity/mfa/enroll", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dev/identity/mfa/status")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/dev/identity/mfa/confirm", new { code = "123456" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/dev/identity/roles/level2/nominate", new { mobile })).StatusCode);

            // Owner-approved first-admin invitation is a trusted operator action.
            db.IdentityUserRoles.Add(new() { UserId = ownerId, Role = IdentityRoleNames.PendingLevel1, AssignedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var ownerEnrollment = (await mfa.BeginEnrollmentAsync(ownerId))!;
            var ownerCodes = (await mfa.ConfirmEnrollmentAsync(ownerId, IdentityTotp.Generate(ownerEnrollment.Secret, DateTime.UtcNow)))!;
            var ownerChallenge = (await mfa.BeginLoginAsync(ownerEmail, password))!;
            var ownerSession = (await mfa.CompleteLoginAsync(ownerChallenge.Challenge, ownerCodes.Codes[0], true))!;
            Assert.True(await db.IdentityUserRoles.AnyAsync(r => r.UserId == ownerId && r.Role == IdentityRoleNames.Level1));
            client.DefaultRequestHeaders.Authorization = new("Bearer", ownerSession.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/dev/identity/roles/level2/nominate", new { mobile })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/dev/identity/roles/level2/nominate", new { mobile })).StatusCode);
            Assert.Null(await sessions.AuthenticateAsync(original.AccessToken));
            Assert.Null(await sessions.RefreshAsync(original.RefreshToken));
            Assert.False(await db.IdentityUserRoles.AnyAsync(r => r.UserId == targetId && r.Role == IdentityRoleNames.Level2));

            var pendingSession = (await sessions.LoginAsync(targetEmail, password))!;
            Assert.NotNull(pendingSession);
            Assert.Null(await sessions.CreatePhoneSessionAsync(targetId));
            client.DefaultRequestHeaders.Authorization = new("Bearer", pendingSession.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dev/identity/authorization/admin")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/dev/identity/roles/level2/nominate", new { mobile })).StatusCode);
            var status = await (await client.GetAsync("/api/dev/identity/mfa/status")).Content.ReadFromJsonAsync<MfaStatus>();
            Assert.NotNull(status); Assert.False(status.Enabled); Assert.True(status.Available);

            using (var unconfigured = new MfaApiFactory(cs, ""))
            using (var missingKeyClient = unconfigured.CreateClient())
            {
                missingKeyClient.DefaultRequestHeaders.Authorization = new("Bearer", pendingSession.AccessToken);
                Assert.Equal(HttpStatusCode.ServiceUnavailable,
                    (await missingKeyClient.PostAsync("/api/dev/identity/mfa/enroll", null)).StatusCode);
            }
            var response = await client.PostAsync("/api/dev/identity/mfa/enroll", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            var enrollment = (await response.Content.ReadFromJsonAsync<MfaEnrollment>())!;
            Assert.StartsWith("otpauth://totp/ArginTaxPayer:", enrollment.ProvisioningUri);
            Assert.Contains("secret=" + enrollment.Secret, enrollment.ProvisioningUri);
            var repeated = await (await client.PostAsync("/api/dev/identity/mfa/enroll", null)).Content.ReadFromJsonAsync<MfaEnrollment>();
            Assert.Equal(enrollment.Secret, repeated!.Secret);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/dev/identity/mfa/confirm", new { code = "invalid" })).StatusCode);
            Assert.False(await db.IdentityMfaStates.AnyAsync(m => m.UserId == targetId && m.EnabledAtUtc != null));
            var confirmed = await client.PostAsJsonAsync("/api/dev/identity/mfa/confirm",
                new { code = IdentityTotp.Generate(enrollment.Secret, DateTime.UtcNow) });
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            var recovery = (await confirmed.Content.ReadFromJsonAsync<MfaRecoveryCodes>())!;
            Assert.Equal(10, recovery.Codes.Length);
            Assert.True(await db.IdentityUserRoles.AnyAsync(r => r.UserId == targetId && r.Role == IdentityRoleNames.Level2));
            Assert.False(await db.IdentityUserRoles.AnyAsync(r => r.UserId == targetId && r.Role == IdentityRoleNames.PendingLevel2));
            Assert.Null(await sessions.AuthenticateAsync(pendingSession.AccessToken));
            Assert.Null(await sessions.LoginAsync(targetEmail, password));
            var challenge = (await mfa.BeginLoginAsync(targetEmail, password))!;
            var admin = (await mfa.CompleteLoginAsync(challenge.Challenge, recovery.Codes[0], true))!;
            client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dev/identity/authorization/admin")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/dev/identity/roles/level2/nominate", new { mobile })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/dev/identity/mfa/enroll", null)).StatusCode);
        }
        finally
        {
            await using var cleanup = new ArginTaxPayerDbContext(options);
            await IdentityTestServices.CleanupCustomerFixturesAsync(cleanup, ids);
        }
    }

    private sealed record MfaStatus(bool Enabled, bool Available);

    private sealed class MfaApiFactory(string connection, string key) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:ArginTaxPayerDb"] = connection,
                ["Identity:MfaEncryptionKey"] = key,
                ["Ghasedak:EnableLiveSending"] = "false"
            }));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(s => s.AddSingleton<IStartupFilter, LoopbackFilter>());
        }
    }
    private sealed class LoopbackFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
            app.Use(async (context, continuation) => {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                await continuation();
            });
            next(app);
        };
    }
}
