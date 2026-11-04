#!/usr/bin/env bash
# Phase18 Step07 one-time initial Production schema migration, only after
# separate operator review and a successful isolated backup+restore drill.
# This never uses the shared PostgreSQL or QA 15433. No automatic retries.
set -Eeuo pipefail
umask 077
fail(){ printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "${1:-}" == "--apply-reviewed-initial-schema" && "$#" == 1 ]] ||
  fail 'Explicit --apply-reviewed-initial-schema required'
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked source'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[a-f0-9]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'Full independently verified macOS source SHA required'
[[ "${PHASE18_OWNER_APPROVED_INITIAL_MIGRATION:-}" == YES ]] ||
  fail 'Initial Production migration requires explicit owner approval'
[[ "${PHASE18_REVIEWED_SQL_SHA256:-}" =~ ^[a-f0-9]{64}$ ]] ||
  fail 'Full SHA256 of independently reviewed SQL required'
[[ -n "${PHASE18_REVIEWED_SQL_PATH:-}" ]] || fail 'Reviewed private SQL path required'
[[ -n "${PHASE18_RESTORE_MANIFEST:-}" ]] || fail 'Pre-migration restore PASS manifest required'
for x in dotnet sha256sum psql git stat; do command -v "$x" >/dev/null || fail "Missing $x"; done
dotnet ef --version >/dev/null 2>&1 || fail 'dotnet-ef is required'
root="$PWD/artifacts/phase18-step07/production-private"
[[ -f "$root/compose.env" && -f "$root/ca/ca.pem" &&
  "$(stat -c %a "$root/compose.env")" == 600 ]] || fail 'Private Production DB config absent/unsafe'
review=$(realpath -- "${PHASE18_REVIEWED_SQL_PATH}")
[[ "$review" == "$PWD/artifacts/phase18-step07/sql-review/$sha/"*"/migrations.sql" ]] ||
  fail 'Reviewed SQL must be a private Step07 artifact at exact source SHA'
[[ -f "$review" && ! -L "$review" ]] || fail 'Reviewed SQL unavailable or symlink'
actual_sha=$(sha256sum "$review" | awk '{print $1}')
[[ "$actual_sha" == "$PHASE18_REVIEWED_SQL_SHA256" ]] ||
  fail 'Reviewed migration script checksum mismatch'
restore=$(realpath -- "${PHASE18_RESTORE_MANIFEST}")
[[ "$restore" == "$PWD/artifacts/phase18-step07/restore/$sha/"*"/manifest.tsv" ]] ||
  fail 'Restore evidence must match the current source SHA'
[[ -f "$restore" && ! -L "$restore" ]] || fail 'Restore evidence unavailable or symlink'
grep -Fxq $'restore\tPASS' "$restore" || fail 'Pre-migration restore did not PASS'
grep -Fxq $'source_sha\t'"$sha" "$restore" || fail 'Restore evidence SHA mismatch'
migrator=$(sed -n 's/^PHASE18_PROD_MIGRATOR_PASSWORD=//p' "$root/compose.env")
[[ "$migrator" =~ ^[a-f0-9]{64}$ ]] || fail 'Missing migrator-only password'
export PGSSLMODE=verify-full PGSSLROOTCERT="$root/ca/ca.pem" PGPASSWORD="$migrator"
common=(-h 127.0.0.1 -p 15434 -U argin_prod_migrator -d argintaxpayer_prod -X -v ON_ERROR_STOP=1)
proof=$(psql "${common[@]}" -Atc "SELECT current_database(), current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()),(SELECT rolsuper FROM pg_roles WHERE rolname=current_user);") ||
  fail 'Cannot confirm exact SSL/Production role and identity'
[[ "$proof" == 'argintaxpayer_prod|argin_prod_migrator|t|f' ]] ||
  fail 'Unexpected DB, role, TLS, or privileges'
tables=$(psql "${common[@]}" -Atc "SELECT count(*) FROM pg_tables WHERE schemaname='public';")
[[ "$tables" == 0 ]] ||
  fail 'Production DB already contains schema; refuse initial one-time migration'
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
  fail 'Production activation marker exists; migration disallowed'
# Independently regenerate the SQL from this source and compare exact bytes.
out="$PWD/artifacts/phase18-step07/migration-run/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
offline='Host=127.0.0.1;Port=1;Database=phase18_offline_sql_only;Username=no_live_access;Password=no_live_access;SSL Mode=Require;Timeout=1'
if ConnectionStrings__ArginTaxPayerDb="$offline" \
  dotnet ef migrations script --idempotent \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --configuration Release \
  --output "$out/regenerated.sql" > "$out/generate.log" 2>&1; then
  :
else
  fail 'Failed regenerating reviewed SQL'
fi
regen_sha=$(sha256sum "$out/regenerated.sql" | awk '{print $1}')
[[ "$regen_sha" == "$actual_sha" ]] ||
  fail 'Migration source changed relative to reviewed SQL; do not execute'
echo '[START] Approved one-time EF migration on dedicated Production DB ONLY'
connection="Host=127.0.0.1;Port=15434;Database=argintaxpayer_prod;Username=argin_prod_migrator;Password=$migrator;SSL Mode=VerifyFull;Root Certificate=$root/ca/ca.pem;Timeout=5"
if ConnectionStrings__ArginTaxPayerDb="$connection" \
  dotnet ef database update \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --configuration Release > "$out/migrate.log" 2>&1; then
  :
else
  fail "Migration failed; STOP and inspect local private log $out/migrate.log"
fi
history=$(psql "${common[@]}" -Atc 'SELECT count(*) FROM "__EFMigrationsHistory";')
[[ "$history" =~ ^[0-9]+$ && "$history" -gt 0 ]] ||
  fail 'EF history missing despite command success'
printf 'source_sha\t%s\nreviewed_sql_sha256\t%s\npre_migration_restore\tPASS\ninitial_migration\tPASS\nef_history_count\t%s\npost_migration_restore\tPENDING_SEPARATE_DRILL\nproduction-go-live\tBLOCKED\n' \
  "$sha" "$actual_sha" "$history" > "$out/manifest.tsv"
unset PGPASSWORD PGSSLMODE PGSSLROOTCERT migrator connection
echo "[PASS] Reviewed initial Production migration; EF history rows $history"
echo "[DONE] $out/manifest.tsv"
echo '[NEXT] Run a SECOND dedicated backup/restore after migration.'
echo '[BLOCKED] No production services or tax authority operations authorized.'
