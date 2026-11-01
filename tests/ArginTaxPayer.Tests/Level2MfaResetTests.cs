using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Level2MfaResetTests
{
    [Fact]
    public async Task Level1CanFindResetAndReenrollLevel2WhileOtherCallersAndTargetsAreDenied()
    {
        if (Environment.GetEnvironmentVariable("RUN_ARGINTAXPAYER_PG_INTEGRATION") != "1") return;
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
        Assert.Equal("argintaxpayer_test", new NpgsqlConnectionStringBuilder(cs).Database);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var options = new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(cs).Options;
        await using var db = new ArginTaxPayerDbContext(options);
        var ids = new List<Guid>();
        const string password = "Reset-Mfa-Fixture-Only-2026!";
        var sessions = new IdentitySessionService(db);
        var mfa = new IdentityMfaService(db, new IdentityMfaCrypto(key), sessions);
        async Task<(Guid Id, string Mobile, SessionTokens Tokens, MfaRecoveryCodes Codes)> Create(string role)
        {
            var id = Guid.NewGuid(); ids.Add(id);
            var mobile = "+989" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
            db.IdentityUsers.Add(new IdentityUser {
                Id = id, NormalizedMobile = mobile, MobileVerifiedAtUtc = DateTime.UtcNow,
                PasswordHash = IdentityPasswordHasher.Hash(password), IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                Roles = [new IdentityUserRole { UserId = id, Role = role, AssignedAtUtc = DateTime.UtcNow }]
            });
            await db.SaveChangesAsync();
            var enrollment = (await mfa.BeginEnrollmentAsync(id))!;
            var codes = (await mfa.ConfirmEnrollmentAsync(id, IdentityTotp.Generate(enrollment.Secret, DateTime.UtcNow)))!;
            var challenge = (await mfa.BeginLoginAsync(mobile, password))!;
            var tokens = (await mfa.CompleteLoginAsync(challenge.Challenge, codes.Codes[0], true))!;
            return (id, mobile, tokens, codes);
        }
        try
        {
            var root = await Create(IdentityRoleNames.Level1);
            var target = await Create(IdentityRoleNames.Level2);
            var customer = await Create(IdentityRoleNames.Customer);
            var originalHash = await db.IdentityUsers.Where(u => u.Id == target.Id).Select(u => u.PasswordHash).SingleAsync();
            db.CustomerCommercialOwners.Add(new CustomerCommercialOwner {
                CustomerUserId = target.Id, RootLevel1UserId = root.Id,
                AcquisitionChannel = CustomerAcquisitionChannel.Level1Created, Revision = 7,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            using var factory = new ResetApiFactory(cs, key);
            using var client = factory.CreateClient();
            const string endpoint = "/api/dev/identity/mfa/level2/reset";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id })).StatusCode);
            foreach (var token in new[] { customer.Tokens.AccessToken, target.Tokens.AccessToken })
            {
                client.DefaultRequestHeaders.Authorization = new("Bearer", token);
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id })).StatusCode);
            }
            client.DefaultRequestHeaders.Authorization = new("Bearer", target.Tokens.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dev/customer-owners/accounts/page?role=level2")).StatusCode);
            var scopedPage = await client.GetFromJsonAsync<JsonElement>("/api/dev/customer-owners/accounts/page?query=" + Uri.EscapeDataString(customer.Mobile));
            Assert.Equal(0, scopedPage.GetProperty("total").GetInt32());
            client.DefaultRequestHeaders.Authorization = new("Bearer", root.Tokens.AccessToken);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dev/customer-owners/accounts/page?role=invalid")).StatusCode);
            foreach (var denied in new[] { root.Id, customer.Id })
                Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(endpoint, new { targetUserId = denied })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(endpoint, new { targetUserId = Guid.NewGuid() })).StatusCode);

            var domestic = "0" + target.Mobile[3..];
            foreach (var query in new[] { domestic, string.Concat(domestic.Select(c => (char)('\u06f0' + c - '0'))), target.Mobile, target.Mobile[5..] })
            {
                var page = await client.GetFromJsonAsync<JsonElement>("/api/dev/customer-owners/accounts/page?role=level2&query=" + Uri.EscapeDataString(query));
                Assert.Equal(1, page.GetProperty("total").GetInt32());
                var row = page.GetProperty("items")[0];
                Assert.Equal(target.Id, row.GetProperty("customerUserId").GetGuid());
                Assert.True(row.GetProperty("mfaEnabled").GetBoolean());
                Assert.Contains("Level2", row.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
            }
            var customerPage = await client.GetFromJsonAsync<JsonElement>("/api/dev/customer-owners/accounts/page?role=customer&query=" + Uri.EscapeDataString(domestic));
            Assert.Equal(0, customerPage.GetProperty("total").GetInt32());
            var staleChallenge = (await mfa.BeginLoginAsync(target.Mobile, password))!;
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id })).StatusCode);
            Assert.Null(await sessions.AuthenticateAsync(target.Tokens.AccessToken));
            Assert.Null(await sessions.RefreshAsync(target.Tokens.RefreshToken));
            Assert.Null(await mfa.CompleteLoginAsync(staleChallenge.Challenge, target.Codes.Codes[1], true));
            Assert.False(await db.IdentityMfaStates.AnyAsync(x => x.UserId == target.Id));
            Assert.Equal(originalHash, await db.IdentityUsers.Where(u => u.Id == target.Id).Select(u => u.PasswordHash).SingleAsync());
            var preservedOwner = await db.CustomerCommercialOwners.AsNoTracking().SingleAsync(o => o.CustomerUserId == target.Id);
            Assert.Equal(root.Id, preservedOwner.RootLevel1UserId);
            Assert.Equal(7, preservedOwner.Revision);
            Assert.Equal(CustomerAcquisitionChannel.Level1Created, preservedOwner.AcquisitionChannel);
            Assert.False(await db.IdentityRecoveryCodes.AnyAsync(x => x.UserId == target.Id));
            Assert.False(await db.IdentityMfaChallenges.AnyAsync(x => x.UserId == target.Id));
            Assert.True(await db.IdentityUserRoles.AnyAsync(x => x.UserId == target.Id && x.Role == IdentityRoleNames.PendingLevel2));
            Assert.False(await db.IdentityUserRoles.AnyAsync(x => x.UserId == target.Id && x.Role == IdentityRoleNames.Level2));
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id })).StatusCode);
            var pendingPage = await client.GetFromJsonAsync<JsonElement>("/api/dev/customer-owners/accounts/page?role=level2&query=" + Uri.EscapeDataString(domestic));
            Assert.Equal(1, pendingPage.GetProperty("total").GetInt32());
            Assert.False(pendingPage.GetProperty("items")[0].GetProperty("mfaEnabled").GetBoolean());

            var login = (await sessions.LoginAsync(target.Mobile, password))!;
            Assert.NotNull(login);
            Assert.Null(await sessions.CreatePhoneSessionAsync(target.Id));
            client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(endpoint, new { targetUserId = customer.Id })).StatusCode);
            var enrollmentResponse = await client.PostAsync("/api/dev/identity/mfa/enroll", null);
            Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
            var enrollment = (await enrollmentResponse.Content.ReadFromJsonAsync<MfaEnrollment>())!;
            client.DefaultRequestHeaders.Authorization = new("Bearer", root.Tokens.AccessToken);
            var inProgressPage = await client.GetFromJsonAsync<JsonElement>("/api/dev/customer-owners/accounts/page?role=level2&query=" + Uri.EscapeDataString(domestic));
            Assert.False(inProgressPage.GetProperty("items")[0].GetProperty("mfaEnabled").GetBoolean());
            Assert.True(inProgressPage.GetProperty("items")[0].GetProperty("hasMfaState").GetBoolean());
            // Simulate the enrollment endpoint's preflight check completing before reset.
            Assert.Equal(target.Id, await sessions.AuthenticateAsync(login.AccessToken));
            var gate = new EnrollmentLockInterceptor();
            await using var pausedDb = new ArginTaxPayerDbContext(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
                .UseNpgsql(cs).AddInterceptors(gate).Options);
            var pausedMfa = new IdentityMfaService(pausedDb, new IdentityMfaCrypto(key), new IdentitySessionService(pausedDb));
            var inFlightEnrollment = pausedMfa.BeginEnrollmentForSessionAsync(login.AccessToken);
            try
            {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id })).StatusCode);
            }
            finally { gate.Resume.TrySetResult(); }
            Assert.Null(await inFlightEnrollment);
            Assert.False(await db.IdentityMfaStates.AnyAsync(x => x.UserId == target.Id));
            var replacementLogin = (await sessions.LoginAsync(target.Mobile, password))!;
            Assert.NotNull(replacementLogin);
            client.DefaultRequestHeaders.Authorization = new("Bearer", replacementLogin.AccessToken);
            enrollmentResponse = await client.PostAsync("/api/dev/identity/mfa/enroll", null);
            Assert.Equal(HttpStatusCode.OK, enrollmentResponse.StatusCode);
            enrollment = (await enrollmentResponse.Content.ReadFromJsonAsync<MfaEnrollment>())!;
            var confirmed = await client.PostAsJsonAsync("/api/dev/identity/mfa/confirm", new { code = IdentityTotp.Generate(enrollment.Secret, DateTime.UtcNow) });
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            Assert.Null(await sessions.AuthenticateAsync(login.AccessToken));
            Assert.True(await db.IdentityUserRoles.AnyAsync(x => x.UserId == target.Id && x.Role == IdentityRoleNames.Level2));
            Assert.False(await db.IdentityUserRoles.AnyAsync(x => x.UserId == target.Id && x.Role == IdentityRoleNames.PendingLevel2));
            Assert.Null(await sessions.LoginAsync(target.Mobile, password));
            client.DefaultRequestHeaders.Authorization = new("Bearer", root.Tokens.AccessToken);
            // Two administrators' tabs must not both report a successful reset of the same factor.
            var concurrent = await Task.WhenAll(
                client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id }),
                client.PostAsJsonAsync(endpoint, new { targetUserId = target.Id }));
            Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.Conflict);
            db.IdentityUserRoles.Add(new IdentityUserRole { UserId = customer.Id, Role = IdentityRoleNames.Level1, AssignedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(endpoint, new { targetUserId = customer.Id })).StatusCode);
        }
        finally
        {
            await using var cleanup = new ArginTaxPayerDbContext(options);
            await IdentityTestServices.CleanupCustomerFixturesAsync(cleanup, ids);
        }
    }

    private sealed class EnrollmentLockInterceptor : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                Entered.TrySetResult();
                await Resume.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class ResetApiFactory(string connection, string key) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:ArginTaxPayerDb"] = connection, ["Identity:MfaEncryptionKey"] = key,
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
            app.Use(async (context, continuation) => { context.Connection.RemoteIpAddress = IPAddress.Loopback; await continuation(); });
            next(app);
        };
    }
}
