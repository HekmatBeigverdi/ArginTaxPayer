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
/// There is deliberately no signing/private-key API until vault custody exists.
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

                // No updates, no deletion and no replacement; registration is immutable.
                await using var cmd=conn.CreateCommand();
                cmd.Transaction=tx;
                cmd.CommandText="""
INSERT INTO sandbox_fiscal_memories
(id,customer_user_id,taxpayer_id,memory_identifier,public_spki_der,
 public_fingerprint_sha256,revision,created_at_utc,updated_at_utc)
VALUES (@id,@owner,@taxpayer,@memory,@spki,@fingerprint,@keyid,@operationid,1,@now,@now)
ON CONFLICT(customer_user_id,taxpayer_id) DO NOTHING
RETURNING id
""";
                Add(cmd,"id",Guid.NewGuid());Add(cmd,"owner",authorized.Owner);
                Add(cmd,"taxpayer",taxpayerId);Add(cmd,"memory",request.MemoryIdentifier);
                Add(cmd,"spki",spki);Add(cmd,"fingerprint",fingerprint);
                Add(cmd,"keyid",Guid.NewGuid());Add(cmd,"operationid",Guid.NewGuid());
                Add(cmd,"now",DateTime.UtcNow);
                var created=await cmd.ExecuteScalarAsync(ct);
                await tx.CommitAsync(ct);
                return created is Guid ?
                    new(201,"",new(request.MemoryIdentifier,fingerprint,1,false,false)):
                    new(409,"sandbox_memory_already_registered");
            } catch {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        } finally {if(opened) await conn.CloseAsync();}
    }


    /// <summary>
    /// Encrypted private PKCS8 import after the public key and Sandbox memory
    /// are registered. Idempotent only for the same key, rejects replacement.
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
