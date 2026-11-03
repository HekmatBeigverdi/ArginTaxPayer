-- Phase 15 Step 04 — Read-only diagnostic audit, run on argintaxpayer_test only.
-- No DML, no DDL, no calls to tax authority. Counts only; no personal IDs or raw payloads.
\set ON_ERROR_STOP on
BEGIN TRANSACTION READ ONLY;
SELECT current_database() AS database_checked;
DO $ BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
  RAISE EXCEPTION 'Phase15 audit is restricted to argintaxpayer_test';
 END IF;
END $;

SELECT 'invoice_owner_mismatch' AS check_name,COUNT(*) AS observed_rows
FROM invoice_documents d JOIN taxpayer_profiles t ON t.id=d.taxpayer_id
WHERE t.customer_user_id IS DISTINCT FROM d.customer_user_id;

SELECT 'lineage_out_of_scope_edges' AS check_name,COUNT(*) AS observed_rows
FROM sandbox_invoice_legal_relations r
JOIN invoice_documents p ON p.id=r.predecessor_invoice_id
JOIN invoice_documents s ON s.id=r.successor_invoice_id
WHERE p.customer_user_id<>r.customer_user_id OR s.customer_user_id<>r.customer_user_id
   OR p.taxpayer_id<>r.taxpayer_id OR s.taxpayer_id<>r.taxpayer_id;

SELECT 'multiple_predecessors_per_successor' AS check_name,COUNT(*) AS observed_rows
FROM (
 SELECT r.customer_user_id,r.taxpayer_id,r.successor_invoice_id
 FROM sandbox_invoice_legal_relations r
 GROUP BY r.customer_user_id,r.taxpayer_id,r.successor_invoice_id HAVING COUNT(*)>1
) repeated;

SELECT 'commercial_orders_collected_over_gross' AS check_name,COUNT(*) AS observed_rows
FROM commercial_orders WHERE collected_amount_irr>gross_amount_irr;

SELECT 'commercial_receipt_total_disagreement' AS check_name,COUNT(*) AS observed_rows
FROM commercial_orders o LEFT JOIN (
 SELECT order_id,COALESCE(SUM(amount_irr),0) AS total_irr
 FROM commercial_collections GROUP BY order_id
) receipts ON receipts.order_id=o.id
WHERE o.collected_amount_irr<>COALESCE(receipts.total_irr,0);

SELECT 'commercial_allocation_amount_disagreement' AS check_name,COUNT(*) AS observed_rows
FROM commercial_revenue_allocations a
WHERE a.gross_collected_irr <> a.approved_cost_irr+a.partner_share_irr+a.root_share_irr;

SELECT 'partner_settlement_overpaid' AS check_name,COUNT(*) AS observed_rows
FROM (
 SELECT a.id,a.partner_share_irr,
 COALESCE(SUM(CASE WHEN e.kind=1 THEN e.amount_irr WHEN e.kind=2 THEN -e.amount_irr ELSE 0 END),0) AS net_settled
 FROM commercial_revenue_allocations a LEFT JOIN partner_settlement_events e ON e.allocation_id=a.id
 GROUP BY a.id,a.partner_share_irr
) positions WHERE net_settled<0 OR net_settled>partner_share_irr;

SELECT 'historical_partner_orders_no_longer_current_owner' AS diagnostic,
       COUNT(*) AS observed_rows
FROM commercial_orders o
LEFT JOIN customer_commercial_owners own ON own.customer_user_id=o.customer_user_id
WHERE o.partner_user_id IS NOT NULL
  AND own.partner_level2_user_id IS DISTINCT FROM o.partner_user_id;

-- Acknowledgement is not evidence of official acceptance.
SELECT 'acknowledged_without_official_success' AS diagnostic,COUNT(*) AS observed_rows
FROM invoice_dispatch_operations op
WHERE op.state=5 AND NOT EXISTS (
 SELECT 1 FROM sandbox_authority_inquiry_reports r
 WHERE r.invoice_id=op.invoice_id AND r.customer_user_id=op.customer_user_id
 AND r.taxpayer_id=op.taxpayer_id AND r.report_json->>'Status'='SUCCESS'
);
COMMIT;
