using System.Data;
using System.Data.Common;
using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// PostgreSQL-only admission store for Phase10. Does NOT send packets or sign.
/// A trusted application service must authenticate and authorize the customer,
/// taxpayer, fiscal memory and finalized invoice before calling this method.
/// </summary>
public sealed class InvoiceDispatchAdmissionRepository(ArginTaxPayerDbContext db)
{
    public async Task<DispatchAdmissionResult> ReserveAsync(
        InvoiceDispatchAdmission request, string ruleSetEdition,
        string mapperVersion, string signingProfile,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(ruleSetEdition) || ruleSetEdition.Length > 128 ||
            string.IsNullOrWhiteSpace(mapperVersion) || mapperVersion.Length > 80 ||
            string.IsNullOrWhiteSpace(signingProfile) || signingProfile.Length > 128)
            throw new ArgumentException("Valid pinned rule, mapper and signing profile required.");

        // Never take a customer-supplied authorization decision. Upstream must
        // resolve actor/owner and verify the finalized, approved invoice revision.
        // Restrict this implementation to the isolated test database by design.
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                var databaseName = await ScalarAsync<string>(connection, transaction,
                    "SELECT current_database()", Array.Empty<(string, object)>(), ct);
                if (databaseName != "argintaxpayer_test")
                    throw new InvalidOperationException("Dispatch admission restricted to test database.");

                var now = DateTime.UtcNow;
                const string sql = """
INSERT INTO invoice_dispatch_operations (
 id,customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision,
 idempotency_key,payload_sha256,state,state_revision,rule_set_edition,mapper_version,
 signing_profile,created_at_utc,updated_at_utc)
SELECT @id,@customer,@taxpayer,@memory,@invoice,@revision,
 @key,@hash,1,1,@rules,@mapper,@profile,@now,@now
WHERE EXISTS (
 SELECT 1 FROM invoice_documents i
 WHERE i.id=@invoice AND i.customer_user_id=@customer
 AND i.taxpayer_id=@taxpayer AND i.revision=@revision AND i.state=3
)
ON CONFLICT DO NOTHING
RETURNING id
""";
                var args = new (string, object)[]
                {
                    ("id",request.OperationId),("customer",request.CustomerUserId),
                    ("taxpayer",request.TaxpayerId),("memory",request.FiscalMemoryId),
                    ("invoice",request.InvoiceId),("revision",request.InvoiceRevision),
                    ("key",request.IdempotencyKey),("hash",request.PayloadSha256),
                    ("rules",ruleSetEdition),("mapper",mapperVersion),
                    ("profile",signingProfile),("now",now)
                };
                var inserted = await ScalarAsync<Guid?>(connection, transaction, sql, args, ct);
                if (inserted.HasValue)
                {
                    await transaction.CommitAsync(ct);
                    return new(DispatchAdmissionResultStatus.Created, inserted);
                }

                // ON CONFLICT waits for concurrent winners, then read the existing
                // scoped idempotency row. A different key for the same invoice
                // revision cannot create another send operation.
                const string lookup = """
SELECT id,fiscal_memory_id,invoice_id,invoice_revision,payload_sha256,
       rule_set_edition,mapper_version,signing_profile
FROM invoice_dispatch_operations
WHERE customer_user_id=@customer AND taxpayer_id=@taxpayer AND idempotency_key=@key
FOR UPDATE
""";
                await using var command = CreateCommand(connection, transaction, lookup,
                    ("customer",request.CustomerUserId),
                    ("taxpayer",request.TaxpayerId),("key",request.IdempotencyKey));
                await using var reader = await command.ExecuteReaderAsync(ct);
                DispatchAdmissionResult result;
                if (!await reader.ReadAsync(ct))
                    result = new(DispatchAdmissionResultStatus.Conflict);
                else
                {
                    var existingId = reader.GetGuid(0);
                    var matches = reader.GetGuid(1)==request.FiscalMemoryId &&
                                  reader.GetGuid(2)==request.InvoiceId &&
                                  reader.GetInt64(3)==request.InvoiceRevision &&
                                  reader.GetString(4)==request.PayloadSha256 &&
                                  reader.GetString(5)==ruleSetEdition &&
                                  reader.GetString(6)==mapperVersion &&
                                  reader.GetString(7)==signingProfile;
                    result = matches
                        ? new(DispatchAdmissionResultStatus.Replayed, existingId)
                        : new(DispatchAdmissionResultStatus.Conflict);
                }
                await reader.DisposeAsync();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }
    }

    private static DbCommand CreateCommand(DbConnection conn, DbTransaction tx,
        string sql, params (string Name, object Value)[] parameters)
    {
        var cmd=conn.CreateCommand();
        cmd.Transaction=tx;
        cmd.CommandText=sql;
        foreach (var (name,value) in parameters)
        {
            var p=cmd.CreateParameter();
            p.ParameterName=name;
            p.Value=value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }

    private static async Task<T?> ScalarAsync<T>(DbConnection conn, DbTransaction tx,
        string sql, (string Name, object Value)[] parameters, CancellationToken ct)
    {
        await using var cmd=CreateCommand(conn,tx,sql,parameters);
        var value=await cmd.ExecuteScalarAsync(ct);
        if (value is null || value is DBNull) return default;
        return (T)value;
    }
}

public enum DispatchAdmissionResultStatus { Created=1, Replayed=2, Conflict=3 }

public sealed record DispatchAdmissionResult(
    DispatchAdmissionResultStatus Status, Guid? OperationId = null);
