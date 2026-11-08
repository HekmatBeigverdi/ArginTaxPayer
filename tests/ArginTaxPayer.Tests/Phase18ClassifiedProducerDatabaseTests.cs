using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Domain.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Migrations;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ArginTaxPayer.Tests;

public sealed class Phase18ClassifiedProducerDatabaseTests
{
    [Theory]
    [InlineData(FiscalConnectionEnvironment.Production,"Production","tp.tax.gov.ir",1)]
    [InlineData(FiscalConnectionEnvironment.Sandbox,"Sandbox","sandboxrc.tax.gov.ir",2)]
    public async Task Real_producer_uses_scoped_custody_and_atomic_classified_outbox_without_enqueue(
        FiscalConnectionEnvironment environment,string name,string host,int persisted)
    {
        var raw=Environment.GetEnvironmentVariable("PHASE18_PRODUCER_TEST_CONNECTION");
        if(string.IsNullOrEmpty(raw))return;
        var parsed=new NpgsqlConnectionStringBuilder(raw);
        if(parsed.Host is not ("localhost" or "127.0.0.1") || parsed.Database!="argintaxpayer_test")
            throw new InvalidOperationException("Only loopback test PostgreSQL is permitted.");
        var schema="p18_producer_"+Guid.NewGuid().ToString("N");
        await using var connection=new NpgsqlConnection(raw);
        await connection.OpenAsync();
        await Exec(connection,$"CREATE SCHEMA {schema}");
        try
        {
            await Exec(connection,$"SET search_path TO {schema}");
            var options=new DbContextOptionsBuilder<ArginTaxPayerDbContext>().UseNpgsql(connection,
                npgsql=>npgsql.EnableRetryOnFailure(3)).Options;
            await using var db=new ArginTaxPayerDbContext(options);
            await Exec(connection,db.Database.GenerateCreateScript());
            await Exec(connection,"""
CREATE TABLE sandbox_fiscal_memories(id uuid PRIMARY KEY,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL);
CREATE TABLE invoice_dispatch_operations(id uuid PRIMARY KEY,customer_user_id uuid NOT NULL,taxpayer_id uuid NOT NULL,
 fiscal_memory_id uuid NOT NULL,invoice_id uuid NOT NULL,invoice_revision bigint NOT NULL,
 idempotency_key text NOT NULL UNIQUE,payload_sha256 text NOT NULL,state integer NOT NULL,state_revision bigint NOT NULL,
 rule_set_edition text NOT NULL,mapper_version text NOT NULL,signing_profile text NOT NULL,
 created_at_utc timestamptz NOT NULL,updated_at_utc timestamptz NOT NULL,authority_environment integer,
 CONSTRAINT fk_phase10_dispatch_sandbox_memory FOREIGN KEY(fiscal_memory_id) REFERENCES sandbox_fiscal_memories(id));
CREATE TABLE invoice_dispatch_attempts(id uuid PRIMARY KEY,operation_id uuid NOT NULL);
""");
            foreach(var sql in new AddAuthorityInvoiceFiscalReservations().UpOperations.OfType<SqlOperation>())
                await Exec(connection,sql.Sql);
            var now=DateTime.UtcNow;var owner=Guid.NewGuid();var taxpayer=Guid.NewGuid();var memory=Guid.NewGuid();
            var buyer=Guid.NewGuid();var unit=Guid.NewGuid();var release=Guid.NewGuid();var invoiceId=Guid.NewGuid();
            var accessToken=new string('a',43);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["ArginTaxPayer:EnableOperationalEndpoints"]="true",[$"{name}InvoiceLegacy:Enabled"]="true",
                [$"{name}InvoiceLegacy:OperatorApprovedEndpoint"]="true",[$"{name}InvoiceLegacy:ApiBaseUrl"]=$"https://{host}/req/api/",
                [$"{name}InvoiceLegacy:LastUsedSerial:{memory:N}"]="20",
                ["SigningKeyProtection:DatabaseMasterKey"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            }).Build();
            db.IdentityUsers.Add(new IdentityUser {Id=owner,NormalizedEmail="producer@example.invalid",IsActive=true,CreatedAtUtc=now,
                Roles=new List<IdentityUserRole>{new(){UserId=owner,Role=IdentityRoleNames.Customer,AssignedAtUtc=now}}});
            db.IdentitySessions.Add(new IdentitySession {Id=Guid.NewGuid(),UserId=owner,AccessTokenHash=Hash(accessToken),RefreshTokenHash=Hash("synthetic-refresh"),
                CreatedAtUtc=now,AccessExpiresAtUtc=now.AddHours(1),RefreshExpiresAtUtc=now.AddDays(1)});
            db.TaxpayerProfiles.Add(new TaxpayerProfile {Id=taxpayer,CustomerUserId=owner,Kind=TaxpayerKind.NaturalPerson,
                NormalizedIdentifier="1234567890",DisplayName="Synthetic taxpayer",CreatedAtUtc=now,UpdatedAtUtc=now});
            db.FiscalMemories.Add(new FiscalMemory {Id=memory,TaxpayerId=taxpayer,MemoryIdentifier="ABC123",AuthorityEnvironment=environment,
                Status=FiscalMemoryStatus.Active,CreatedAtUtc=now,UpdatedAtUtc=now});
            db.CustomerTaxpayerMemberships.Add(new CustomerTaxpayerMembership {Id=Guid.NewGuid(),CustomerUserId=owner,TaxpayerId=taxpayer,
                LinkedByUserId=owner,LinkedAtUtc=now});
            db.Parties.Add(new Party {Id=buyer,OwnerCustomerUserId=owner,Kind=PartyKind.LegalEntity,NormalizedIdentifier="98765432101",
                DisplayName="Synthetic buyer",CreatedAtUtc=now,UpdatedAtUtc=now});
            db.CustomerPartyLinks.Add(new CustomerPartyLink {Id=Guid.NewGuid(),CustomerUserId=owner,PartyId=buyer,LinkedAtUtc=now});
            db.InvoiceParticipantDetails.Add(new InvoiceParticipantDetails {Id=Guid.NewGuid(),CustomerUserId=owner,TaxpayerId=taxpayer,
                EconomicCode="12345678901",CreatedAtUtc=now,UpdatedAtUtc=now});
            db.CatalogReferenceReleases.Add(new CatalogReferenceRelease {Id=release,SourceDocument="synthetic",Edition="test",SourceSha256=new string('a',64),
                SourceReference="test-fixture",State=3,ImportedAtUtc=now,ActivatedAtUtc=now});
            db.OfficialMeasurementUnits.Add(new OfficialMeasurementUnit {Id=unit,ReleaseId=release,Code="1",Title="unit"});
            var issue=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc);
            var snapshot=JsonSerializer.Serialize(new {sourceWorkspaceId=invoiceId,sourceRevision=1,environment=name,fiscalMemoryId=memory,
                sandboxFiscalMemoryId=(Guid?)null,
                invoiceType=1,pattern=1,buyerPartyId=buyer,buyerPersonType=2,otherTaxAndLegalFeesRial=0,issueDateUtc=issue,
                settlementMethod=1,cashAmountRial=110,creditAmountRial=0,
                financialSummary=new {GrossRial=100,DiscountRial=0,AfterDiscountRial=100,PreviewVatRial=10,PreviewGrandTotalRial=110},
                lines=new[]{new InvoiceEntryLine(Guid.NewGuid(),"Synthetic service",1,100,0,10m,unit,OfficialIdentifierSnapshot:"1234567890123")}});
            db.InvoiceDocuments.Add(new StoredInvoice {Id=invoiceId,CustomerUserId=owner,TaxpayerId=taxpayer,Revision=1,
                State=InvoiceLifecycleState.Finalized,Subject=InvoiceSubject.Original,IssueDateUtc=issue,SnapshotJson=snapshot,
                SnapshotSha256=Hash(snapshot),UpdatedAtUtc=now});
            db.InvoiceDocumentRevisions.Add(new StoredInvoiceRevision {InvoiceId=invoiceId,CustomerUserId=owner,TaxpayerId=taxpayer,
                Revision=1,SnapshotJson=snapshot,SnapshotSha256=Hash(snapshot),CapturedAtUtc=now});
            db.InvoiceReviews.Add(new StoredInvoiceReview {InvoiceId=invoiceId,CustomerUserId=owner,TaxpayerId=taxpayer,
                Revision=1,State=InvoiceReviewState.Approved,UpdatedAtUtc=now});
            using var signing=RSA.Create(2048);using var authority=RSA.Create(2048);
            var keyId=Guid.NewGuid();var origin=Guid.NewGuid();var spki=signing.ExportSubjectPublicKeyInfo();
            db.SigningOperations.Add(new SigningOperation {Id=origin,CustomerUserId=owner,TaxpayerId=taxpayer,FiscalMemoryId=memory,
                OperationId=Guid.NewGuid(),ActorUserId=owner,OperationKind=1,ProfileId="synthetic",MetadataDigestSha256=new string('a',64),
                State=3,Revision=1,CreatedAtUtc=now,UpdatedAtUtc=now});
            db.SigningKeyVersions.Add(new SigningKeyVersion {Id=keyId,CustomerUserId=owner,TaxpayerId=taxpayer,FiscalMemoryId=memory,
                OriginOperationRecordId=origin,Origin=1,VersionNumber=1,ProfileId="synthetic",PublicFingerprintSha256=Convert.ToHexStringLower(SHA256.HashData(spki)),
                PublicSpkiDer=spki,CustodyReference="db://synthetic-key",ProtectionKeyVersion="db:v1",LifecycleState=2,PortalState=1,PublicMatch=1,
                Revision=1,CreatedAtUtc=now,UpdatedAtUtc=now});
            await db.SaveChangesAsync();
            using var protection=new DatabaseKeyProtectionProvider(config);
            var protector=new KeyEnvelopeProtector(protection);
            var custody=new ProtectedKeyEnvelopeStore(protector,new DatabaseProtectedKeyEnvelopeStore(db));
            var keyIdentity=new KeyProtectionIdentity(owner,taxpayer,memory,keyId,origin);
            var pkcs8=signing.ExportPkcs8PrivateKey();
            try { await custody.StageAsync(keyIdentity,pkcs8); }
            finally { CryptographicOperations.ZeroMemory(pkcs8); }
            var requests=new CredentialsHandler(host,authority);
            var services=new ServiceCollection().AddSingleton<IConfiguration>(config)
                .AddSingleton(new DatabaseLegacySigningKeyLoader(db,custody,protector))
                .AddSingleton(new LegacyInvoiceAccessTokenClient(config,()=>new HttpClient(requests,false)))
                .AddSingleton(new LegacySandboxServerEncryptionKeyClient(config,()=>new HttpClient(requests,false))).BuildServiceProvider();
            using(services)
            {
                var sessions=new IdentitySessionService(db);
                var scope=new TaxpayerScopeService(db,new IdentityAuthorizationService(db,sessions),new ParticipantScopeService(db));
                var producer=new Phase18ProductionInvoicePreparationService(db,scope,services);
                // Configuration and key failures happen before credential HTTP and leave no reservations.
                config[$"{name}InvoiceLegacy:ApiBaseUrl"]="https://untrusted.invalid/req/api/";
                var disabled=await producer.PrepareAsync(accessToken,taxpayer,invoiceId);
                Assert.Equal("production_legacy_endpoint_disabled",disabled.SafeCode);
                Assert.Equal(0,requests.Calls);
                config[$"{name}InvoiceLegacy:ApiBaseUrl"]=$"https://{host}/req/api/";
                await db.SigningKeyVersions.Where(k=>k.Id==keyId).ExecuteUpdateAsync(u=>u.SetProperty(k=>k.LifecycleState,(short)1));
                var missingKey=await producer.PrepareAsync(accessToken,taxpayer,invoiceId);
                Assert.Equal("production_legacy_key_unavailable",missingKey.SafeCode);
                Assert.Equal(0,requests.Calls);
                await db.SigningKeyVersions.Where(k=>k.Id==keyId).ExecuteUpdateAsync(u=>u.SetProperty(k=>k.LifecycleState,(short)2));
                Assert.Equal(0L,await Scalar(connection,"SELECT count(*) FROM authority_invoice_fiscal_reservations"));
                requests.RejectToken=true;
                var rejected=await producer.PrepareAsync(accessToken,taxpayer,invoiceId);
                Assert.Equal(InvoiceApiDecision.Invalid,rejected.Decision);
                Assert.Equal(2,requests.Calls);
                Assert.Equal(0L,await Scalar(connection,"SELECT count(*) FROM authority_invoice_fiscal_reservations"));
                Assert.Equal(0L,await Scalar(connection,"SELECT count(*) FROM invoice_dispatch_operations"));
                Assert.Equal(0L,await Scalar(connection,"SELECT count(*) FROM outbox_messages"));
                requests.RejectToken=false;
                var result=await producer.PrepareAsync(accessToken,taxpayer,invoiceId);
                Assert.Equal("legacy_outbox_prepared",result.SafeCode);
                Assert.Equal(InvoiceApiDecision.Success,result.Decision);
                Assert.Equal(4,requests.Calls);
                Assert.Equal(21L,await Scalar(connection,"SELECT serial FROM authority_invoice_fiscal_reservations"));
                Assert.Equal(persisted,await Scalar(connection,"SELECT authority_environment FROM invoice_dispatch_operations"));
                Assert.Equal(3,await Scalar(connection,"SELECT state FROM invoice_dispatch_operations"));
                var outbox=await db.OutboxMessages.AsNoTracking().SingleAsync();
                Assert.Equal($"invoice.{name.ToLowerInvariant()}.legacy.dispatch.requested",outbox.EventType);
                Assert.DoesNotContain("TEST-ONLY-ACCESS-TOKEN",outbox.PayloadJson);
                Assert.DoesNotContain("Synthetic service",outbox.PayloadJson);
                Assert.DoesNotContain("PRIVATE KEY",outbox.PayloadJson);
                var second=await producer.PrepareAsync(accessToken,taxpayer,invoiceId);
                Assert.Equal("already_prepared",second.SafeCode);
                Assert.Equal(result.OperationId,second.OperationId);
                Assert.Equal(4,requests.Calls); // no credential refresh or new packet on repeat
                Assert.Equal(1L,await Scalar(connection,"SELECT count(*) FROM invoice_dispatch_operations"));
                Assert.Equal(1L,await Scalar(connection,"SELECT count(*) FROM outbox_messages"));
                await Exec(connection,"UPDATE invoice_dispatch_operations SET state=4; INSERT INTO invoice_dispatch_attempts VALUES('11111111-1111-1111-1111-111111111111',(SELECT id FROM invoice_dispatch_operations))");
                Assert.Equal("dispatch_operation_requires_reconciliation",(await producer.PrepareAsync(accessToken,taxpayer,invoiceId)).SafeCode);
                Assert.Equal(4,requests.Calls);
                await db.FiscalMemories.Where(m=>m.Id==memory).ExecuteUpdateAsync(u=>u.SetProperty(m=>m.AuthorityEnvironment,(FiscalConnectionEnvironment?)null));
                Assert.Equal("production_memory_not_ready",(await producer.PrepareAsync(accessToken,taxpayer,invoiceId)).SafeCode);
                await db.FiscalMemories.Where(m=>m.Id==memory).ExecuteUpdateAsync(u=>u.SetProperty(m=>m.AuthorityEnvironment,
                    (FiscalConnectionEnvironment?)(environment==FiscalConnectionEnvironment.Production?FiscalConnectionEnvironment.Sandbox:FiscalConnectionEnvironment.Production)));
                Assert.NotEqual(InvoiceApiDecision.Success,(await producer.PrepareAsync(accessToken,taxpayer,invoiceId)).Decision);
                await db.FiscalMemories.Where(m=>m.Id==memory).ExecuteUpdateAsync(u=>u.SetProperty(m=>m.AuthorityEnvironment,(FiscalConnectionEnvironment?)environment));
                await db.CustomerTaxpayerMemberships.Where(m=>m.CustomerUserId==owner).ExecuteUpdateAsync(u=>u.SetProperty(m=>m.UnlinkedAtUtc,(DateTime?)DateTime.UtcNow).SetProperty(m=>m.UnlinkedByUserId,(Guid?)owner));
                Assert.Equal("membership_inactive",(await producer.PrepareAsync(accessToken,taxpayer,invoiceId)).SafeCode);
                Assert.Equal(4,requests.Calls);
            }
        }
        finally
        {
            await Exec(connection,"SET search_path TO public");
            await Exec(connection,$"DROP SCHEMA {schema} CASCADE");
        }
    }
    private sealed class CredentialsHandler(string host,RSA authority):HttpMessageHandler
    {
        public int Calls {get;private set;}
        public bool RejectToken {get;set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(host,request.RequestUri!.Host);Calls++;
            if(RejectToken && request.RequestUri.AbsolutePath.EndsWith("/GET_TOKEN",StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            object response=request.RequestUri.AbsolutePath switch {
                "/req/api/self-tsp/sync/GET_TOKEN"=>new {errors=Array.Empty<object>(),result=new {data=new {token="TEST-ONLY-ACCESS-TOKEN"}}},
                "/req/api/self-tsp/sync/GET_SERVER_INFORMATION"=>new {errors=Array.Empty<object>(),result=new {data=new {
                    publicKeys=new[]{new {id="synthetic-authority",key=Convert.ToBase64String(authority.ExportSubjectPublicKeyInfo())}}}}},
                _=>throw new InvalidOperationException("Invoice enqueue is forbidden in this fixture.")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(response),Encoding.UTF8,"application/json")});
        }
    }
    private static string Hash(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static async Task Exec(NpgsqlConnection c,string sql)
    {await using var command=new NpgsqlCommand(sql,c);await command.ExecuteNonQueryAsync();}
    private static async Task<object?> Scalar(NpgsqlConnection c,string sql)
    {await using var command=new NpgsqlCommand(sql,c);return await command.ExecuteScalarAsync();}
}
