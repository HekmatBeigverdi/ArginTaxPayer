using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal static class InvoiceDeletionEligibility
{
    internal static Task<Guid[]> ReadAsync(ArginTaxPayerDbContext db, Guid[] ids,
        Guid[] owners, Guid[] taxpayers, CancellationToken ct) =>
        ids.Length == 0 ? Task.FromResult(Array.Empty<Guid>()) :
        db.Database.SqlQuery<Guid>($"""
WITH candidates AS (
 SELECT id, customer_user_id, taxpayer_id FROM invoice_documents
 WHERE id=ANY({ids}) AND customer_user_id=ANY({owners}) AND taxpayer_id=ANY({taxpayers})
  AND deleted_at_utc IS NULL AND subject=1
 UNION
 SELECT w.id, w.customer_user_id, w.taxpayer_id FROM invoice_entry_workspaces w
 WHERE w.id=ANY({ids}) AND w.customer_user_id=ANY({owners}) AND w.taxpayer_id=ANY({taxpayers})
  AND w.deleted_at_utc IS NULL
  AND NOT EXISTS (SELECT 1 FROM invoice_documents d WHERE d.id=w.id)
)
SELECT c.id AS "Value"
FROM candidates c
LEFT JOIN invoice_documents d ON d.id=c.id AND d.customer_user_id=c.customer_user_id AND d.taxpayer_id=c.taxpayer_id
LEFT JOIN invoice_entry_workspaces w ON w.id=c.id AND w.customer_user_id=c.customer_user_id AND w.taxpayer_id=c.taxpayer_id
CROSS JOIN LATERAL (
 SELECT count(*) AS attempts,
  bool_and(op.state IN (5,8) AND latest.status='FAILED') AS rejected
 FROM invoice_dispatch_operations op
 LEFT JOIN LATERAL (
   SELECT report_json->>'Status' AS status FROM (
    SELECT report_json,observed_at_utc,id FROM production_authority_inquiry_reports
    WHERE operation_id=op.id AND customer_user_id=c.customer_user_id AND taxpayer_id=c.taxpayer_id AND invoice_id=c.id
    UNION ALL
    SELECT report_json,observed_at_utc,id FROM sandbox_authority_inquiry_reports
    WHERE operation_id=op.id AND customer_user_id=c.customer_user_id AND taxpayer_id=c.taxpayer_id AND invoice_id=c.id
   ) reports ORDER BY observed_at_utc DESC,id DESC LIMIT 1
 ) latest ON true
 WHERE op.invoice_id=c.id AND op.customer_user_id=c.customer_user_id AND op.taxpayer_id=c.taxpayer_id
) dispatch
WHERE EXISTS (
 SELECT 1 FROM customer_taxpayer_memberships m
 WHERE m.customer_user_id=c.customer_user_id AND m.taxpayer_id=c.taxpayer_id AND m.unlinked_at_utc IS NULL
)
AND (
 (dispatch.attempts=0 AND (d.id IS NULL OR d.state=1)
  AND COALESCE(w.draft_json->>'InternalReviewState','Draft')='Draft'
  AND NOT EXISTS (SELECT 1 FROM invoice_reviews r WHERE r.invoice_id=c.id
   AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id AND r.state<>1
   AND COALESCE(w.draft_json->>'BulkImportKey','')<> 'unsent-return:' || replace(c.id::text,'-','')))
 OR (d.state=3 AND dispatch.attempts=1 AND dispatch.rejected
  AND NOT EXISTS (
   SELECT 1 FROM production_authority_inquiry_reports r
   WHERE r.invoice_id=c.id AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
    AND r.report_json->>'Status'='SUCCESS'
   UNION ALL
   SELECT 1 FROM sandbox_authority_inquiry_reports r
   WHERE r.invoice_id=c.id AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
    AND r.report_json->>'Status'='SUCCESS'))
)
AND NOT EXISTS (
 SELECT 1 FROM sandbox_invoice_legal_relations r
 WHERE c.id IN (r.predecessor_invoice_id,r.successor_invoice_id)
  AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM authority_invoice_legal_relations r
 WHERE c.id IN (r.predecessor_invoice_id,r.successor_invoice_id)
  AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM invoice_rejection_replacements r
 WHERE c.id IN (r.predecessor_invoice_id,r.successor_invoice_id)
  AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM invoice_dispatch_operations op
 JOIN invoice_entry_workspaces retry ON retry.customer_user_id=op.customer_user_id
  AND retry.taxpayer_id=op.taxpayer_id
  AND split_part(retry.draft_json->>'BulkImportKey',':',1) IN ('sandbox-correction','production-resubmission')
  AND split_part(retry.draft_json->>'BulkImportKey',':',2)=replace(op.id::text,'-','')
 WHERE op.invoice_id=c.id AND op.customer_user_id=c.customer_user_id AND op.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM sandbox_invoice_correction_drafts r
 WHERE c.id IN (r.invoice_id,r.workspace_id)
  AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM sandbox_invoice_correction_workspaces r
 WHERE c.id=r.predecessor_invoice_id AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM authority_invoice_correction_workspaces r
 WHERE c.id=r.predecessor_invoice_id AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM sandbox_invoice_sales_return_workspaces r
 WHERE c.id=r.predecessor_invoice_id AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
 UNION ALL
 SELECT 1 FROM authority_invoice_sales_return_workspaces r
 WHERE c.id=r.predecessor_invoice_id AND r.customer_user_id=c.customer_user_id AND r.taxpayer_id=c.taxpayer_id
)
AND COALESCE(split_part(w.draft_json->>'BulkImportKey',':',1),'')
 NOT IN ('sandbox-correction','production-resubmission')
""").ToArrayAsync(ct);
}
