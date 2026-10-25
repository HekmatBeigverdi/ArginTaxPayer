using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

public enum SandboxSerialStatus { Reserved, IdenticalReplay, Denied, NotFound, NotFinalized, Conflict, Invalid }
public sealed record SandboxSerialResult(SandboxSerialStatus Status,
    Guid? ReservationId=null, string? FiscalTaxId=null, long? Serial=null);

/// <summary>
/// PostgreSQL serial allocation for a single legally finalized Sandbox invoice.
/// Must never be invoked for unsigned or unapproved drafts, and itself provides
/// NO invoice submission authorization. A serial is reserved, never reused.
/// </summary>
public sealed class SandboxInvoiceFiscalSerialRepository(
    ArginTaxPayerDbContext db,TaxpayerScopeService scope)
{
    public async Task<SandboxSerialResult> ReserveAsync(string? bearer,
        Guid taxpayerId,Guid invoiceId,long expectedRevision,CancellationToken ct=default)
    {
        if(taxpayerId==Guid.Empty||invoiceId==Guid.Empty||expectedRevision<1)
            return new(SandboxSerialStatus.Invalid);
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return new(SandboxSerialStatus.Denied);
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(t=>t.Id==taxpayerId).Select(t=>t.CustomerUserId)
            .SingleOrDefaultAsync(ct);
        if(owner is null||owner==Guid.Empty)return new(SandboxSerialStatus.NotFound);

        var conn=db.Database.GetDbConnection();
        var opened=conn.State!=ConnectionState.Open;
        if(opened)await conn.OpenAsync(ct);
        try{
            await using var tx=await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            try{
                var database=await Scalar(conn,tx,"SELECT current_database()",ct);
                if(database is not string name||name!="argintaxpayer_test")
                    return new(SandboxSerialStatus.Denied);

                // The memory row is the DB-wide reservation mutex for this
                // exact sandbox fiscal memory (no in-process lock).
                await using var memoryCmd=Command(conn,tx,"""
SELECT id,memory_identifier
FROM sandbox_fiscal_memories
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer
 AND private_envelope_json IS NOT NULL
FOR UPDATE
""",("owner",owner.Value),("taxpayer",taxpayerId));
                Guid memoryId;
                string memoryCode;
                await using(var reader=await memoryCmd.ExecuteReaderAsync(ct)){
                    if(!await reader.ReadAsync(ct))
                        return new(SandboxSerialStatus.NotFound);
                    memoryId=reader.GetGuid(0);
                    memoryCode=reader.GetString(1);
                }
                if(memoryCode.Length!=6)
                    return new(SandboxSerialStatus.Invalid);

                await using var invoiceCmd=Command(conn,tx,"""
SELECT revision,state,issue_date_utc,snapshot_sha256,snapshot_json
FROM invoice_documents
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND id=@invoice
FOR UPDATE
""",("owner",owner.Value),("taxpayer",taxpayerId),("invoice",invoiceId));
                long revision;
                int state;
                DateTime issue;
                string hash,snapshot;
                await using(var reader=await invoiceCmd.ExecuteReaderAsync(ct)){
                    if(!await reader.ReadAsync(ct))
                        return new(SandboxSerialStatus.NotFound);
                    revision=reader.GetInt64(0);state=reader.GetInt32(1);
                    issue=reader.GetDateTime(2);hash=reader.GetString(3);
                    snapshot=reader.GetString(4);
                }
                if(revision!=expectedRevision)
                    return new(SandboxSerialStatus.Conflict);
                if(state!=(int)InvoiceLifecycleState.Finalized)
                    return new(SandboxSerialStatus.NotFinalized);
                // Bind reservation to the server-owned, signed-off snapshot;
                // never accept memory or hash from caller.
                try{
                    using var json=System.Text.Json.JsonDocument.Parse(snapshot);
                    var root=json.RootElement;
                    if(root.ValueKind!=System.Text.Json.JsonValueKind.Object ||
                       !root.TryGetProperty("environment",out var env) ||
                       env.ValueKind!=System.Text.Json.JsonValueKind.String ||
                       env.GetString()!="Sandbox" ||
                       !root.TryGetProperty("sandboxFiscalMemoryId",out var mid) ||
                       mid.ValueKind!=System.Text.Json.JsonValueKind.String ||
                       !mid.TryGetGuid(out var linkedMemory) ||
                       linkedMemory!=memoryId)
                        return new(SandboxSerialStatus.Denied);
                }catch(System.Text.Json.JsonException){
                    return new(SandboxSerialStatus.Denied);
                }

                await using var previous=Command(conn,tx,"""
SELECT id,invoice_revision,approved_snapshot_sha256,fiscal_tax_id,serial
FROM sandbox_invoice_fiscal_reservations
WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice
""",("owner",owner.Value),("taxpayer",taxpayerId),("invoice",invoiceId));
                await using(var reader=await previous.ExecuteReaderAsync(ct)){
                    if(await reader.ReadAsync(ct)){
                        var same=reader.GetInt64(1)==revision && reader.GetString(2)==hash;
                        return same ? new(SandboxSerialStatus.IdenticalReplay,
                            reader.GetGuid(0),reader.GetString(3),reader.GetInt64(4))
                            : new(SandboxSerialStatus.Conflict);
                    }
                }
                var max=await Scalar(conn,tx,"""
SELECT COALESCE(MAX(serial),0) FROM sandbox_invoice_fiscal_reservations
WHERE sandbox_memory_id=@memory
""",ct,("memory",memoryId));
                long serial=checked(Convert.ToInt64(max)+1);
                if(serial>0xffffffffffL)return new(SandboxSerialStatus.Invalid);
                var taxId=Phase10FiscalInvoiceIdentifier.Generate(memoryCode,serial,issue);
                var id=Guid.NewGuid();
                await using var insert=Command(conn,tx,"""
INSERT INTO sandbox_invoice_fiscal_reservations (
id,customer_user_id,taxpayer_id,sandbox_memory_id,invoice_id,
invoice_revision,approved_snapshot_sha256,serial,fiscal_tax_id,
issue_date_utc,created_at_utc)
VALUES (@id,@owner,@taxpayer,@memory,@invoice,@revision,@hash,@serial,@taxid,@issue,@at)
""",("id",id),("owner",owner.Value),("taxpayer",taxpayerId),
                    ("memory",memoryId),("invoice",invoiceId),("revision",revision),
                    ("hash",hash),("serial",serial),("taxid",taxId),
                    ("issue",issue),("at",DateTime.UtcNow));
                await insert.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
                return new(SandboxSerialStatus.Reserved,id,taxId,serial);
            }catch(PostgresException e) when(e.SqlState is
                PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure){
                await tx.RollbackAsync(CancellationToken.None);
                return new(SandboxSerialStatus.Conflict);
            }
        }finally{if(opened)await conn.CloseAsync();}
    }

    private static DbCommand Command(DbConnection conn,DbTransaction tx,string sql,
        params (string Name,object Value)[] values)
    {
        var cmd=conn.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;
        foreach(var (name,value) in values){
            var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
        }
        return cmd;
    }
    private static async Task<object?> Scalar(DbConnection conn,DbTransaction tx,
        string sql,CancellationToken ct,params (string Name,object Value)[] values)
    {
        await using var cmd=Command(conn,tx,sql,values);
        return await cmd.ExecuteScalarAsync(ct);
    }
}
