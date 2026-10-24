using System.Data;
using System.Security.Cryptography;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed record SandboxMemoryRegistration(string MemoryIdentifier, string PublicKeyPem);
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
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
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
SELECT memory_identifier, public_fingerprint_sha256, revision
FROM sandbox_fiscal_memories WHERE taxpayer_id=@taxpayer AND customer_user_id=@owner
""";
            Add(cmd,"taxpayer",taxpayerId);Add(cmd,"owner",authorized.Owner);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct)) return new(200,"not_registered");
            return new(200,"",new(reader.GetString(0),reader.GetString(1),reader.GetInt64(2),false,false));
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
VALUES (@id,@owner,@taxpayer,@memory,@spki,@fingerprint,1,@now,@now)
ON CONFLICT(customer_user_id,taxpayer_id) DO NOTHING
RETURNING id
""";
                Add(cmd,"id",Guid.NewGuid());Add(cmd,"owner",authorized.Owner);
                Add(cmd,"taxpayer",taxpayerId);Add(cmd,"memory",request.MemoryIdentifier);
                Add(cmd,"spki",spki);Add(cmd,"fingerprint",fingerprint);Add(cmd,"now",DateTime.UtcNow);
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
