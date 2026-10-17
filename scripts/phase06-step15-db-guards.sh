#!/usr/bin/env bash
set -euo pipefail

# Read-only PostgreSQL guard inspection. No SQL writes or migrations.
# Intentionally requires PGHOST/PGPORT/PGUSER/PGDATABASE/PGPASSWORD from the
# operator's approved PRIVATE environment. Never source arbitrary project files.
if [[ "${PGDATABASE:-}" != "argintaxpayer_test" ]]; then
  echo "ABORT: PGDATABASE must equal argintaxpayer_test" >&2
  exit 2
fi
for variable in PGHOST PGPORT PGUSER PGPASSWORD; do
  if [[ -z "${!variable:-}" ]]; then
    echo "ABORT: $variable is missing (private test database configuration)" >&2
    exit 2
  fi
done
if [[ "${PGHOST}" != "127.0.0.1" || "${PGPORT}" != "15432" ]]; then
  echo "ABORT: expected the owner-approved localhost AutoSSH tunnel at 127.0.0.1:15432" >&2
  exit 2
fi

psql -X -v ON_ERROR_STOP=1 -Atq <<'SQL'
BEGIN TRANSACTION READ ONLY;
SELECT CASE WHEN current_database() = 'argintaxpayer_test' THEN 'DATABASE_OK'
            ELSE (1 / 0)::text END AS database_guard;
SELECT 'units_102=' || count(*) FROM official_measurement_units
WHERE release_id='14051800-0000-4000-8000-000000000118';
SELECT 'critical_trigger=' || tgname FROM pg_trigger
WHERE tgname IN (
 'trg_phase06_tax_mapping_guard',
 'trg_phase06_audit_immutable'
) AND NOT tgisinternal ORDER BY tgname;
SELECT 'customer_code_index=' || indexname FROM pg_indexes
WHERE tablename='customer_catalog_items'
 AND indexname='ux_customer_catalog_internal_code';
SELECT 'official_mapping_count=' || count(*) FROM catalog_official_mappings;
ROLLBACK;
SQL
