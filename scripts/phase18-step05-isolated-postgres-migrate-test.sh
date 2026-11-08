#!/usr/bin/env bash
# Phase18 ONLY: verify TLS and apply EF migrations to dedicated disposable DB.
# Never touches existing Docker PostgreSQL at 5432 or argintaxpayer_test.
set -Eeuo pipefail
umask 077
fail() { echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Tracked worktree dirty'
for x in dotnet psql git; do command -v "$x" >/dev/null || fail "Missing $x"; done
root="$PWD/artifacts/phase18-step05-db/isolated"
[[ -f "$root/compose.env" && -f "$root/ca/ca.pem" ]] || fail 'Run isolated postgres provisioner first'
[[ "$(stat -c '%a' "$root/compose.env")" == 600 ]] || fail 'Private env permissions must be 600'
dbpass=$(sed -n 's/^PHASE18_DB_PASSWORD=//p' "$root/compose.env")
[[ "$dbpass" =~ ^[0-9a-f]{64}$ ]] || fail 'Invalid private acceptance DB secret'
common=(-h 127.0.0.1 -p 15433 -U phase18_acceptance -d argin_phase18_acceptance -X -v ON_ERROR_STOP=1)
export PGPASSWORD="$dbpass" PGSSLMODE=verify-full PGSSLROOTCERT="$root/ca/ca.pem"
# Verify SSL with full CA and hostname validation before any SQL writes.
echo '[START] VerifyFull TLS PostgreSQL connection, explicit DB and least privilege'
proof=$(psql "${common[@]}" -Atc "SELECT current_database(), current_user, (SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()), (SELECT rolsuper FROM pg_roles WHERE rolname=current_user), (SELECT rolcreatedb FROM pg_roles WHERE rolname=current_user);" 2>/dev/null) ||
  fail 'TLS validation or QA database connection failed'
[[ "$proof" == 'argin_phase18_acceptance|phase18_acceptance|t|f|f' ]] ||
  fail 'Wrong database/role/TLS/superuser capability'
# This QA database must initially be empty. Fail instead of assuming ownership.
tables=$(psql "${common[@]}" -Atc "SELECT COUNT(*) FROM pg_tables WHERE schemaname='public';")
if [[ "$tables" != 0 ]]; then
  echo '[INFO] Acceptance database already has schema; migration will proceed only if managed EF history exists.'
  history=$(psql "${common[@]}" -Atc "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")
  [[ "$history" == t ]] || fail 'Non-empty database with no EF migration history'
fi
command -v dotnet >/dev/null || fail 'dotnet missing'
dotnet ef --version >/dev/null 2>&1 ||
  fail 'dotnet-ef missing. Install required EF CLI in user scope; do not migrate another DB.'
connection="Host=127.0.0.1;Port=15433;Database=argin_phase18_acceptance;Username=phase18_acceptance;Password=$dbpass;SSL Mode=VerifyFull;Root Certificate=$root/ca/ca.pem;Timeout=5"
unset dbpass
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step05-db/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
echo '[START] Apply EF migrations to ONLY argin_phase18_acceptance via VerifyFull TLS'
if ConnectionStrings__ArginTaxPayerDb="$connection" dotnet ef database update \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --configuration Release > "$out/migration.log" 2>&1; then
  echo '[PASS] Dedicated QA database migrations completed.'
else
  echo "[FAIL] Migration; inspect private log $out/migration.log; DO NOT retry on another database."
  exit 1
fi
echo '[START] Verify isolated MFA/RBAC acceptance'
if RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1 \
   PHASE18_DISPOSABLE_DB_CONNECTION="$connection" \
   bash scripts/phase18-step05-disposable-db-acceptance.sh; then
  echo '[PASS] Dedicated QA MFA and role acceptance'
else
  echo '[FAIL] MFA acceptance; review private report. Production release remains blocked.'
  exit 1
fi
unset PGPASSWORD
echo '[DONE] Existing PostgreSQL and public site unchanged; test DB retained for explicit cleanup.'
