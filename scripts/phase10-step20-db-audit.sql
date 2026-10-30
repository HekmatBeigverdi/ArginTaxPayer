-- ArginTaxPayer Phase10 Step20 read-only security/DB evidence.
-- Run only against the operator-selected argintaxpayer_test.
\set ON_ERROR_STOP on
BEGIN READ ONLY;
DO $$
DECLARE missing text;
BEGIN
  IF current_database() <> 'argintaxpayer_test' THEN
    RAISE EXCEPTION 'Refusing database other than argintaxpayer_test';
  END IF;
  SELECT string_agg(name,', ') INTO missing
  FROM (VALUES
    ('invoice_documents'),('invoice_document_revisions'),('invoice_operations'),
    ('invoice_dispatch_operations'),('invoice_dispatch_attempts'),
    ('invoice_dispatch_receipts'),('sandbox_fiscal_memories')
  ) v(name)
  WHERE to_regclass('public.' || name) IS NULL;
  IF missing IS NOT NULL THEN
    RAISE EXCEPTION 'Required phase10 tables missing: %',missing;
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM pg_trigger
    WHERE tgrelid='public.invoice_document_revisions'::regclass
      AND tgname='trg_phase07_invoice_history_guard' AND tgenabled='O'
  ) THEN
    RAISE EXCEPTION 'Invoice revision immutability trigger disabled/missing';
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM pg_trigger
    WHERE tgrelid='public.invoice_dispatch_attempts'::regclass
      AND tgname='trg_p10_attempts_immutable' AND tgenabled='O'
  ) OR NOT EXISTS (
    SELECT 1 FROM pg_trigger
    WHERE tgrelid='public.invoice_dispatch_receipts'::regclass
      AND tgname='trg_p10_receipts_immutable' AND tgenabled='O'
  ) THEN
    RAISE EXCEPTION 'Phase10 dispatch evidence immutability trigger disabled/missing';
  END IF;
  IF (SELECT count(*) FROM "__EFMigrationsHistory"
      WHERE "MigrationId" IN (
        '20261023190000_AddPhase10InvoiceDispatchOutbox',
        '20261024103000_AddSandboxFiscalMemoryMetadata',
        '20261024170000_AddSandboxEncryptedCustodyColumns')) <> 3 THEN
    RAISE EXCEPTION 'Expected Phase10 migration history missing';
  END IF;
END $$;

SELECT current_database() AS test_database,
       (SELECT count(*) FROM public.invoice_documents) AS document_count,
       (SELECT count(*) FROM public.invoice_document_revisions) AS immutable_revision_count,
       (SELECT count(*) FROM public.invoice_dispatch_operations) AS dispatch_operation_count,
       (SELECT count(*) FROM public.invoice_dispatch_attempts) AS attempt_count,
       (SELECT count(*) FROM public.invoice_dispatch_receipts) AS receipt_count,
       (SELECT count(*) FROM public.sandbox_fiscal_memories) AS sandbox_memory_count;

SELECT conrelid::regclass::text AS relation,
       conname AS constraint_name,
       contype AS constraint_type
FROM pg_constraint
WHERE conrelid IN (
  'public.invoice_documents'::regclass,
  'public.invoice_document_revisions'::regclass,
  'public.invoice_dispatch_operations'::regclass,
  'public.sandbox_fiscal_memories'::regclass)
AND contype IN ('c','f','p','u')
ORDER BY 1,2;
COMMIT;
