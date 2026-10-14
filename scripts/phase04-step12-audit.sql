-- Phase 04 Step12 READ-ONLY PostgreSQL invariants.
-- Before use, manually verify connection is argintaxpayer_test.
-- This file performs no UPDATE/INSERT/DELETE/DDL and never dumps PII.
BEGIN READ ONLY;
DO $$
DECLARE
  db_name text;
  bad_count bigint;
BEGIN
  SELECT current_database() INTO db_name;
  IF db_name <> 'argintaxpayer_test' THEN
    RAISE EXCEPTION 'Refusing unexpected database %', db_name;
  END IF;
  IF to_regclass('public.customer_commercial_owners') IS NULL OR
     to_regclass('public.customer_commercial_owner_changes') IS NULL THEN
    RAISE EXCEPTION 'Step11 commercial ownership migration missing';
  END IF;

  SELECT count(*) INTO bad_count
  FROM customer_commercial_owners o
  WHERE NOT EXISTS (SELECT 1 FROM identity_users u WHERE u.id=o.customer_user_id)
    OR NOT EXISTS (SELECT 1 FROM identity_user_roles r
                   WHERE r.user_id=o.root_level1_user_id AND r.role='Level1')
    OR (o.partner_level2_user_id IS NOT NULL AND NOT EXISTS
        (SELECT 1 FROM identity_user_roles r
         WHERE r.user_id=o.partner_level2_user_id AND r.role='Level2'));
  IF bad_count > 0 THEN
    RAISE EXCEPTION 'Commercial ownership role or FK invariant violations: %', bad_count;
  END IF;

  SELECT count(*) INTO bad_count FROM customer_commercial_owner_changes h
   WHERE h.to_revision <> h.from_revision + 1;
  IF bad_count > 0 THEN
    RAISE EXCEPTION 'Invalid commercial ownership revision history: %', bad_count;
  END IF;

  SELECT count(*) INTO bad_count
  FROM customer_commercial_owners o
  WHERE o.partner_level2_user_id = o.root_level1_user_id;
  IF bad_count > 0 THEN
    RAISE EXCEPTION 'Level1 cannot be its own Level2 partner: %', bad_count;
  END IF;
END $$;

SELECT 'commercial_owners' finding, count(*) count FROM customer_commercial_owners
UNION ALL SELECT 'owned_by_partner', count(*) FROM customer_commercial_owners WHERE partner_level2_user_id IS NOT NULL
UNION ALL SELECT 'direct_root_owned', count(*) FROM customer_commercial_owners WHERE partner_level2_user_id IS NULL
UNION ALL SELECT 'commercial_owner_history', count(*) FROM customer_commercial_owner_changes
UNION ALL SELECT 'unreconciled_registered_customers', count(*)
FROM identity_users u WHERE EXISTS (SELECT 1 FROM identity_user_roles r
  WHERE r.user_id=u.id AND r.role='Customer')
AND NOT EXISTS (SELECT 1 FROM customer_commercial_owners o WHERE o.customer_user_id=u.id)
UNION ALL SELECT 'legacy_unlinked_taxpayers', count(*)
FROM taxpayer_profiles WHERE customer_user_id IS NULL
ORDER BY finding;

COMMIT;
