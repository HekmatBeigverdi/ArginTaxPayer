#!/usr/bin/env bash
# Step07: generate reviewable offline SQL from the exact tracked migration set.
# No Production/QA database access, no credentials or SQL execution.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked files'
for tool in dotnet sha256sum git; do command -v "$tool" >/dev/null || fail "Missing $tool"; done
dotnet ef --version >/dev/null 2>&1 || fail 'dotnet-ef required'
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step07/sql-review/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
# Deliberately fake offline endpoint; generation must not rely on real DB.
offline='Host=127.0.0.1;Port=1;Database=phase18_offline_sql_only;Username=no_live_access;Password=no_live_access;SSL Mode=Require;Timeout=1'
echo '[START] Generating full EF Core idempotent SQL from source only'
if ConnectionStrings__ArginTaxPayerDb="$offline" \
   dotnet ef migrations script --idempotent \
   --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
   --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
   --configuration Release \
   --output "$out/migrations.sql" > "$out/generation.log" 2>&1; then
  [[ -s "$out/migrations.sql" ]] || fail 'Empty migration SQL'
else
  fail 'Offline SQL generation failed; inspect private log'
fi
sha256sum "$out/migrations.sql" > "$out/migrations.sha256"
# Flag potential destructive patterns; a match is not a permit to execute.
if grep -Ein '\b(DROP[[:space:]]+(TABLE|COLUMN|SCHEMA|INDEX|CONSTRAINT)|TRUNCATE|DELETE[[:space:]]+FROM|ALTER[[:space:]]+TABLE)\b' \
  "$out/migrations.sql" > "$out/ddl-review-hits.txt"; then
  review='MANUAL_REVIEW_REQUIRED'
else
  review='MANUAL_REVIEW_REQUIRED_EVEN_IF_NO_HITS'
fi
printf 'source_sha\t%s\nfull_migration_sql\tGENERATED_OFFLINE\nreview\t%s\nmigration_execute\tBLOCKED_UNTIL_EXPLICIT_APPROVAL\n' \
 "$sha" "$review" > "$out/manifest.tsv"
printf '[DONE] SQL review: %s\n' "$out"
echo '[BLOCKED] No database migration executed; review SQL and hashes privately.'
