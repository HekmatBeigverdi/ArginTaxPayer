using ArginTaxPayer.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal sealed record EffectiveInvoice(Guid InvoiceId, string Subject, DateTime Date, string Status);

internal static class Phase10EffectiveInvoiceQuery
{
    internal static async Task<Dictionary<Guid, EffectiveInvoice>> ReadAsync(
        ArginTaxPayerDbContext db, Guid[] ids, Guid[] owners, Guid[] taxpayers, CancellationToken ct)
    {
        var result = new Dictionary<Guid, EffectiveInvoice>();
        if (ids.Length == 0) return result;
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE chain AS (
                  SELECT r.predecessor_invoice_id AS root_id,r.successor_invoice_id AS invoice_id,
                    r.customer_user_id,r.taxpayer_id,r.created_at_utc,1 AS depth,
                    ARRAY[r.predecessor_invoice_id,r.successor_invoice_id] AS visited
                  FROM sandbox_invoice_legal_relations r
                  WHERE r.predecessor_invoice_id=ANY(@ids)
                    AND r.customer_user_id=ANY(@owners) AND r.taxpayer_id=ANY(@taxpayers)
                  UNION ALL
                  SELECT c.root_id,r.successor_invoice_id,r.customer_user_id,r.taxpayer_id,
                    r.created_at_utc,c.depth+1,c.visited||r.successor_invoice_id
                  FROM chain c JOIN sandbox_invoice_legal_relations r
                    ON r.predecessor_invoice_id=c.invoice_id
                    AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
                  WHERE NOT r.successor_invoice_id=ANY(c.visited) AND c.depth<100
                )
                SELECT c.root_id,d.id,d.subject,d.issue_date_utc,
                  COALESCE(NULLIF(report.status,'UNKNOWN'),CASE op.state
                    WHEN 5 THEN 'ACKNOWLEDGED' WHEN 4 THEN 'SENDING'
                    WHEN 6 THEN 'INDETERMINATE' ELSE 'NOT_SENT' END)
                FROM chain c JOIN invoice_documents d ON d.id=c.invoice_id
                  AND d.customer_user_id=c.customer_user_id AND d.taxpayer_id=c.taxpayer_id
                LEFT JOIN LATERAL (
                  SELECT ar.report_json->>'Status' AS status FROM sandbox_authority_inquiry_reports ar
                  WHERE ar.invoice_id=d.id AND ar.customer_user_id=d.customer_user_id AND ar.taxpayer_id=d.taxpayer_id
                  ORDER BY ar.observed_at_utc DESC,ar.id DESC LIMIT 1
                ) report ON true
                LEFT JOIN LATERAL (
                  SELECT state FROM invoice_dispatch_operations op
                  WHERE op.invoice_id=d.id AND op.customer_user_id=d.customer_user_id AND op.taxpayer_id=d.taxpayer_id
                  ORDER BY op.created_at_utc DESC,op.id DESC LIMIT 1
                ) op ON true
                WHERE report.status='SUCCESS'
                  OR (d.subject=2 AND (op.state IN (4,5,6) OR report.status IS NOT NULL))
                ORDER BY c.depth DESC,c.created_at_utc DESC,d.id DESC
                """;
            foreach (var (name, value) in new (string, object)[] { ("ids", ids), ("owners", owners), ("taxpayers", taxpayers) })
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name; parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.TryAdd(reader.GetGuid(0), new(reader.GetGuid(1),
                    ((InvoiceSubject)reader.GetInt32(2)).ToString(),reader.GetDateTime(3),reader.GetString(4)));
            return result;
        }
        finally { if (opened) await connection.CloseAsync(); }
    }
}
