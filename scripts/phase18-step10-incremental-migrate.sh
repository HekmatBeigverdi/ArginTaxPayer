#!/usr/bin/env bash
# Phase18 Step10: single guarded incremental migration of existing dedicated Production DB.
# The existing Step07 backup/restore drill is deliberately restricted to an empty identity
# dataset. This script refuses real identity data rather than making an unencrypted backup.
set -Eeuo pipefail
umask 077
fail() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "${1:-}" == "--apply-reviewed-environment-column" && "$#" == 1 ]] ||
  fail 'Usage: --apply-reviewed-environment-column'
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Run as nonroot Ubuntu operator'
[[ "${PHASE18_OWNER_APPROVED_INCREMENTAL_MIGRATION:-}" == YES ]] ||
  fail 'Explicit owner approval is required'
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] ||
  fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
  fail 'Dirty tracked source'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" == "$sha" && "$sha" =~ ^[a-f0-9]{40}$ ]] ||
  fail 'Exact approved macOS/Ubuntu SHA required'
for tool in dotnet psql flock sha256sum sudo; do
  command -v "$tool" >/dev/null || fail "Missing $tool"
done
exec 9> /tmp/argintaxpayer-phase18-step10-migration.lock
flock -n 9 || fail 'Another incremental migration is running'
sudo -v
private="$PWD/artifacts/phase18-step07/production-private"
[[ -f "$private/compose.env" && -f "$private/ca/ca.pem" ]] ||
  fail 'Dedicated Production DB private configuration missing'
[[ "$(stat -c %a "$private/compose.env")" == 600 ]] ||
  fail 'Insecure private credentials'
# The phase18-activation-approved marker authorizes service startup, not schema migration.
# Do not create or require it here: the migration is independently owner-gated.
sudo grep -Fxq 'ArginTaxPayer__EnableOperationalEndpoints=false' /etc/argintaxpayer/api.env ||
  fail 'Operational APIs must remain disabled'
sudo grep -Fxq 'ARGIN_PRODUCTION_UI_ENABLED=false' /etc/argintaxpayer/web.env ||
  fail 'Production UI must remain disabled'
migrator=$(sed -n 's/^PHASE18_PROD_MIGRATOR_PASSWORD=//p' "$private/compose.env")
[[ "$migrator" =~ ^[a-f0-9]{64}$ ]] || fail 'Invalid migrator credential'
export PGSSLMODE=verify-full PGSSLROOTCERT="$private/ca/ca.pem" PGPASSWORD="$migrator"
psql_args=(-h 127.0.0.1 -p 15434 -U argin_prod_migrator -d argintaxpayer_prod -X -A -t -v ON_ERROR_STOP=1)
identity=$(psql "${psql_args[@]}" -c \
  "SELECT current_database(),current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid());")
[[ "$identity" == 'argintaxpayer_prod|argin_prod_migrator|t' ]] ||
  fail 'Wrong DB, role or TLS state'
latest=$(psql "${psql_args[@]}" -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')
target=20261104220000_AddFiscalMemoryAuthorityEnvironment
if [[ "$latest" == "$target" ]]; then
  echo '[PASS] Target migration already present; no changes made.'
  exit 0
fi
[[ "$latest" == '20261102050000_AddSmsCustomerBillingEnforcement' ]] ||
  fail "Unexpected migration history baseline: $latest"
column=$(psql "${psql_args[@]}" -c \
  "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='fiscal_memories' AND column_name='authority_environment';")
[[ "$column" == 0 ]] || fail 'Column exists but migration history absent; manual reconciliation required'
# Existing Step07 backup/restore drill is safe only before any real identities are stored.
identities=$(psql "${psql_args[@]}" -c 'SELECT count(*) FROM public.identity_users;')
[[ "$identities" == 0 ]] ||
  fail 'Existing identity data: use separately approved ENCRYPTED backup and restore; refusing unencrypted drill'
[[ -n "${PHASE18_REVIEWED_SQL_SHA256:-}" && "${PHASE18_REVIEWED_SQL_SHA256}" =~ ^[a-f0-9]{64}$ ]] ||
  fail 'Expected full SQL SHA256 is required'
review=$(realpath -e -- "${PHASE18_REVIEWED_SQL_PATH:-/nonexistent}") ||
  fail 'Reviewed SQL file absent'
[[ "$review" == "$PWD/artifacts/phase18-step07/sql-review/$sha/"*"/migrations.sql" ]] ||
  fail 'Reviewed SQL belongs to another SHA or directory'
actual=$(sha256sum "$review" | cut -d' ' -f1)
[[ "$actual" == "$PHASE18_REVIEWED_SQL_SHA256" ]] ||
  fail 'Reviewed SQL hash mismatch'
# Compare exact offline SQL regenerated from source, not from an external DB.
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT
offline='Host=127.0.0.1;Port=1;Database=phase18_offline_sql_only;Username=no_live_access;Password=no_live_access;SSL Mode=Require;Timeout=1'
ConnectionStrings__ArginTaxPayerDb="$offline" dotnet ef migrations script --idempotent \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --configuration Release --output "$tmp" >/dev/null
[[ "$(sha256sum "$tmp" | cut -d' ' -f1)" == "$actual" ]] ||
  fail 'EF SQL generation mismatch; source or tooling changed'
echo '[START] Isolated recovery drill before migration'
PHASE18_RESTORE_DRILL_APPROVED=YES PHASE18_EXPECTED_SHA="$sha" \
  bash scripts/phase18-step07-backup-restore-drill.sh
echo '[START] Apply only reviewed Step10 migration to dedicated Production DB'
connection="Host=127.0.0.1;Port=15434;Database=argintaxpayer_prod;Username=argin_prod_migrator;Password=$migrator;SSL Mode=VerifyFull;Root Certificate=$private/ca/ca.pem;Timeout=5"
# Migration engine applies all migrations up to target; baseline is verified above.
ConnectionStrings__ArginTaxPayerDb="$connection" dotnet ef database update "$target" \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --configuration Release
unset connection migrator
latest=$(psql "${psql_args[@]}" -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')
column=$(psql "${psql_args[@]}" -c \
  "SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='fiscal_memories' AND column_name='authority_environment' AND is_nullable='YES';")
[[ "$latest" == "$target" && "$column" == 1 ]] ||
  fail 'Postmigration verification failed; stop before application promotion'
echo '[PASS] Exact Phase18 Step10 additive nullable-column migration installed.'
echo '[NEXT] Run independent postmigration recovery verification, then stage compatible API.'
