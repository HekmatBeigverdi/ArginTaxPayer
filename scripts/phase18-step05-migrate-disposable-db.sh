#!/usr/bin/env bash
# Phase18 Step05 - migrate ONLY a newly created disposable database.
# Separate from production/test DB; never changes original postgres-postgres-1.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
fail() { echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux ]] || fail 'Ubuntu VPS only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Tracked files changed'
[[ "${PHASE18_MIGRATE_DISPOSABLE_DB:-}" == YES ]] || fail 'Explicit PHASE18_MIGRATE_DISPOSABLE_DB=YES required'
for tool in dotnet psql git stat; do command -v "$tool" >/dev/null || fail "Missing $tool"; done
config="$PWD/artifacts/phase18-step05-db-infra/connection.env"
[[ -f "$config" && ! -L "$config" ]] || fail 'No dedicated private connection file'
[[ "$(stat -c '%a' "$config")" == 600 ]] || fail 'Connection file must have 0600 permissions'
# File created exclusively by the Phase18 provisioner; never source arbitrary file.
source "$config"
[[ "$PHASE18_PG_HOST" == localhost && "$PHASE18_PG_PORT" == 15433 &&
   "$PHASE18_PG_DATABASE" == argin_phase18_acceptance &&
   "$PHASE18_PG_USER" == phase18_acceptance ]] || fail 'Unexpected DB identity'
[[ -f "$PHASE18_PG_SSL_ROOT" ]] || fail 'Missing local test CA'
export PGHOST="$PHASE18_PG_HOST" PGPORT="$PHASE18_PG_PORT"
export PGUSER="$PHASE18_PG_USER" PGDATABASE="$PHASE18_PG_DATABASE"
export PGSSLMODE=verify-full PGSSLROOTCERT="$PHASE18_PG_SSL_ROOT"
export PGPASSWORD="$PHASE18_PG_PASSWORD"
verified=$(psql -X -Atq -v ON_ERROR_STOP=1 \
  -c "SELECT current_database() || '|' || current_user || '|' || s.ssl::text || '|' || r.rolsuper::text FROM pg_stat_ssl s CROSS JOIN pg_roles r WHERE s.pid=pg_backend_pid() AND r.rolname=current_user")
[[ "$verified" == 'argin_phase18_acceptance|phase18_acceptance|true|false' ]] \
  || fail 'Database / role / TLS / superuser guard failed'
# Migration is one-time. Refuse any existing EF history or unexpected public objects.
state=$(psql -X -Atq -v ON_ERROR_STOP=1 \
  -c "SELECT count(*) FROM pg_tables WHERE schemaname='public'")
[[ "$state" == 0 ]] || fail 'Public schema already contains tables; migration is NOT retried automatically'
dotnet ef --version >/dev/null 2>&1 || fail 'dotnet-ef missing; do NOT install or change DB automatically'
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step05-db/$sha/$(date -u +%Y%m%dT%H%M%SZ)-migration"
mkdir -p "$out"; chmod 700 "$out"
echo '[START] EF migrations against dedicated disposable database only'
export ConnectionStrings__ArginTaxPayerDb="$PHASE18_DISPOSABLE_DB_CONNECTION"
if dotnet ef database update \
   --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
   --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
   > "$out/migration.log" 2>&1; then
  echo '[PASS] Disposable DB schema migration'
else
  echo "[FAIL] Migration failed. Do not retry until private log reviewed: $out/migration.log"
  exit 1
fi
printf 'sha=%s\nmigration=PASS\n' "$sha" > "$out/manifest.txt"
echo "[DONE] Private migration evidence: $out/manifest.txt"
echo '[NOTE] No customer or tax authority resources have been changed.'
