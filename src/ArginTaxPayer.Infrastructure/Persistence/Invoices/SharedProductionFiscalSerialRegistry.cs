using System.Data;
using ArginTaxPayer.Domain.Invoices;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>A committed reservation is never recycled, even if the caller rolls back.</summary>
internal static class SharedProductionFiscalSerialRegistry
{
    internal static async Task<long> ReserveAsync(string connectionString, StoredInvoice invoice,
        string memoryCode, long lowerBound, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // All clients serialize by actual authority memory code, independent of local DB UUIDs.
        await using var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@memory,1801))", connection, transaction);
        gate.Parameters.AddWithValue("memory", memoryCode);
        await gate.ExecuteNonQueryAsync(ct);
        await using var replay = new NpgsqlCommand("""
SELECT serial,snapshot_sha256,issue_date_utc FROM production_shared_serial_reservations
WHERE memory_code=@memory AND invoice_id=@invoice AND customer_user_id=@owner
""", connection, transaction);
        replay.Parameters.AddWithValue("memory", memoryCode);
        replay.Parameters.AddWithValue("invoice", invoice.Id);
        replay.Parameters.AddWithValue("owner", invoice.CustomerUserId);
        await using (var reader = await replay.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(1) != invoice.SnapshotSha256 || reader.GetDateTime(2) != invoice.IssueDateUtc)
                    throw new InvalidOperationException("Shared fiscal reservation differs from immutable invoice.");
                var reserved = reader.GetInt64(0);
                await reader.DisposeAsync();
                await transaction.CommitAsync(ct);
                return reserved;
            }
        }
        await using var maximum = new NpgsqlCommand("""
SELECT GREATEST(
 COALESCE((SELECT max(serial) FROM production_shared_serial_reservations WHERE memory_code=@memory),0),
 COALESCE((SELECT last_serial FROM production_shared_serial_floors WHERE memory_code=@memory),0),
 COALESCE((SELECT max(r.serial) FROM authority_invoice_fiscal_reservations r
 JOIN fiscal_memories m ON m.id=r.fiscal_memory_id
 WHERE m.memory_identifier=@memory AND r.authority_environment=1),0))
""", connection, transaction);
        maximum.Parameters.AddWithValue("memory", memoryCode);
        var serial = checked(Math.Max(lowerBound, Convert.ToInt64(await maximum.ExecuteScalarAsync(ct))) + 1);
        if (serial > 0xffffffffffL) throw new ArgumentException("Production fiscal serial exhausted.");
        await using var insert = new NpgsqlCommand("""
INSERT INTO production_shared_serial_reservations
(memory_code,serial,invoice_id,customer_user_id,snapshot_sha256,issue_date_utc)
VALUES (@memory,@serial,@invoice,@owner,@hash,@issue)
""", connection, transaction);
        insert.Parameters.AddWithValue("memory", memoryCode);
        insert.Parameters.AddWithValue("serial", serial);
        insert.Parameters.AddWithValue("invoice", invoice.Id);
        insert.Parameters.AddWithValue("owner", invoice.CustomerUserId);
        insert.Parameters.AddWithValue("hash", invoice.SnapshotSha256);
        insert.Parameters.AddWithValue("issue", invoice.IssueDateUtc);
        await insert.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return serial;
    }
}
