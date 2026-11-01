-- Read-only. Run only against argintaxpayer_test after applying the reviewed migration.
BEGIN READ ONLY;
DO $audit$
BEGIN
 IF current_database()<>'argintaxpayer_test' THEN
  RAISE EXCEPTION 'Expected argintaxpayer_test';
 END IF;
 IF to_regclass('public.application_settings') IS NULL THEN
  RAISE EXCEPTION 'application_settings is missing';
 END IF;
 IF to_regclass('public.commercial_checkout_tax_settings') IS NOT NULL THEN
  RAISE EXCEPTION 'Legacy single-purpose VAT table unexpectedly exists';
 END IF;
 IF EXISTS(
  SELECT 1 FROM application_settings
  WHERE setting_key='checkout.vat' AND
    (jsonb_typeof(value_json->'basis_points')<>'number' OR
     (value_json->>'basis_points')::integer NOT BETWEEN 0 AND 10000)
 ) THEN RAISE EXCEPTION 'Invalid checkout VAT setting'; END IF;
 RAISE NOTICE 'PASS: Phase12 extensible application settings schema and VAT integrity';
END $audit$;
ROLLBACK;
