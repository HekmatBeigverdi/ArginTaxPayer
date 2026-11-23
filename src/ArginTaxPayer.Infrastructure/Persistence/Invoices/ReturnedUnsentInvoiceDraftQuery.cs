using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal static class ReturnedUnsentInvoiceDraftQuery
{
    internal static IQueryable<Guid> Read(ArginTaxPayerDbContext db) => db.Database.SqlQueryRaw<Guid>("""
SELECT w.id AS "Value"
FROM invoice_entry_workspaces w
JOIN invoice_documents d ON d.id=w.id AND d.customer_user_id=w.customer_user_id
 AND d.taxpayer_id=w.taxpayer_id
WHERE d.state=1 AND d.deleted_at_utc IS NULL AND w.deleted_at_utc IS NULL
 AND w.draft_json->>'BulkImportKey'='unsent-return:' || replace(w.id::text,'-','')
 AND (
  d.snapshot_json->>'sourceWorkspaceId' IS DISTINCT FROM w.id::text
  OR d.snapshot_json->>'sourceRevision' IS DISTINCT FROM w.revision::text
 )
""");
}
