using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Company-facing decimal invoice numbering, independent of fiscal tax IDs.</summary>
internal static class InvoiceBusinessSerialSequence
{
    internal const long Maximum = 9_999_999_999;

    internal static bool IsRejectedRetry(string? key) =>
        key?.StartsWith("production-resubmission:", StringComparison.Ordinal) == true ||
        key?.StartsWith("sandbox-correction:", StringComparison.Ordinal) == true;

    internal static async Task<long?> ReserveRetryAsync(DbConnection connection,
        DbTransaction transaction, Guid taxpayerId, Guid workspaceId, Guid predecessorId,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
WITH predecessor_serial AS (
 SELECT persian_year,serial,0 AS priority FROM invoice_business_serial_retries
 WHERE workspace_id=@predecessor AND taxpayer_id=@taxpayer
 UNION ALL
 SELECT persian_year,serial,1 AS priority FROM invoice_business_serials
 WHERE workspace_id=@predecessor AND taxpayer_id=@taxpayer
)
INSERT INTO invoice_business_serial_retries AS retry(workspace_id,taxpayer_id,persian_year,serial)
SELECT @workspace,@taxpayer,persian_year,serial FROM predecessor_serial
ORDER BY priority LIMIT 1
ON CONFLICT(workspace_id) DO UPDATE SET serial=retry.serial
WHERE retry.taxpayer_id=EXCLUDED.taxpayer_id AND retry.persian_year=EXCLUDED.persian_year
  AND retry.serial=EXCLUDED.serial
RETURNING serial
""";
        Add(command,"taxpayer",taxpayerId);
        Add(command,"workspace",workspaceId);
        Add(command,"predecessor",predecessorId);
        return await command.ExecuteScalarAsync(ct) is long serial ? serial : null;
    }

    public static async Task<long?> ReserveAsync(DbConnection connection,
        DbTransaction transaction, Guid taxpayerId, Guid workspaceId,
        DateTime issueDateUtc, long? requested, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
WITH linked AS (
 SELECT serial FROM invoice_business_serial_retries
 WHERE workspace_id=@workspace AND taxpayer_id=@taxpayer
), issue_year AS (
 SELECT phase08_persian_year_for_issue(@issue_date) AS year
), locked AS (
 SELECT pg_advisory_xact_lock(hashtextextended(@taxpayer::text || ':' || year::text, 0))
 FROM issue_year
), wanted AS (
 SELECT year, COALESCE(@requested,
   (SELECT serial FROM invoice_business_serials s WHERE s.workspace_id=@workspace
      AND s.taxpayer_id=@taxpayer AND s.persian_year=year),
   (SELECT COALESCE(MAX(serial),0)+1 FROM invoice_business_serials s
    WHERE s.taxpayer_id=@taxpayer AND s.persian_year=year AND s.workspace_id<>@workspace)) AS serial
 FROM issue_year, locked
 WHERE NOT EXISTS(SELECT 1 FROM linked)
), reserved AS (
INSERT INTO invoice_business_serials(taxpayer_id,persian_year,serial,workspace_id,assigned_at_utc)
SELECT @taxpayer,year,serial,@workspace,now() FROM wanted
ON CONFLICT(workspace_id) DO UPDATE SET
 taxpayer_id=EXCLUDED.taxpayer_id,persian_year=EXCLUDED.persian_year,
 serial=EXCLUDED.serial,assigned_at_utc=now()
WHERE invoice_business_serials.taxpayer_id=EXCLUDED.taxpayer_id
 AND invoice_business_serials.persian_year=EXCLUDED.persian_year
RETURNING serial
)
SELECT serial FROM linked UNION ALL SELECT serial FROM reserved
""";
        Add(command,"taxpayer",taxpayerId);
        Add(command,"workspace",workspaceId);
        Add(command,"issue_date",issueDateUtc);
        Add(command,"requested",requested is null ? DBNull.Value : requested.Value);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToInt64(value);
    }

    public static async Task<long?> NextAsync(DbConnection connection, Guid taxpayerId,
        DateTime issueDateUtc, CancellationToken ct)
    {
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.CommandText="""
SELECT COALESCE(MAX(s.serial),0)+1
FROM (SELECT phase08_persian_year_for_issue(@issue_date) AS year) y
LEFT JOIN invoice_business_serials s ON s.taxpayer_id=@taxpayer AND s.persian_year=y.year
""";
            Add(command,"taxpayer",taxpayerId);
            Add(command,"issue_date",issueDateUtc);
            var value=await command.ExecuteScalarAsync(ct);
            var next=value is null or DBNull ? 1L : Convert.ToInt64(value);
            return next<=Maximum ? next : null;
        }
        finally {if(opened)await connection.CloseAsync();}
    }

    private static void Add(DbCommand command,string name,object value)
    {
        var parameter=command.CreateParameter();
        parameter.ParameterName=name;
        parameter.Value=value;
        command.Parameters.Add(parameter);
    }
}
