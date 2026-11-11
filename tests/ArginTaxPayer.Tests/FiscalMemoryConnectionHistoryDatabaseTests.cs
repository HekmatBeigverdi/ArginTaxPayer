using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class FiscalMemoryConnectionHistoryDatabaseTests
{
    [Phase18OnboardingPostgresFact]
    public async Task Sandbox_success_survives_reload_without_overwriting_duplicate_registered_memory()
    {
        await using var f = await Fixture.Create();
        await using (var db = f.Context())
        {
            var history = new FiscalMemoryConnectionHistory(db);
            var binding = await history.CaptureSandboxAsync(f.Taxpayer, f.Sandbox);
            Assert.NotNull(binding);
            Assert.True(await history.RecordAsync(binding, "Authenticated", DateTime.UtcNow, DateTime.UtcNow));
        }
        await using var reloaded = f.Context();
        var service = new FiscalMemoryConnectionHistory(reloaded);
        var sandbox = await service.CaptureSandboxAsync(f.Taxpayer, f.Sandbox);
        Assert.Equal("Authenticated", (await service.ReadAsync(sandbox)).LastAttempt!.Status);
        var regular = await service.CaptureRegisteredAsync(f.Taxpayer, f.Registered);
        Assert.Null(regular!.KeyId);
        Assert.Null((await service.ReadAsync(regular)).LastAttempt);
        Assert.Null((await service.ReadAsync(sandbox! with { OwnerId = Guid.NewGuid() })).LastSuccess);
    }

    [Phase18OnboardingPostgresFact]
    public async Task Stale_probe_is_rejected_and_same_key_restore_requires_a_new_probe()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Context();
        var history = new FiscalMemoryConnectionHistory(db);
        var captured = await history.CaptureSandboxAsync(f.Taxpayer, f.Sandbox);
        Assert.True(await history.RecordAsync(captured, "Authenticated", DateTime.UtcNow, DateTime.UtcNow));
        await db.Database.ExecuteSqlRawAsync("UPDATE sandbox_fiscal_memories SET revision=revision+1");
        Assert.False(await history.RecordAsync(captured, "Authenticated", DateTime.UtcNow, DateTime.UtcNow));
        var current = await history.CaptureSandboxAsync(f.Taxpayer, f.Sandbox);
        Assert.Null((await history.ReadAsync(current)).LastSuccess);
    }

    [Phase18OnboardingPostgresFact]
    public async Task Newer_timeout_and_prior_success_are_both_retained_and_observations_are_append_only()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Context();
        var history = new FiscalMemoryConnectionHistory(db);
        var binding = await history.CaptureSandboxAsync(f.Taxpayer, f.Sandbox);
        var now = DateTime.UtcNow;
        Assert.True(await history.RecordAsync(binding, "TimedOut", now.AddSeconds(1), now.AddSeconds(2)));
        Assert.True(await history.RecordAsync(binding, "Authenticated", now, now.AddSeconds(3)));
        var read = await history.ReadAsync(binding);
        Assert.Equal("TimedOut", read.LastAttempt!.Status);
        Assert.NotNull(read.LastSuccess);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM fiscal_memory_connection_observations"));
    }

    [Phase18OnboardingPostgresFact]
    public async Task Actual_Sandbox_probe_records_success_and_custody_failure_against_the_key_it_used()
    {
        foreach(var corrupt in new[]{false,true})
        {
            await using var f=await Fixture.Create();await using var db=f.Context();
            using var provider=new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
            var protector=new KeyEnvelopeProtector(provider);using var rsa=RSA.Create(2048);
            var key=Guid.NewGuid();var operation=Guid.NewGuid();var spki=rsa.ExportSubjectPublicKeyInfo();
            var fingerprint=Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();
            var envelope=await protector.ProtectAsync(new(f.Owner,f.Taxpayer,f.Sandbox,key,operation,Sandbox:true),rsa.ExportPkcs8PrivateKey());
            if(corrupt)envelope.Tag[0]^=1;
            var json=JsonSerializer.Serialize(envelope);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sandbox_fiscal_memories SET signing_key_version_id={key},origin_operation_id={operation},public_spki_der={spki},public_fingerprint_sha256={fingerprint},private_envelope_json={json}");
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["SandboxConnectionTest:Enabled"]="true",["SandboxConnectionTest:OperatorApprovedExperimentalApi"]="true",
                ["SandboxConnectionTest:ApiBaseUrl"]="https://sandboxrc.tax.gov.ir/req/api/"}).Build();
            using var services=new ServiceCollection().AddSingleton<IConfiguration>(config).AddSingleton(protector)
                .AddSingleton<IHttpClientFactory>(new SyntheticHttpFactory()).AddPhase10SandboxConnection().BuildServiceProvider();
            var scope=new TaxpayerScopeService(db,new(db,new(db)),new ParticipantScopeService(db));
            var service=new SandboxFiscalMemoryMetadataService(db,scope,services);
            var result=await service.TestAuthorityAsync(f.Token,f.Taxpayer);
            Assert.Equal(corrupt?503:200,result.StatusCode);
            if(!corrupt){Assert.Equal(SigningConnectionTestStatus.Authenticated,result.Result!.Status);Assert.True(result.ConnectionRecorded);}
            var history=new FiscalMemoryConnectionHistory(db);
            var saved=await history.ReadAsync(await history.CaptureSandboxAsync(f.Taxpayer,f.Sandbox));
            Assert.Equal(corrupt?"VaultUnavailable":"Authenticated",saved.LastAttempt?.Status);
        }
    }

    [Phase18OnboardingPostgresFact]
    public async Task Unclassified_probe_does_not_become_verified_in_another_environment()
    {
        await using var f=await Fixture.Create();await using var db=f.Context();
        await db.Database.ExecuteSqlRawAsync("UPDATE fiscal_memories SET authority_environment=NULL");
        var history=new FiscalMemoryConnectionHistory(db);var binding=await history.CaptureRegisteredAsync(f.Taxpayer,f.Registered);
        Assert.False(await history.RecordAsync(binding,"Authenticated",DateTime.UtcNow,DateTime.UtcNow));
        Assert.Null((await history.ReadAsync(binding)).LastSuccess);
    }

    [Phase18OnboardingPostgresFact]
    public async Task Autoactivation_requires_the_probed_key_and_records_both_classified_and_first_classification_transitions()
    {
        foreach(var unclassified in new[]{false,true})foreach(var changeKey in new[]{false,true})
        {
            await using var f=await Fixture.Create();await using var db=f.Context();
            await db.Database.ExecuteSqlRawAsync("UPDATE fiscal_memories SET authority_environment="+(unclassified?"NULL":"1"));
            var key=Guid.NewGuid();var spki=new byte[]{1,2,3};var fingerprint=new string('b',64);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO signing_key_versions(id,customer_user_id,taxpayer_id,fiscal_memory_id,lifecycle_state,version_number,revision,public_fingerprint_sha256,public_spki_der) VALUES ({key},{f.Owner},{f.Taxpayer},{f.Registered},2,1,1,{fingerprint},{spki})");
            var history=new FiscalMemoryConnectionHistory(db);var captured=await history.CaptureRegisteredAsync(f.Taxpayer,f.Registered);
            if(changeKey)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE signing_key_versions SET id={Guid.NewGuid()}");
            var scope=new TaxpayerScopeService(db,new(db,new(db)),new ParticipantScopeService(db));
            var service=new FiscalMemoryAuthorityOnboardingService(db,scope,new UnavailableSigningReadOnlyConnectionTest(),new InvoiceSubscriptionGate(db));
            var result=SigningConnectionTestResult.Create(SigningConnectionTestStatus.Authenticated,DateTime.UtcNow) with {
                FiscalInformation=new("synthetic","ACTIVE","synthetic","0013541339")};
            var activated=await service.ActivateAfterSuccessfulConnectionTestAsync(f.Token,f.Taxpayer,f.Registered,
                unclassified?FiscalConnectionEnvironment.Production:null,result,expectedBinding:captured);
            Assert.Equal(!changeKey,activated);
            var memory=await db.FiscalMemories.AsNoTracking().SingleAsync();
            Assert.Equal(changeKey?FiscalMemoryStatus.PendingVerification:FiscalMemoryStatus.Active,memory.Status);
            if(!changeKey){
                var current=await history.CaptureRegisteredAsync(f.Taxpayer,f.Registered);
                Assert.True(ConnectionHistoryPolicy.CanFollowActivation(captured!,current!,unclassified?1:null));
                Assert.True(await history.RecordAsync(current,"Authenticated",DateTime.UtcNow,DateTime.UtcNow));
            }
        }
    }

    [Phase18OnboardingPostgresFact]
    public async Task Actual_same_key_restore_preserves_identity_and_rejects_another_key_or_expired_owner_login()
    {
        await using var f=await Fixture.Create();await using var db=f.Context();
        using var provider=new Phase09SigningKeyProtectionTests.SyntheticProtectionProvider();
        var protector=new KeyEnvelopeProtector(provider);using var rsa=RSA.Create(2048);using var other=RSA.Create(2048);
        var key=Guid.NewGuid();var operation=Guid.NewGuid();var spki=rsa.ExportSubjectPublicKeyInfo();
        var fingerprint=Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();
        var envelope=await protector.ProtectAsync(new(f.Owner,f.Taxpayer,f.Sandbox,key,operation,Sandbox:true),rsa.ExportPkcs8PrivateKey());
        var json=JsonSerializer.Serialize(envelope);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sandbox_fiscal_memories SET signing_key_version_id={key},origin_operation_id={operation},public_spki_der={spki},public_fingerprint_sha256={fingerprint},private_envelope_json={json}");
        using var services=new ServiceCollection().AddSingleton(protector).BuildServiceProvider();
        var scope=new TaxpayerScopeService(db,new(db,new(db)),new ParticipantScopeService(db));
        var service=new SandboxFiscalMemoryMetadataService(db,scope,services);
        var restored=await service.RestorePrivateAsync(f.Token,f.Taxpayer,new(f.Sandbox,rsa.ExportPkcs8PrivateKeyPem(),1,true));
        Assert.Equal(200,restored.StatusCode);Assert.Equal(2,restored.Value!.Revision);
        var binding=await new FiscalMemoryConnectionHistory(db).CaptureSandboxAsync(f.Taxpayer,f.Sandbox);
        Assert.Equal(key,binding!.KeyId);Assert.Equal(f.Sandbox,binding.MemoryId);Assert.Equal(fingerprint,binding.Fingerprint);
        Assert.Equal(409,(await service.RestorePrivateAsync(f.Token,f.Taxpayer,new(f.Sandbox,other.ExportPkcs8PrivateKeyPem(),2,true))).StatusCode);
        Assert.Equal(409,(await service.RestorePrivateAsync(f.Token,f.Taxpayer,new(f.Sandbox,rsa.ExportPkcs8PrivateKeyPem(),1,true))).StatusCode);
        await db.Database.ExecuteSqlRawAsync("UPDATE identity_sessions SET created_at_utc=now()-interval '10 minutes'");
        Assert.Equal(403,(await service.RestorePrivateAsync(f.Token,f.Taxpayer,new(f.Sandbox,rsa.ExportPkcs8PrivateKeyPem(),2,true))).StatusCode);
        Assert.Equal(2,(await new FiscalMemoryConnectionHistory(db).CaptureSandboxAsync(f.Taxpayer,f.Sandbox))!.MemoryRevision);
    }

    private sealed class SyntheticHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)=>new(new Handler());
        private sealed class Handler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.AbsolutePath.EndsWith("GET_TOKEN")
                    ? "{\"result\":{\"data\":{\"token\":\"synthetic-token\"}}}"
                    : "{\"result\":{\"data\":{\"nameTrade\":\"synthetic\",\"fiscalStatus\":\"ACTIVE\",\"economicCode\":\"synthetic\",\"nationalId\":\"0013541339\"}}}")});
        }
    }

    private sealed class Fixture(string connection, string schema) : IAsyncDisposable
    {
        private readonly string schema = schema;
        private readonly string connection = connection;
        public string Token { get; } = new string('s',43);
        public Guid Taxpayer { get; } = Guid.NewGuid();
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Sandbox { get; } = Guid.NewGuid();
        public Guid Registered { get; } = Guid.NewGuid();
        public ArginTaxPayerDbContext Context() => new(new DbContextOptionsBuilder<ArginTaxPayerDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options);
        public static async Task<Fixture> Create()
        {
            var raw = Environment.GetEnvironmentVariable("PHASE18_ONBOARDING_TEST_CONNECTION") ??
                Environment.GetEnvironmentVariable("ConnectionStrings__ArginTaxPayerDb")!;
            var parsed = new NpgsqlConnectionStringBuilder(raw);
            if (parsed.Host is not ("127.0.0.1" or "localhost") || parsed.Database != "argintaxpayer_test")
                throw new InvalidOperationException("Only loopback argintaxpayer_test disposable schemas are allowed.");
            var f = new Fixture(raw, "memory_connection_" + Guid.NewGuid().ToString("N"));
            await using var root = new NpgsqlConnection(raw); await root.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE SCHEMA {f.schema}", root); await create.ExecuteNonQueryAsync();
            try
            {
                await using var db = f.Context();
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE identity_users(id uuid PRIMARY KEY,is_active boolean NOT NULL DEFAULT true);
                    CREATE TABLE identity_user_roles(user_id uuid,role text);
                    CREATE TABLE identity_mfa_states(user_id uuid,enabled_at_utc timestamptz);
                    CREATE TABLE identity_sessions(user_id uuid,access_token_hash text,revoked_at_utc timestamptz,created_at_utc timestamptz,access_expires_at_utc timestamptz,refresh_expires_at_utc timestamptz);
                    CREATE TABLE customer_commercial_owners(customer_user_id uuid,partner_level2_user_id uuid);
                    CREATE TABLE customer_taxpayer_memberships(customer_user_id uuid,taxpayer_id uuid,unlinked_at_utc timestamptz);
                    CREATE TABLE customer_subscriptions(id uuid,customer_user_id uuid,kind integer,state integer,activated_at_utc timestamptz,expires_at_utc timestamptz);
                    CREATE TABLE invoice_dispatch_operations(fiscal_memory_id uuid);
                    CREATE TABLE fiscal_memory_authority_events(id uuid,fiscal_memory_id uuid,taxpayer_id uuid,actor_user_id uuid,authority_environment integer,
                      action text,from_status integer,to_status integer,memory_revision bigint,recorded_at_utc timestamptz);
                    CREATE TABLE taxpayer_profiles(id uuid PRIMARY KEY,customer_user_id uuid,status integer DEFAULT 1,normalized_identifier text DEFAULT '0013541339');
                    CREATE TABLE fiscal_memories(id uuid PRIMARY KEY,taxpayer_id uuid,memory_identifier text,authority_environment integer,revision bigint,status integer DEFAULT 1,last_connection_test_succeeded boolean,last_connection_test_at_utc timestamptz,created_at_utc timestamptz DEFAULT now(),updated_at_utc timestamptz DEFAULT now());
                    CREATE TABLE signing_key_versions(id uuid PRIMARY KEY,customer_user_id uuid,taxpayer_id uuid,fiscal_memory_id uuid,lifecycle_state smallint,
                      version_number bigint,revision bigint,public_fingerprint_sha256 text,public_spki_der bytea);
                    CREATE TABLE signing_key_selections(customer_user_id uuid,taxpayer_id uuid,fiscal_memory_id uuid,key_version_id uuid);
                    CREATE TABLE sandbox_fiscal_memories(id uuid PRIMARY KEY,customer_user_id uuid,taxpayer_id uuid,memory_identifier text,revision bigint,
                      signing_key_version_id uuid,public_fingerprint_sha256 text,public_spki_der bytea,private_envelope_json text,origin_operation_id uuid,updated_at_utc timestamptz DEFAULT now());
                    """);
                await using (var tx = await db.Database.BeginTransactionAsync())
                {
                    foreach (var sql in new AddFiscalMemoryConnectionObservations().UpOperations.OfType<SqlOperation>())
                        await db.Database.ExecuteSqlRawAsync(sql.Sql);
                    await tx.CommitAsync();
                }
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_users(id) VALUES ({f.Owner})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO taxpayer_profiles(id,customer_user_id) VALUES ({f.Taxpayer},{f.Owner})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO fiscal_memories(id,taxpayer_id,memory_identifier,authority_environment,revision) VALUES ({f.Registered},{f.Taxpayer},'ABC123',2,1)");
                var tokenHash=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(f.Token))).ToLowerInvariant();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_user_roles VALUES ({f.Owner},'Customer')");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity_sessions VALUES ({f.Owner},{tokenHash},NULL,now(),now()+interval '1 hour',now()+interval '2 hours')");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO customer_taxpayer_memberships VALUES ({f.Owner},{f.Taxpayer},NULL)");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO customer_subscriptions VALUES ({Guid.NewGuid()},{f.Owner},1,2,now()-interval '1 day',now()+interval '1 day')");
                var key = Guid.NewGuid(); var fingerprint = new string('a',64);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO sandbox_fiscal_memories(id,customer_user_id,taxpayer_id,memory_identifier,revision,signing_key_version_id,public_fingerprint_sha256) VALUES ({f.Sandbox},{f.Owner},{f.Taxpayer},'ABC123',1,{key},{fingerprint})");
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(this.connection); await connection.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schema} CASCADE", connection); await drop.ExecuteNonQueryAsync();
        }
    }
}
