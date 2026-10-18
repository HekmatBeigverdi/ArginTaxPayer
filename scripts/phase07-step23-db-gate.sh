#!/usr/bin/env bash
# Phase07 Step23 read-only PostgreSQL acceptance gate. Called only after
# operator has established the standard development connection.
set -Eeuo pipefail
conn="${ConnectionStrings__ArginTaxPayerDb:-}"
[[ -n "$conn" ]] || { echo 'Missing test DB connection environment' >&2; exit 2; }
field() { printf '%s\n' "$conn" | tr ';' '\n' | sed -n "s/^$1=//p" | head -n1; }
host="$(field Host)"
port="$(field Port)"
db="$(field Database)"
user="$(field Username)"
password="$(field Password)"
[[ "$db" == argintaxpayer_test &&
   "$host" == 127.0.0.1 &&
   "$port" =~ ^(5432|15432)$ &&
   -n "$user" && -n "$password" ]] || {
  echo 'Refusing: only loopback argintaxpayer_test is permitted' >&2; exit 2;
}
command -v psql >/dev/null || { echo 'psql is required' >&2; exit 2; }
export PGPASSWORD="$password"
unset password conn
sql=$(cat <<'SQL'
SELECT CASE WHEN current_database()='argintaxpayer_test' THEN 'OK' ELSE 'INVALID' END;
SELECT 'MISSING_MIGRATION ' || wanted.id
FROM (VALUES
 ('20261018090000_ScopeFiscalParticipantsPerCustomer'),
 ('20261018150000_AddResumableInvoiceEntryWorkspaces')
) AS wanted(id)
WHERE NOT EXISTS (
 SELECT 1 FROM "__EFMigrationsHistory" h WHERE h."MigrationId" = wanted.id
);
SELECT CASE WHEN to_regclass('public.invoice_entry_workspaces') IS NULL
 THEN 'MISSING_TABLE invoice_entry_workspaces' ELSE 'OK_TABLE' END;
SELECT CASE WHEN to_regclass('public.ux_parties_owner_kind_identifier') IS NULL
 THEN 'MISSING_INDEX ux_parties_owner_kind_identifier' ELSE 'OK_PARTY_INDEX' END;
SELECT CASE WHEN to_regclass('public.ux_taxpayer_profiles_customer_kind_identifier') IS NULL
 THEN 'MISSING_INDEX ux_taxpayer_profiles_customer_kind_identifier' ELSE 'OK_TAXPAYER_INDEX' END;
SELECT CASE WHEN to_regclass('public.ix_invoice_entry_workspaces_scope') IS NULL
 THEN 'MISSING_INDEX ix_invoice_entry_workspaces_scope' ELSE 'OK_DRAFT_INDEX' END;
SQL
)
output="$(PGCONNECT_TIMEOUT=10 psql -X -q -v ON_ERROR_STOP=1 -h "$host" -p "$port" -U "$user" -d "$db" -Atc "$sql")"
printf '%s\n' "$output"
if grep -Eq '^MISSING_|^INVALID$' <<< "$output"; then
 echo 'FAIL: required Phase07 migrations or schema are missing. No changes were applied.' >&2
 exit 1
fi
[[ "$output" == *"OK_TABLE"* && "$output" == *"OK_PARTY_INDEX"* &&
   "$output" == *"OK_TAXPAYER_INDEX"* && "$output" == *"OK_DRAFT_INDEX"* ]] || exit 1
echo 'PASS: test PostgreSQL schema and migrations verified; no writes.'
