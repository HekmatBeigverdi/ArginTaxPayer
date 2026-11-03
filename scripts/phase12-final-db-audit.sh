#!/usr/bin/env bash
# Guarded, read-only Phase12 acceptance on an explicit test database.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
: "${PHASE12_PGHOST:?Set PHASE12_PGHOST to the test DB host}"
: "${PHASE12_PGPORT:?Set PHASE12_PGPORT to the test DB port}"
: "${PHASE12_PGUSER:?Set PHASE12_PGUSER to the test DB user}"
db=argintaxpayer_test
export PGHOST="$PHASE12_PGHOST" PGPORT="$PHASE12_PGPORT" PGUSER="$PHASE12_PGUSER" PGDATABASE="$db"
command -v psql >/dev/null || { echo "psql missing" >&2; exit 2; }
actual="$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT current_database()')"
[[ "$actual" == "$db" ]] || { echo "REFUSED: expected test DB, got $actual" >&2; exit 2; }
scripts=(
  scripts/phase12-step07-ledger-audit.sql
  scripts/phase12-step08-settlement-audit.sql
  scripts/phase12-step10-finance-audit.sql
  scripts/phase12-checkout-audit.sql
  scripts/phase12-application-settings-audit.sql
)
if [[ -f scripts/phase12-batch-settlement-audit.sql ]]; then
 scripts+=(scripts/phase12-batch-settlement-audit.sql)
fi
failed=0
for s in "${scripts[@]}"; do
 echo "[START] $s"
 if psql -X -v ON_ERROR_STOP=1 -f "$s"; then
  echo "[PASS] $s"
 else
  echo "[FAIL] $s"
  failed=$((failed+1))
 fi
done
(( failed == 0 )) || { echo "DB audit failures: $failed"; exit 1; }
echo "[PASS] All read-only phase12 financial audits on $db"
