-- Phase 15 / Step 15: read-only PostgreSQL dashboard query plan audit.
-- Run against argintaxpayer_test only, using an account permitted to read the test tables.
-- No DDL, DML, REFRESH, VACUUM, or foreign service calls. No customer identifiers printed by queries.
\set ON_ERROR_STOP on
\pset pager off
BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '15s';
SET LOCAL lock_timeout = '2s';
SET LOCAL idle_in_transaction_session_timeout = '90s';

SELECT 1 / (CASE WHEN current_database() = 'argintaxpayer_test' THEN 1 ELSE 0 END)
    AS test_database_only_gate;
SELECT 'phase15-step15' AS audit, current_database() AS database_name, version() AS postgres_version;

-- Sensitive business identifiers are not emitted by these report queries.
-- EXPLAIN can expose values in predicate strings. Treat captured output as restricted QA evidence.
-- First inspect table sizes and index coverage (no query execution of protected data).
SELECT relname, n_live_tup::bigint AS estimated_live_rows, n_dead_tup::bigint AS estimated_dead_rows
FROM pg_stat_user_tables
WHERE relname IN (
  'invoice_documents','invoice_dispatch_operations','sandbox_invoice_legal_relations',
  'sandbox_authority_inquiry_reports','commercial_orders','commercial_collections',
  'commercial_revenue_allocations','partner_settlement_events',
  'customer_commercial_owners','customer_subscriptions','subscription_credit_accounts'
) ORDER BY relname;

SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = current_schema()
  AND tablename IN (
    'invoice_documents','invoice_dispatch_operations','sandbox_invoice_legal_relations',
    'sandbox_authority_inquiry_reports','commercial_orders','commercial_collections',
    'commercial_revenue_allocations','partner_settlement_events','customer_commercial_owners',
    'customer_subscriptions','subscription_credit_accounts'
  ) ORDER BY tablename,indexname;

-- EXPLAIN ANALYZE runs only SELECT statements under a READ ONLY transaction.
-- Use the current database's test-owned records, not production identities.
PREPARE phase15_sale_cohort(uuid, timestamptz, timestamptz) AS
SELECT COALESCE(SUM(o.gross_amount_irr),0)::bigint AS gross,
  COALESCE(SUM(o.collected_amount_irr),0)::bigint AS collected,
  COALESCE(SUM(o.gross_amount_irr-o.collected_amount_irr),0)::bigint AS outstanding
FROM commercial_orders o
WHERE o.status<>4 AND o.issued_at_utc >= $2 AND o.issued_at_utc < $3
  AND o.root_admin_user_id=$1;

PREPARE phase15_verified_collections(uuid, timestamptz, timestamptz) AS
SELECT COALESCE(SUM(c.amount_irr),0)::bigint
FROM commercial_collections c
JOIN commercial_orders o ON o.id=c.order_id
WHERE o.root_admin_user_id=$1 AND o.status<>4
  AND c.verified_at_utc >= $2 AND c.verified_at_utc < $3;

PREPARE phase15_partner_scope(uuid) AS
SELECT count(*)::bigint FROM customer_commercial_owners o
JOIN identity_users u ON u.id=o.customer_user_id AND u.is_active=true
WHERE o.partner_level2_user_id=$1;

PREPARE phase15_expiring_subscriptions(uuid, timestamptz, timestamptz) AS
SELECT count(*)::bigint
FROM customer_subscriptions s
JOIN customer_commercial_owners o ON o.customer_user_id=s.customer_user_id
JOIN identity_users u ON u.id=s.customer_user_id AND u.is_active=true
WHERE o.root_level1_user_id=$1 AND s.state=2
  AND s.activated_at_utc <= $2 AND s.expires_at_utc > $2
  AND s.expires_at_utc <= $3;

-- Choose existing TEST owner identities without printing them to stdout.
-- When there are no qualifying owners, use a zero UUID and mark the sample nonrepresentative.
SELECT COALESCE((
  SELECT root_level1_user_id::text FROM customer_commercial_owners
  WHERE root_level1_user_id IS NOT NULL ORDER BY customer_user_id LIMIT 1
), '00000000-0000-0000-0000-000000000000') AS root_actor \gset
SELECT COALESCE((
  SELECT partner_level2_user_id::text FROM customer_commercial_owners
  WHERE partner_level2_user_id IS NOT NULL ORDER BY customer_user_id LIMIT 1
), '00000000-0000-0000-0000-000000000000') AS partner_actor \gset
SELECT CASE WHEN EXISTS(SELECT 1 FROM customer_commercial_owners) THEN 'test owners present'
  ELSE 'WARNING: no test owners, plans not representative' END AS sample_quality;
\echo ==== ROOT COMMERCIAL SALES PLAN ====
EXPLAIN (ANALYZE, BUFFERS, TIMING OFF, SUMMARY ON)
EXECUTE phase15_sale_cohort( :'root_actor'::uuid,
  now()-interval '30 days', now());

\echo ==== VERIFIED RECEIPTS PLAN ====
EXPLAIN (ANALYZE, BUFFERS, TIMING OFF, SUMMARY ON)
EXECUTE phase15_verified_collections( :'root_actor'::uuid,
  now()-interval '30 days', now());

\echo ==== PARTNER OWNERSHIP PLAN ====
EXPLAIN (ANALYZE, BUFFERS, TIMING OFF, SUMMARY ON)
EXECUTE phase15_partner_scope(:'partner_actor'::uuid);

\echo ==== SUBSCRIPTION EXPIRY PLAN ====
EXPLAIN (ANALYZE, BUFFERS, TIMING OFF, SUMMARY ON)
EXECUTE phase15_expiring_subscriptions(:'root_actor'::uuid,
  now(), now()+interval '30 days');

-- Existing SQL can be profiled with representative TEST actor ID from an operator
-- session. Do not commit tokens, customer IDs or raw EXPLAIN output to Git.
ROLLBACK;
