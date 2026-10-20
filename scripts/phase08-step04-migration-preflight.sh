#!/usr/bin/env bash
# Phase08 Step04: read-only, fail-closed migration preflight.
# Sources connection from environment only; never prints credentials.
set -Eeuo pipefail
conn="${ConnectionStrings__ArginTaxPayerDb:-}"
[[ -n "$conn" ]] || { echo 'Missing ConnectionStrings__ArginTaxPayerDb' >&2; exit 2; }
field() { printf '%s\n' "$conn" | tr ';' '\n' | sed -n "s/^$1=//p" | head -n 1; }
host="$(field Host)"; port="$(field Port)"; database="$(field Database)"
username="$(field Username)"; password="$(field Password)"
[[ "$host" == "127.0.0.1" && "$port" =~ ^(5432|15432)$ &&
   "$database" == "argintaxpayer_test" && -n "$username" && -n "$password" ]] || {
  echo 'REFUSED: required loopback argintaxpayer_test connection.' >&2; exit 2;
}
command -v psql >/dev/null || { echo 'psql client is missing' >&2; exit 2; }
export PGPASSWORD="$password" PGCONNECT_TIMEOUT=10
unset conn password
psql_cmd=(psql -X -q -v ON_ERROR_STOP=1 -h "$host" -p "$port" -U "$username" -d "$database")
current="$("${psql_cmd[@]}" -Atqc "SELECT current_database()")"
[[ "$current" == "argintaxpayer_test" ]] || { echo 'REFUSED: live DB mismatch' >&2; exit 2; }
echo "PASS: live database argintaxpayer_test at $host:$port"
"${psql_cmd[@]}" -Atqc "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;"
prior=("20261017010000_AddInvoicePersistenceAndOperationClaims"
       "20261018090000_ScopeFiscalParticipantsPerCustomer"
       "20261018150000_AddResumableInvoiceEntryWorkspaces")
for id in "${prior[@]}"; do
  present="$("${psql_cmd[@]}" -Atqc "SELECT EXISTS(SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='$id')")"
  [[ "$present" == t ]] || { echo "REFUSED: missing prerequisite $id" >&2; exit 3; }
done
already="$("${psql_cmd[@]}" -Atqc "SELECT EXISTS(SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='20261019080000_AddInvoiceReviewWorkflowPersistence')")"
if [[ "$already" == t ]]; then
  echo 'ALREADY_APPLIED: Phase08 migration is in EF history; do not reapply.'
  exit 4
fi
for table in invoice_documents invoice_document_revisions invoice_entry_workspaces; do
  found="$("${psql_cmd[@]}" -Atqc "SELECT to_regclass('public.$table') IS NOT NULL")"
  [[ "$found" == t ]] || { echo "REFUSED: prerequisite table $table missing" >&2; exit 3; }
done
for table in invoice_reviews invoice_review_events; do
  found="$("${psql_cmd[@]}" -Atqc "SELECT to_regclass('public.$table') IS NOT NULL")"
  [[ "$found" == f ]] || { echo "REFUSED: $table exists but migration is not in EF history; manual reconciliation needed" >&2; exit 3; }
done
echo 'PASS: Phase08 additive migration prerequisites; no changes made.'
