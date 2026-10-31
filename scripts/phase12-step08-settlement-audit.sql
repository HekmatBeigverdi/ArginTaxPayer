-- Read-only audit, only on local test database after migrations are reviewed/applied.
BEGIN READ ONLY;
DO $audit$
BEGIN
 IF current_database() <> 'argintaxpayer_test' THEN
   RAISE EXCEPTION 'Run Step08 settlement audit only on argintaxpayer_test';
 END IF;
 IF EXISTS (
   SELECT 1 FROM commercial_revenue_allocations a
   LEFT JOIN LATERAL (
      SELECT COALESCE(SUM(CASE WHEN kind=1 THEN amount_irr ELSE 0 END),0) paid,
             COALESCE(SUM(CASE WHEN kind=2 THEN amount_irr ELSE 0 END),0) reversed
      FROM partner_settlement_events e WHERE e.allocation_id=a.id
   ) s ON true
   WHERE s.reversed>s.paid OR s.paid-s.reversed>a.partner_share_irr
 ) THEN RAISE EXCEPTION 'Partner has been overpaid or over-reversed'; END IF;
 IF EXISTS (
   SELECT 1 FROM partner_settlement_events e
   JOIN commercial_revenue_allocations a ON a.id=e.allocation_id
   WHERE e.root_user_id<>a.root_user_id OR e.partner_user_id IS DISTINCT FROM a.partner_user_id
 ) THEN RAISE EXCEPTION 'Settlement owner mismatch'; END IF;
 IF EXISTS (
   SELECT 1 FROM partner_settlement_events reverse
   LEFT JOIN partner_settlement_events original ON original.id=reverse.original_event_id
   WHERE reverse.kind=2 AND
    (original.id IS NULL OR original.kind<>1 OR original.allocation_id<>reverse.allocation_id
      OR original.amount_irr<>reverse.amount_irr)
 ) THEN RAISE EXCEPTION 'Invalid settlement reversal'; END IF;
 RAISE NOTICE 'PASS: Phase12 Step08 settlement audit';
END $audit$;
ROLLBACK;
