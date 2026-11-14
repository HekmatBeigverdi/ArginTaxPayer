namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>
/// Maps each rejected Sandbox operation to its latest generation that was
/// actually dispatched. Draft-only generations must not become lineage nodes.
/// </summary>
internal static class SandboxRetryLineageQuery
{
    internal const string Ctes = """
sandbox_retry_generations AS (
 SELECT c.invoice_id AS predecessor_invoice_id,h.workspace_id AS successor_invoice_id,
  c.customer_user_id,c.taxpayer_id,c.created_at_utc,h.occurred_at_utc,
  c.operation_id,
  ROW_NUMBER() OVER(PARTITION BY c.operation_id
   ORDER BY h.occurred_at_utc DESC,h.workspace_id DESC) AS generation_rank
 FROM sandbox_invoice_correction_drafts c
 JOIN invoice_entry_workspace_revisions h ON h.customer_user_id=c.customer_user_id
  AND h.taxpayer_id=c.taxpayer_id AND h.action='create'
  AND split_part(h.snapshot_json->>'BulkImportKey',':',1)='sandbox-correction'
  AND split_part(h.snapshot_json->>'BulkImportKey',':',2)=replace(c.operation_id::text,'-','')
 WHERE h.workspace_id<>c.invoice_id
  AND (EXISTS(SELECT 1 FROM invoice_dispatch_operations op
    WHERE op.invoice_id=h.workspace_id AND op.customer_user_id=c.customer_user_id
     AND op.taxpayer_id=c.taxpayer_id AND op.state IN (4,5,6,8))
   OR EXISTS(SELECT 1 FROM production_authority_inquiry_reports ir
    WHERE ir.invoice_id=h.workspace_id AND ir.customer_user_id=c.customer_user_id
     AND ir.taxpayer_id=c.taxpayer_id)
   OR EXISTS(SELECT 1 FROM sandbox_authority_inquiry_reports ir
    WHERE ir.invoice_id=h.workspace_id AND ir.customer_user_id=c.customer_user_id
     AND ir.taxpayer_id=c.taxpayer_id))
), sandbox_latest_generation AS (
 SELECT predecessor_invoice_id,successor_invoice_id,customer_user_id,taxpayer_id,
  GREATEST(created_at_utc,occurred_at_utc) AS created_at_utc
 FROM sandbox_retry_generations WHERE generation_rank=1
), sandbox_retries AS (
 SELECT DISTINCT ON(customer_user_id,taxpayer_id,predecessor_invoice_id)
  predecessor_invoice_id,successor_invoice_id,customer_user_id,taxpayer_id,
  created_at_utc,1 AS state
 FROM sandbox_latest_generation
 ORDER BY customer_user_id,taxpayer_id,predecessor_invoice_id,
  created_at_utc DESC,successor_invoice_id DESC
)
""";
}
