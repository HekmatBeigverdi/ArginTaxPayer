#!/usr/bin/env python3
"""Read-only Phase09 PostgreSQL structural and readiness assessment.

Never prints secrets, usernames, raw table data, taxpayer identity or PEM.
Uses existing restricted connection verifier, requiring argintaxpayer_test over
loopback/approved SSH tunnel. No UPDATE/DELETE/INSERT/ALTER are issued.
"""
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(__file__))
from phase09_verify import guard_database, SafeError

SQL = r"""
BEGIN READ ONLY;
SELECT CASE WHEN current_database()='argintaxpayer_test' THEN 'OK' ELSE 'BAD' END;
SELECT CASE WHEN to_regclass('public.signing_operations') IS NOT NULL
 AND to_regclass('public.signing_key_versions') IS NOT NULL
 AND to_regclass('public.signing_change_requests') IS NOT NULL
 AND to_regclass('public.signing_private_key_envelopes') IS NOT NULL
 AND to_regclass('public.signing_audit_events') IS NOT NULL
 THEN 'OK' ELSE 'BAD' END;
SELECT CASE WHEN (SELECT COUNT(*) FROM "__EFMigrationsHistory"
 WHERE "MigrationId" IN (
 '20261020181500_AddPhase09SigningMetadata',
 '20261022140000_AddEncryptedDatabaseSigningCustody')) = 2
 THEN 'OK' ELSE 'BAD' END;
SELECT CASE WHEN (SELECT COUNT(*) FROM pg_constraint
 WHERE conrelid='public.signing_change_requests'::regclass
 AND conname='ck_signing_review_consumption')=1 THEN 'OK' ELSE 'BAD' END;
ROLLBACK;
"""

def main():
    try:
        environment = guard_database()
        # Safe pg vars are constructed from the same validated test DSN.
        from phase09_verify import parse_connection
        cfg = parse_connection(os.environ.get("ConnectionStrings__ArginTaxPayerDb", ""))
        pg = {k: v for k, v in os.environ.items() if not k.startswith("PG")}
        pg.update(PGHOST=cfg["host"], PGPORT=cfg["port"], PGUSER=cfg["username"],
                  PGDATABASE=cfg["database"], PGPASSWORD=cfg["password"],
                  PGSSLMODE=cfg["sslmode"], PGPASSFILE=os.devnull)
        r = subprocess.run(["psql", "-X", "-w", "-q", "-A", "-t", "-v", "ON_ERROR_STOP=1"],
                           input=SQL, capture_output=True, text=True, env=pg, timeout=30)
        checks = [x.strip() for x in r.stdout.splitlines() if x.strip() in ("OK", "BAD")]
        if r.returncode or checks != ["OK"] * 4:
            raise SafeError("Required Phase09 schemas, migrations, or constraints absent.")
        print("PASS: test database and signing custody/review schema (read-only).")
    except (SafeError, OSError, subprocess.TimeoutExpired):
        print("FAIL: PostgreSQL safety/readiness gate (details withheld).", file=sys.stderr)
        return 2
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
