-- Run ONLY against verified argintaxpayer_test. Read-only integrity checks.
BEGIN READ ONLY;
DO $audit$
BEGIN
  IF current_database() <> 'argintaxpayer_test' THEN
    RAISE EXCEPTION 'Test database required';
  END IF;
  IF EXISTS(
    SELECT 1 FROM commercial_revenue_allocations a
    WHERE a.gross_collected_irr <>
      a.approved_cost_irr+a.partner_share_irr+a.root_share_irr
  ) THEN RAISE EXCEPTION 'Allocation imbalance'; END IF;
  IF EXISTS(
    SELECT 1 FROM commercial_revenue_allocations a
    JOIN commercial_collections c ON c.id=a.collection_id
    WHERE a.gross_collected_irr<>c.amount_irr
  ) THEN RAISE EXCEPTION 'Allocation exceeds or differs from verified cash'; END IF;
  IF EXISTS(
    SELECT 1 FROM commercial_revenue_allocations a
    JOIN commercial_collections c ON c.id=a.collection_id
    JOIN commercial_orders o ON o.id=c.order_id
    WHERE a.root_user_id<>o.root_admin_user_id
       OR a.partner_user_id IS DISTINCT FROM o.partner_user_id
  ) THEN RAISE EXCEPTION 'Historical owner attribution mismatch'; END IF;
  IF EXISTS(
    SELECT collection_id FROM commercial_revenue_allocations
    GROUP BY collection_id HAVING count(*)>1
  ) THEN RAISE EXCEPTION 'Duplicate partner allocations'; END IF;
  RAISE NOTICE 'PASS: Phase12 Step07 immutable allocation consistency';
END $audit$;
ROLLBACK;
