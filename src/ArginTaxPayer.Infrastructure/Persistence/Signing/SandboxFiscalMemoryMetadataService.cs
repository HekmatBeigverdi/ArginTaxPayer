using System.Data;
using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using System.Text.Json;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed record SandboxMemoryRegistration(string MemoryIdentifier, string PublicKeyPem);
public sealed record SandboxPrivateKeyImportRequest(string PrivateKeyPem);
public sealed record SandboxMemorySafeView(string MemoryIdentifier, string PublicFingerprintSha256,
    long Revision, bool KeyCustodyReady, bool ConnectionTestReady);
public sealed record SandboxMemoryResult(int StatusCode, string Error, SandboxMemorySafeView? Value=null);

/// <summary>
/// Separate Sandbox public-key/identifier registration. Never reads, writes
/// or replaces production fiscal_memories or production signing key rows.
/// An untrusted browser-supplied PEM MUST be a valid 2048-bit RSA public key.
/// Private PKCS8 import requires protected custody; no authority HTTP is enabled.
/// </summary>
public sealed class SandboxFiscalMemoryMetadataService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope, IServiceProvider services)
{
    public async Task<SandboxMemoryResult> GetAsync(
        string? bearer, Guid taxpayerId, CancellationToken ct=default)
    {
        var authorized=await AuthorizeAsync(bearer,taxpayerId,ct);
        if(authorized.Code!=200) return new(authorized.Code,"sandbox_scope_denied");
        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened) await conn.OpenAsync(ct);
        try {
            await using var cmd=conn.CreateCommand();
            cmd.CommandText="""
SELECT memory_identifier, public_fingerprint_sha256, revision, private_envelope_json
FROM sandbox_fiscal_memories WHERE taxpayer_id=@taxpayer AND customer_user_id=@owner
""";
            Add(cmd,"taxpayer",taxpayerId);Add(cmd,"owner",authorized.Owner);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct)) return new(200,"not_registered");
            return new(200,"",new(reader.GetString(0),reader.GetString(1),reader.GetInt64(2),!reader.IsDBNull(3),false));
        } finally { if(opened) await conn.CloseAsync(); }
    }

    public async Task<SandboxMemoryResult> RegisterAsync(
        string? bearer,Guid taxpayerId,SandboxMemoryRegistration? request,
        CancellationToken ct=default)
    {
        var authorized=await AuthorizeAsync(bearer,taxpayerId,ct);
        if(authorized.Code!=200) return new(authorized.Code,"sandbox_scope_denied");
        if(request is null || !System.Text.RegularExpressions.Regex.IsMatch(
            request.MemoryIdentifier ?? "", "^[A-Z0-9]{6,32}$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
            request.PublicKeyPem is not {Length: > 100 and < 10000})
            return new(400,"invalid_sandbox_memory_input");
        byte[] spki;
        try {
            using var rsa=RSA.Create();
            rsa.ImportFromPem(request.PublicKeyPem.AsSpan());
            if(rsa.KeySize!=2048 || rsa.ExportParameters(false).Exponent is not { } exp ||
               !exp.AsSpan().SequenceEqual(new byte[]{1,0,1}))
                return new(400,"invalid_sandbox_public_key");
            spki=rsa.ExportSubjectPublicKeyInfo();
        } catch(CryptographicException) {
            return new(400,"invalid_sandbox_public_key");
        } catch(ArgumentException) {
            return new(400,"invalid_sandbox_public_key");
        }
        var fingerprint=Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();
        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened) await conn.OpenAsync(ct);
        try{
            await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            try {
                await using var guard=conn.CreateCommand();
                guard.Transaction=tx;
                guard.CommandText="SELECT current_database()";
                if((string?)await guard.ExecuteScalarAsync(ct)!="argintaxpayer_test")
                    return new(503,"sandbox_test_database_required");

                // A record without private custody is recoverable: replace only the
                // untrusted public metadata. Once custody exists, replacement is blocked.
                await using var cmd=conn.CreateCommand();
                cmd.Transaction=tx;
                cmd.CommandText="""
INSERT INTO sandbox_fiscal_memories
(id,customer_user_id,taxpayer_id,memory_identifier,public_spki_der,
 public_fingerprint_sha256,signing_key_version_id,origin_operation_id,revision,created_at_utc,updated_at_utc)
VALUES (@id,@owner,@taxpayer,@memory,@spki,@fingerprint,@keyid,@operationid,1,@now,@now)
ON CONFLICT(customer_user_id,taxpayer_id) DO UPDATE SET
 memory_identifier=EXCLUDED.memory_identifier,
 public_spki_der=EXCLUDED.public_spki_der,
 public_fingerprint_sha256=EXCLUDED.public_fingerprint_sha256,
 signing_key_version_id=EXCLUDED.signing_key_version_id,
 origin_operation_id=EXCLUDED.origin_operation_id,
 revision=sandbox_fiscal_memories.revision+1,
 updated_at_utc=EXCLUDED.updated_at_utc
WHERE sandbox_fiscal_memories.private_envelope_json IS NULL
RETURNING id, revision
""";
                Add(cmd,"id",Guid.NewGuid());Add(cmd,"owner",authorized.Owner);
                Add(cmd,"taxpayer",taxpayerId);Add(cmd,"memory",request.MemoryIdentifier);
                Add(cmd,"spki",spki);Add(cmd,"fingerprint",fingerprint);
                Add(cmd,"keyid",Guid.NewGuid());Add(cmd,"operationid",Guid.NewGuid());
                Add(cmd,"now",DateTime.UtcNow);
                await using var createdReader=await cmd.ExecuteReaderAsync(ct);
                if(!await createdReader.ReadAsync(ct))
                    return new(409,"sandbox_memory_already_registered");
                var revision=createdReader.GetInt64(1);
                await createdReader.DisposeAsync();
                await tx.CommitAsync(ct);
                return new(200,"",new(request.MemoryIdentifier,fingerprint,revision,false,false));
            } catch {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        } finally {if(opened) await conn.CloseAsync();}
    }


    /// <summary>
    /// Encrypted private PKCS8 import after the public key and Sandbox memory
    /// are registered. One-time only; rejects replacement.
    /// Requires the existing Phase09 key protector to be explicitly configured.
    /// No provider API requests are issued.
    /// </summary>
    public async Task<SandboxMemoryResult> ImportPrivateAsync(
        string? bearer, Guid taxpayerId, SandboxPrivateKeyImportRequest? request,
        CancellationToken ct = default)
    {
        var authorized=await AuthorizeAsync(bearer,taxpayerId,ct);
        if(authorized.Code!=200)return new(authorized.Code,"sandbox_scope_denied");
        if(request?.PrivateKeyPem is not {Length: > 100 and < 12000})
            return new(400,"invalid_private_key_input");
        var protector=services.GetService<KeyEnvelopeProtector>();
        if(protector is null)return new(503,"sandbox_key_protection_unavailable");
        byte[] pkcs8;
        byte[] spki;
        try {
            using var rsa=RSA.Create();
            rsa.ImportFromPem(request.PrivateKeyPem.AsSpan());
            if(rsa.KeySize!=2048 || rsa.ExportParameters(false).Exponent is not { } exponent ||
                !exponent.AsSpan().SequenceEqual(new byte[]{1,0,1}))
                return new(400,"invalid_sandbox_private_key");
            pkcs8=rsa.ExportPkcs8PrivateKey();
            spki=rsa.ExportSubjectPublicKeyInfo();
        } catch(CryptographicException) {
            return new(400,"invalid_sandbox_private_key");
        } catch(ArgumentException) {
            return new(400,"invalid_sandbox_private_key");
        }
        try {
            var conn=db.Database.GetDbConnection();
            var opened=conn.State!=ConnectionState.Open;
            if(opened)await conn.OpenAsync(ct);
            try {
                await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                try {
                    await using var read=conn.CreateCommand();
                    read.Transaction=tx;
                    read.CommandText="""
SELECT id, public_spki_der, private_envelope_json, signing_key_version_id,
 origin_operation_id, memory_identifier, public_fingerprint_sha256, revision
FROM sandbox_fiscal_memories
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer FOR UPDATE
""";
                    Add(read,"owner",authorized.Owner);Add(read,"taxpayer",taxpayerId);
                    if((string?)await ScalarDatabaseAsync(conn,tx,ct)!="argintaxpayer_test")
                        return new(503,"sandbox_test_database_required");
                    Guid memoryId,keyId,operationId;
                    byte[] registeredSpki;
                    string memoryCode,registeredHash;
                    long revision;
                    await using(var reader=await read.ExecuteReaderAsync(ct)){
                        if(!await reader.ReadAsync(ct))return new(404,"sandbox_memory_not_registered");
                        memoryId=reader.GetGuid(0);
                        registeredSpki=(byte[])reader.GetValue(1);
                        if(!reader.IsDBNull(2))return new(409,"sandbox_private_key_already_stored");
                        keyId=reader.GetGuid(3);operationId=reader.GetGuid(4);
                        memoryCode=reader.GetString(5);registeredHash=reader.GetString(6);
                        revision=reader.GetInt64(7);
                    }
                    if(!CryptographicOperations.FixedTimeEquals(spki,registeredSpki))
                        return new(409,"sandbox_key_pair_mismatch");
                    var identity=new KeyProtectionIdentity(authorized.Owner,taxpayerId,
                        memoryId,keyId,operationId,Sandbox:true);
                    var envelope=await protector.ProtectAsync(identity,pkcs8,ct);
                    var json=JsonSerializer.Serialize(envelope);
                    await using var update=conn.CreateCommand();
                    update.Transaction=tx;
                    update.CommandText="""
UPDATE sandbox_fiscal_memories
SET private_envelope_json=@envelope,revision=revision+1,updated_at_utc=@now
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND id=@id
 AND revision=@revision AND private_envelope_json IS NULL
RETURNING revision
""";
                    Add(update,"envelope",json);Add(update,"now",DateTime.UtcNow);
                    Add(update,"owner",authorized.Owner);Add(update,"taxpayer",taxpayerId);
                    Add(update,"id",memoryId);Add(update,"revision",revision);
                    var result=await update.ExecuteScalarAsync(ct);
                    if(result is not long newRevision)return new(409,"sandbox_key_registration_conflict");
                    await tx.CommitAsync(ct);
                    return new(200,"",new(memoryCode,registeredHash,newRevision,true,false));
                } catch {
                    await tx.RollbackAsync(CancellationToken.None);
                    throw;
                }
            } finally {if(opened)await conn.CloseAsync();}
        } catch(KeyProtectionException) {
            return new(503,"sandbox_key_protection_unavailable");
        } finally {
            CryptographicOperations.ZeroMemory(pkcs8);
            CryptographicOperations.ZeroMemory(spki);
        }
    }


    /// <summary>
    /// Offline RSA challenge sign+verify against registered Sandbox public key.
    /// Never contacts tax authority and never returns the private key.
    /// </summary>
    public async Task<SandboxMemoryResult> SelfTestAsync(
        string? bearer, Guid taxpayerId, CancellationToken ct=default)
    {
        var authorized=await AuthorizeAsync(bearer,taxpayerId,ct);
        if(authorized.Code!=200)return new(authorized.Code,"sandbox_scope_denied");
        var protector=services.GetService<KeyEnvelopeProtector>();
        if(protector is null)return new(503,"sandbox_key_protection_unavailable");
        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try {
            await using var cmd=conn.CreateCommand();
            cmd.CommandText="""
SELECT id,public_spki_der,private_envelope_json,signing_key_version_id,origin_operation_id
FROM sandbox_fiscal_memories
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
""";
            Add(cmd,"owner",authorized.Owner);Add(cmd,"taxpayer",taxpayerId);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct)||reader.IsDBNull(2))
                return new(409,"sandbox_private_key_not_ready");
            var memoryId=reader.GetGuid(0);
            var publicSpki=(byte[])reader.GetValue(1);
            var envelopeJson=reader.GetString(2);
            var keyId=reader.GetGuid(3);var operationId=reader.GetGuid(4);
            var identity=new KeyProtectionIdentity(authorized.Owner,taxpayerId,
                memoryId,keyId,operationId,Sandbox:true);
            ProtectedKeyEnvelope? envelope;
            try {envelope=JsonSerializer.Deserialize<ProtectedKeyEnvelope>(envelopeJson);}
            catch(JsonException){return new(503,"sandbox_key_integrity_failure");}
            if(envelope is null)return new(503,"sandbox_key_integrity_failure");
            using var secret=await protector.UnprotectAsync(identity,envelope,ct);
            using var privateRsa=RSA.Create();
            privateRsa.ImportPkcs8PrivateKey(secret.Bytes.Span,out var consumed);
            if(consumed!=secret.Bytes.Length)return new(503,"sandbox_key_integrity_failure");
            using var publicRsa=RSA.Create();
            publicRsa.ImportSubjectPublicKeyInfo(publicSpki,out var read);
            if(read!=publicSpki.Length)return new(503,"sandbox_key_integrity_failure");
            var challenge=RandomNumberGenerator.GetBytes(32);
            byte[]? signature=null;
            try {
                signature=privateRsa.SignData(challenge,HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                return publicRsa.VerifyData(challenge,signature,HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1)
                    ?new(200,"sandbox_offline_key_self_test_passed")
                    :new(503,"sandbox_key_integrity_failure");
            }finally {
                CryptographicOperations.ZeroMemory(challenge);
                if(signature is not null)CryptographicOperations.ZeroMemory(signature);
            }
        }catch(KeyProtectionException){
            return new(503,"sandbox_key_protection_unavailable");
        }catch(CryptographicException){
            return new(503,"sandbox_key_integrity_failure");
        }finally{if(opened)await conn.CloseAsync();}
    }


    /// <summary>
    /// Real read-only Sandbox authority authentication, opt-in and isolated.
    /// The selected Sandbox row owns the only fiscal code and key. Never
    /// accesses fiscal_memories or Production key envelopes.
    /// </summary>
    public async Task<(int StatusCode, string Error,
        ArginTaxPayer.Application.Signing.SigningConnectionTestResult? Result)>
        TestAuthorityAsync(string? bearer, Guid taxpayerId, CancellationToken ct = default)
    {
        var authorized=await AuthorizeAsync(bearer,taxpayerId,ct);
        if(authorized.Code!=200)
            return (authorized.Code,"sandbox_scope_denied",null);
        var probe=services.GetService<ArginTaxPayer.Infrastructure.Signing.Connection.SandboxAuthorityTokenProbe>();
        if(probe is null || !ArginTaxPayer.Infrastructure.Signing.Connection.SigningConnectionRegistration
            .SandboxConnectionConfigured(services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()))
            return (503,"sandbox_authority_api_not_verified",null);
        var protector=services.GetService<KeyEnvelopeProtector>();
        if(protector is null)return (503,"sandbox_key_protection_unavailable",null);
        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try {
            await using var cmd=conn.CreateCommand();
            cmd.CommandText="""
SELECT id,memory_identifier,public_spki_der,private_envelope_json,
 signing_key_version_id,origin_operation_id
FROM sandbox_fiscal_memories
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
""";
            Add(cmd,"owner",authorized.Owner);Add(cmd,"taxpayer",taxpayerId);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct) || reader.IsDBNull(3))
                return (409,"sandbox_private_key_not_ready",null);
            var id=reader.GetGuid(0);
            var code=reader.GetString(1);
            var publicSpki=(byte[])reader.GetValue(2);
            var envelopeJson=reader.GetString(3);
            var keyId=reader.GetGuid(4);
            var operationId=reader.GetGuid(5);
            var identity=new KeyProtectionIdentity(authorized.Owner,taxpayerId,
                id,keyId,operationId,Sandbox:true);
            ProtectedKeyEnvelope? envelope;
            try { envelope=JsonSerializer.Deserialize<ProtectedKeyEnvelope>(envelopeJson); }
            catch(JsonException) { return (503,"sandbox_key_integrity_failure",null); }
            if(envelope is null)return (503,"sandbox_key_integrity_failure",null);
            using var secret=await protector.UnprotectAsync(identity,envelope,ct);
            var result=await probe.TestAsync(code,secret,publicSpki,ct);
            return (200,"",result);
        } catch(KeyProtectionException) {
            return (503,"sandbox_key_protection_unavailable",null);
        } finally {if(opened)await conn.CloseAsync();}
    }

    private static async Task<object?> ScalarDatabaseAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        await using var cmd=connection.CreateCommand();
        cmd.Transaction=transaction;
        cmd.CommandText="SELECT current_database()";
        return await cmd.ExecuteScalarAsync(ct);
    }

    private async Task<(int Code,Guid Owner)> AuthorizeAsync(
        string? bearer,Guid taxpayerId,CancellationToken ct)
    {
        if(taxpayerId==Guid.Empty) return (400,Guid.Empty);
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated) return (401,Guid.Empty);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed) return (403,Guid.Empty);
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(x=>x.Id==taxpayerId && x.Status==ArginTaxPayer.Domain.Taxpayers.TaxpayerStatus.Active)
            .Select(x=>x.CustomerUserId).SingleOrDefaultAsync(ct);
        return owner is { } value && value!=Guid.Empty ? (200,value) : (404,Guid.Empty);
    }

    private static void Add(System.Data.Common.DbCommand command,string name,object value) {
        var p=command.CreateParameter();p.ParameterName=name;p.Value=value;command.Parameters.Add(p);
    }
}
