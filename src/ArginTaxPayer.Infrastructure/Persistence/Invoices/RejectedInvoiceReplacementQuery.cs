using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal static class RejectedInvoiceReplacementQuery
{
    internal static async Task<Dictionary<Guid,string>> ReadEffectiveSystemCodesAsync(
        ArginTaxPayerDbContext db,Guid[] invoiceIds,Guid[] owners,Guid[] taxpayers,
        CancellationToken ct)
    {
        var result=new Dictionary<Guid,string>();
        if(invoiceIds.Length==0)return result;
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.CommandText="""
WITH RECURSIVE ancestors AS (
 SELECT d.id AS requested_id,d.id AS invoice_id,d.customer_user_id,d.taxpayer_id,
  0 AS depth,ARRAY[d.id] AS visited
 FROM invoice_documents d
 WHERE d.id=ANY(@invoice_ids) AND d.customer_user_id=ANY(@owners)
  AND d.taxpayer_id=ANY(@taxpayers)
 UNION ALL
 SELECT a.requested_id,r.predecessor_invoice_id,a.customer_user_id,a.taxpayer_id,
  a.depth+1,a.visited||r.predecessor_invoice_id
 FROM ancestors a JOIN invoice_successful_rejection_replacements r
  ON r.successor_invoice_id=a.invoice_id AND r.customer_user_id=a.customer_user_id
  AND r.taxpayer_id=a.taxpayer_id
 WHERE a.depth<100 AND NOT r.predecessor_invoice_id=ANY(a.visited)
)
SELECT DISTINCT ON (a.requested_id) a.requested_id,sr.system_code
FROM ancestors a JOIN invoice_support_references sr
 ON sr.source_id=a.invoice_id AND sr.source_kind='document'
ORDER BY a.requested_id,a.depth DESC
""";
            foreach(var (name,value) in new (string,object)[]{
                ("invoice_ids",invoiceIds),("owners",owners),("taxpayers",taxpayers)})
            {var p=command.CreateParameter();p.ParameterName=name;p.Value=value;command.Parameters.Add(p);}
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))result[reader.GetGuid(0)]=reader.GetString(1);
            return result;
        }
        finally{if(opened)await connection.CloseAsync();}
    }

    internal static async Task<Guid[]> FindBySystemCodeAsync(ArginTaxPayerDbContext db,
        string systemCode,Guid[] allowedTaxpayers,CancellationToken ct)
    {
        if(allowedTaxpayers.Length==0)return [];
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.CommandText="""
WITH RECURSIVE replacements AS (
 SELECT r.customer_user_id,r.taxpayer_id,r.predecessor_invoice_id,
  r.successor_invoice_id,1 AS depth,ARRAY[r.predecessor_invoice_id,r.successor_invoice_id] AS visited
 FROM invoice_successful_rejection_replacements r
 JOIN invoice_support_references ref ON ref.source_kind='document'
  AND ref.source_id=r.predecessor_invoice_id AND ref.system_code=@system_code
 UNION ALL
 SELECT c.customer_user_id,c.taxpayer_id,c.predecessor_invoice_id,r.successor_invoice_id,
  c.depth+1,c.visited||r.successor_invoice_id
 FROM replacements c JOIN invoice_successful_rejection_replacements r
  ON r.predecessor_invoice_id=c.successor_invoice_id
  AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 WHERE c.depth<100 AND NOT r.successor_invoice_id=ANY(c.visited)
)
SELECT DISTINCT r.successor_invoice_id AS "Value" FROM replacements r
JOIN invoice_documents d ON d.id=r.successor_invoice_id
 AND d.customer_user_id=r.customer_user_id AND d.taxpayer_id=r.taxpayer_id
WHERE r.taxpayer_id=ANY(@taxpayers)
""";
            foreach(var (name,value) in new (string,object)[]{
                ("system_code",systemCode),("taxpayers",allowedTaxpayers)})
            {var p=command.CreateParameter();p.ParameterName=name;p.Value=value;command.Parameters.Add(p);}
            var ids=new List<Guid>();
            await using var reader=await command.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))ids.Add(reader.GetGuid(0));
            return ids.ToArray();
        }
        finally{if(opened)await connection.CloseAsync();}
    }

    internal static async Task<Guid> ResolveAsync(ArginTaxPayerDbContext db,Guid owner,
        Guid taxpayer,Guid invoiceId,CancellationToken ct)
    {
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened)await connection.OpenAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.CommandText="""
WITH RECURSIVE replacement AS (
 SELECT @invoice::uuid AS invoice_id,0 AS depth,ARRAY[@invoice::uuid] AS visited
 UNION ALL
 SELECT r.successor_invoice_id,c.depth+1,c.visited||r.successor_invoice_id
 FROM replacement c JOIN invoice_successful_rejection_replacements r
  ON r.predecessor_invoice_id=c.invoice_id
  AND r.customer_user_id=@owner AND r.taxpayer_id=@taxpayer
 WHERE c.depth<100 AND NOT r.successor_invoice_id=ANY(c.visited)
)
SELECT invoice_id FROM replacement ORDER BY depth DESC LIMIT 1
""";
            foreach(var (name,value) in new[]{("invoice",invoiceId),("owner",owner),("taxpayer",taxpayer)})
            {var p=command.CreateParameter();p.ParameterName=name;p.Value=value;command.Parameters.Add(p);}
            return (Guid)(await command.ExecuteScalarAsync(ct))!;
        }
        finally{if(opened)await connection.CloseAsync();}
    }

    internal static async Task<bool> RegisterAsync(ArginTaxPayerDbContext db,Guid owner,
        Guid taxpayer,Guid predecessor,Guid operation,Guid workspace,CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO invoice_rejection_replacements
 (id,customer_user_id,taxpayer_id,predecessor_invoice_id,predecessor_operation_id,successor_invoice_id,created_at_utc)
SELECT {Guid.NewGuid()},{owner},{taxpayer},{predecessor},{operation},{workspace},{DateTime.UtcNow}
WHERE EXISTS(SELECT 1 FROM invoice_dispatch_operations op
 JOIN invoice_documents d ON d.id=op.invoice_id AND d.customer_user_id=op.customer_user_id
  AND d.taxpayer_id=op.taxpayer_id AND d.subject=1
 JOIN production_authority_inquiry_reports report ON report.operation_id=op.id
  AND report.invoice_id=op.invoice_id AND report.customer_user_id=op.customer_user_id
  AND report.taxpayer_id=op.taxpayer_id
 WHERE op.id={operation} AND op.invoice_id={predecessor} AND op.customer_user_id={owner}
  AND op.taxpayer_id={taxpayer} AND report.report_json->>'Status'='FAILED'
  AND report.report_json->>'CanPrepareCorrection'='true')
 AND EXISTS(SELECT 1 FROM invoice_entry_workspaces
  WHERE id={workspace} AND customer_user_id={owner} AND taxpayer_id={taxpayer})
ON CONFLICT(customer_user_id,taxpayer_id,successor_invoice_id) DO NOTHING
""",ct);
        return await db.Database.SqlQuery<int>($"""
SELECT 1 AS "Value" FROM invoice_rejection_replacements
WHERE customer_user_id={owner} AND taxpayer_id={taxpayer} AND predecessor_invoice_id={predecessor}
 AND predecessor_operation_id={operation} AND successor_invoice_id={workspace}
""").AnyAsync(ct);
    }
}
