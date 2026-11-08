"""Read-only PostgreSQL regression for the actual legacy Sandbox SQL.

Opt in with RUN_HOSTED_SANDBOX_SQL_TESTS=1 and a dedicated PG* connection.
Fixtures are CTEs, so no database tables or customer evidence are modified.
"""
from pathlib import Path
import os
import re
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
FILES = ["Phase10LegacyOutboxWorker.cs", "Phase10LegacyReferenceInquiryService.cs",
         "Phase10LegacyUidInquiryService.cs"]
IDS = {name: f"00000000-0000-0000-0000-{number:012d}"
       for number, name in enumerate(["owner", "taxpayer", "memory", "invoice", "operation", "attempt"], 1)}


@unittest.skipUnless(os.environ.get("RUN_HOSTED_SANDBOX_SQL_TESTS") == "1", "Dedicated PostgreSQL opt-in required")
class LegacySandboxScopeTests(unittest.TestCase):
    def result_count(self, file, *, environment=2, foreign_owner=False):
        source = (ROOT / "src/ArginTaxPayer.Infrastructure/Persistence/Invoices" / file).read_text()
        candidates = re.findall(r'CommandText\s*=\s*"""\s*(.*?)\s*"""', source, re.S)
        query = next(q for q in candidates if q.startswith("SELECT memory_identifier FROM sandbox_fiscal_memories")
                     or q.startswith("SELECT d.id,d.fiscal_memory_id,m.memory_identifier"))
        for name, value in IDS.items():
            query = query.replace("@" + name, "'" + value + "'::uuid")
        def guid(name): return "'" + IDS[name] + "'::uuid"
        owner = "'00000000-0000-0000-0000-000000000099'::uuid" if foreign_owner else guid("owner")
        state = 6 if "UidInquiry" in file else 5
        fixture = f"""
WITH sandbox_fiscal_memories AS (
 SELECT {guid('memory')} AS id,{owner} AS customer_user_id,{guid('taxpayer')} AS taxpayer_id,'ABC123'::text AS memory_identifier
), fiscal_memories AS (
 SELECT {guid('memory')} AS id,{guid('taxpayer')} AS taxpayer_id,1 AS authority_environment WHERE false
), invoice_dispatch_operations AS (
 SELECT {guid('operation')} AS id,{guid('owner')} AS customer_user_id,{guid('taxpayer')} AS taxpayer_id,
 {guid('memory')} AS fiscal_memory_id,{guid('invoice')} AS invoice_id,{state} AS state,
 1::bigint AS state_revision,{environment} AS authority_environment,now() AS created_at_utc
), outbox_messages AS (
 SELECT {guid('operation')} AS id,'invoice.sandbox.legacy.dispatch.requested'::text AS event_type,
 jsonb_build_object('environment','Sandbox','schema','phase10.sandbox.legacy.invoice.dispatch.v1',
 'operationId',{guid('operation')}::text,'customerUserId',{guid('owner')}::text,
 'taxpayerId',{guid('taxpayer')}::text,'invoiceId',{guid('invoice')}::text,
 'fiscalMemoryId',{guid('memory')}::text,'requestJson','{{}}','fiscalTaxId','synthetic') AS payload_json
), invoice_dispatch_attempts AS (
 SELECT {guid('attempt')} AS id,{guid('operation')} AS operation_id,1 AS attempt_number
), invoice_dispatch_receipts AS (
 SELECT {guid('operation')} AS operation_id,1 AS receipt_kind,'reference'::text AS authority_reference
)
SELECT count(*) FROM ({query}) AS observed;
"""
        result = subprocess.run(["psql", "-X", "-At", "-v", "ON_ERROR_STOP=1"],
            input="BEGIN READ ONLY; SET LOCAL statement_timeout='5s';\n" + fixture + "ROLLBACK;",
            text=True, capture_output=True, timeout=15)
        self.assertEqual(result.returncode, 0, result.stderr)
        rows = [line for line in result.stdout.splitlines() if line.isdigit()]
        self.assertEqual(len(rows), 1)
        return int(rows[0])

    def test_sandbox_record_does_not_require_an_unrelated_regular_memory(self):
        for file in FILES:
            with self.subTest(file=file): self.assertEqual(self.result_count(file), 1)

    def test_production_classified_operation_remains_blocked(self):
        for file in FILES:
            with self.subTest(file=file): self.assertEqual(self.result_count(file, environment=1), 0)

    def test_historical_sandbox_operation_keeps_its_original_memory_binding(self):
        for file in FILES:
            with self.subTest(file=file): self.assertEqual(self.result_count(file, environment="NULL::integer"), 1)

    def test_foreign_sandbox_owner_remains_blocked(self):
        for file in FILES:
            with self.subTest(file=file): self.assertEqual(self.result_count(file, foreign_owner=True), 0)


if __name__ == "__main__":
    unittest.main()
