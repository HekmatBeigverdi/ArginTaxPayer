using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Signing;

public sealed record ConnectionBinding(Guid OwnerId, Guid TaxpayerId, Guid MemoryId,
    string Source, string MemoryIdentifier, int? Environment, long MemoryRevision,
    Guid? KeyId, long? KeyRevision, string? Fingerprint);
public sealed record ConnectionObservation(ConnectionBinding Binding, string Status,
    DateTime StartedAtUtc, DateTime TestedAtUtc, Guid AttemptId);
public sealed record ConnectionAttemptView(string Status, DateTime TestedAtUtc);
public sealed record ConnectionHistoryView(bool Available, ConnectionAttemptView? LastAttempt = null,
    ConnectionAttemptView? LastSuccess = null);

public static class ConnectionHistoryPolicy
{
    public static bool CanRecord(ConnectionBinding captured, ConnectionBinding? current) => captured == current;

    public static ConnectionHistoryView Project(ConnectionBinding current, IEnumerable<ConnectionObservation> observations)
    {
        if(current.Environment is not (1 or 2))return new(true);
        var matching = observations.Where(o => o.Binding with { MemoryRevision = current.MemoryRevision } == current)
            .OrderByDescending(o => o.StartedAtUtc).ThenByDescending(o => o.AttemptId).ToArray();
        static ConnectionAttemptView? View(ConnectionObservation? o) => o is null ? null : new(o.Status, o.TestedAtUtc);
        return new(true, View(matching.FirstOrDefault()), View(matching.FirstOrDefault(o => o.Status == "Authenticated")));
    }

    // A successful, separately authorized local activation may advance only the
    // memory revision/classification. It cannot bless another key or identifier.
    public static bool CanFollowActivation(ConnectionBinding before, ConnectionBinding after, int? requestedEnvironment)
    {
        var classify=before.Environment is null && requestedEnvironment is 1 or 2 && after.Environment==requestedEnvironment;
        return after.Source=="RegisteredMemory" && after.Environment is 1 or 2 &&
            after.MemoryRevision==before.MemoryRevision+(classify?2:1) &&
            (before.Environment==after.Environment||classify) &&
            before with {MemoryRevision=after.MemoryRevision,Environment=after.Environment}==after;
    }
}

/// <summary>Append-only safe probe evidence. Callers must authorize the memory before capture/read.</summary>
public sealed class FiscalMemoryConnectionHistory(ArginTaxPayerDbContext db)
{
    public Task<ConnectionBinding?> CaptureRegisteredAsync(Guid taxpayerId, Guid memoryId, CancellationToken ct = default) =>
        CaptureAsync("RegisteredMemory", taxpayerId, memoryId, ct);

    public Task<ConnectionBinding?> CaptureSandboxAsync(Guid taxpayerId, Guid memoryId, CancellationToken ct = default) =>
        CaptureAsync("LegacySandbox", taxpayerId, memoryId, ct);

    private async Task<ConnectionBinding?> CaptureAsync(string source, Guid taxpayerId, Guid memoryId, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = source == "LegacySandbox" ? """
                SELECT customer_user_id,taxpayer_id,id,memory_identifier,2,revision,
                       signing_key_version_id,revision,public_fingerprint_sha256
                FROM sandbox_fiscal_memories WHERE taxpayer_id=@taxpayer AND id=@memory
                """ : """
                SELECT p.customer_user_id,m.taxpayer_id,m.id,m.memory_identifier,m.authority_environment,m.revision,
                       k.id,k.revision,k.public_fingerprint_sha256
                FROM fiscal_memories m JOIN taxpayer_profiles p ON p.id=m.taxpayer_id
                LEFT JOIN LATERAL (
                    SELECT v.id,v.revision,v.public_fingerprint_sha256 FROM signing_key_versions v
                    WHERE v.customer_user_id=p.customer_user_id AND v.taxpayer_id=m.taxpayer_id
                      AND v.fiscal_memory_id=m.id AND v.lifecycle_state=2
                      AND (NOT EXISTS(SELECT 1 FROM signing_key_selections s WHERE s.fiscal_memory_id=m.id
                             AND s.customer_user_id=p.customer_user_id AND s.taxpayer_id=m.taxpayer_id AND s.key_version_id IS NOT NULL)
                           OR v.id=(SELECT s.key_version_id FROM signing_key_selections s WHERE s.fiscal_memory_id=m.id
                             AND s.customer_user_id=p.customer_user_id AND s.taxpayer_id=m.taxpayer_id))
                    ORDER BY v.version_number DESC LIMIT 1
                ) k ON true
                WHERE m.taxpayer_id=@taxpayer AND m.id=@memory
                """;
            Add(command, "taxpayer", taxpayerId); Add(command, "memory", memoryId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), source, reader.GetString(3),
                reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)), reader.GetInt64(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetInt64(7),
                reader.IsDBNull(8) ? null : reader.GetString(8));
        }
        finally { if (opened) await connection.CloseAsync(); }
    }

    public async Task<bool> RecordAsync(ConnectionBinding? captured, string status, DateTime startedAtUtc,
        DateTime testedAtUtc, CancellationToken ct = default)
    {
        if (captured is null || captured.Environment is not (1 or 2) ||
            !Enum.TryParse<ArginTaxPayer.Application.Signing.SigningConnectionTestStatus>(status, out var parsed) || !Enum.IsDefined(parsed) ||
            status=="Authenticated" && (captured.KeyId is null || string.IsNullOrWhiteSpace(captured.Fingerprint))) return false;
        var current = await CaptureAsync(captured.Source, captured.TaxpayerId, captured.MemoryId, ct);
        if (!ConnectionHistoryPolicy.CanRecord(captured, current)) return false;
        var json = JsonSerializer.Serialize(captured);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO fiscal_memory_connection_observations
                  (attempt_id,customer_user_id,taxpayer_id,memory_id,source,binding,status,started_at_utc,tested_at_utc)
                VALUES ({Guid.NewGuid()},{captured.OwnerId},{captured.TaxpayerId},{captured.MemoryId},
                  {captured.Source},CAST({json} AS jsonb),{status},{startedAtUtc},{testedAtUtc})
                """, ct);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable) { return false; }
    }

    public async Task<ConnectionHistoryView> ReadAsync(ConnectionBinding? current, CancellationToken ct = default)
    {
        if (current is null) return new(true);
        // Memory lifecycle changes alone do not invalidate the same key's probe.
        var identity = JsonSerializer.Serialize(new { current.OwnerId, current.TaxpayerId, current.MemoryId,
            current.Source, current.MemoryIdentifier, current.Environment, current.KeyId, current.KeyRevision, current.Fingerprint });
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
            const string select = """
                SELECT binding,status,started_at_utc,tested_at_utc,attempt_id
                FROM fiscal_memory_connection_observations
                WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer AND memory_id=@memory
                  AND source=@source AND binding @> CAST(@identity AS jsonb)
                """;
            command.CommandText = $"({select} ORDER BY started_at_utc DESC,attempt_id DESC LIMIT 1) UNION ALL " +
                $"({select} AND status='Authenticated' ORDER BY started_at_utc DESC,attempt_id DESC LIMIT 1)";
            Add(command, "owner", current.OwnerId); Add(command, "taxpayer", current.TaxpayerId);
            Add(command, "memory", current.MemoryId); Add(command, "source", current.Source); Add(command, "identity", identity);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<ConnectionObservation>();
            while (await reader.ReadAsync(ct))
            {
                var binding = JsonSerializer.Deserialize<ConnectionBinding>(reader.GetString(0));
                if (binding is not null) rows.Add(new(binding, reader.GetString(1), reader.GetDateTime(2), reader.GetDateTime(3), reader.GetGuid(4)));
            }
            return ConnectionHistoryPolicy.Project(current, rows);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable) { return new(false); }
        finally { if (opened) await connection.CloseAsync(); }
    }

    private static void Add(System.Data.Common.DbCommand command, string name, object value)
    { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
}
