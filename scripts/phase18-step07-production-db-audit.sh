#!/usr/bin/env bash
# Step07 read-only DB identity, TLS, isolation and least-privilege audit.
# Does not create, migrate, alter, truncate or delete any DB.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked files'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[0-9a-f]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'Exact independently confirmed macOS SHA required'
for tool in psql docker sudo stat git; do command -v "$tool" >/dev/null || fail "Missing $tool"; done
root="$PWD/artifacts/phase18-step07/production-private"
[[ -f "$root/compose.env" && -f "$root/ca/ca.pem" ]] || fail 'Dedicated Production DB private config absent'
[[ "$(stat -c '%a' "$root/compose.env")" == 600 ]] || fail 'DB credentials must be 0600'
[[ "$(stat -c '%a' "$root")" == 700 ]] || fail 'Production private directory must be 0700'
if ! sudo -n docker inspect argintaxpayer-phase18-production-postgres >/dev/null 2>&1; then
  fail 'Dedicated Production DB container inaccessible; sudo -v before audit'
fi
binding=$(sudo -n docker port argintaxpayer-phase18-production-postgres 5432/tcp)
[[ "$binding" == '127.0.0.1:15434' ]] ||
  fail 'Production DB must expose only 127.0.0.1:15434'
migrator=$(sed -n 's/^PHASE18_PROD_MIGRATOR_PASSWORD=//p' "$root/compose.env")
app=$(sed -n 's/^PHASE18_PROD_APP_PASSWORD=//p' "$root/compose.env")
[[ "$migrator" =~ ^[0-9a-f]{64}$ && "$app" =~ ^[0-9a-f]{64}$ ]] ||
  fail 'Invalid dedicated DB credentials'
out="$PWD/artifacts/phase18-step07/audit/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\ncheck\tresult\n' "$sha" > "$out/manifest.tsv"
export PGSSLMODE=verify-full PGSSLROOTCERT="$root/ca/ca.pem"
export PGPASSWORD="$migrator"
common=(-h 127.0.0.1 -p 15434 -U argin_prod_migrator -d argintaxpayer_prod -X -v ON_ERROR_STOP=1)
proof=$(psql "${common[@]}" -Atc "SELECT current_database(),current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()),(SELECT rolsuper FROM pg_roles WHERE rolname=current_user),(SELECT rolcreatedb FROM pg_roles WHERE rolname=current_user),(SELECT rolcreaterole FROM pg_roles WHERE rolname=current_user);")
[[ "$proof" == 'argintaxpayer_prod|argin_prod_migrator|t|f|f|f' ]] ||
  fail 'Production DB, TLS or migration role permissions mismatch'
printf 'migrator-tls-and-role\tPASS\n' >> "$out/manifest.tsv"
limits=$(psql "${common[@]}" -Atc "SELECT rolsuper,rolcreatedb,rolcreaterole,rolreplication,has_schema_privilege('argin_prod_app','public','CREATE'),has_database_privilege('argin_prod_app','argintaxpayer_prod','CONNECT') FROM pg_roles WHERE rolname='argin_prod_app';")
[[ "$limits" == 'f|f|f|f|f|t' ]] || fail 'Runtime app role has excessive permissions'
printf 'runtime-least-privilege\tPASS\n' >> "$out/manifest.tsv"
export PGPASSWORD="$app"
app_proof=$(psql -h 127.0.0.1 -p 15434 -U argin_prod_app -d argintaxpayer_prod -X -At \
  -v ON_ERROR_STOP=1 -c "SELECT current_database(),current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid());")
[[ "$app_proof" == 'argintaxpayer_prod|argin_prod_app|t' ]] ||
  fail 'Runtime role cannot connect over VerifyFull or has wrong identity'
printf 'app-tls-login\tPASS\n' >> "$out/manifest.tsv"
# After EF migration, the app must be able to SELECT an owned table without
# becoming the schema owner or receiving migration/DDL privileges.
export PGPASSWORD="$migrator"
history=$(psql "${common[@]}" -Atc "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")
if [[ "$history" == t ]]; then
  export PGPASSWORD="$app"
  psql -h 127.0.0.1 -p 15434 -U argin_prod_app -d argintaxpayer_prod -X -At \
    -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM public.identity_users;' \
    > "$out/app-read-check.log" 2>&1 ||
    fail 'Runtime app role cannot read migrated identity_users with limited privileges'
  printf 'app-postmigration-select\tPASS\n' >> "$out/manifest.tsv"
else
  printf 'app-postmigration-select\tPENDING_INITIAL_MIGRATION\n' >> "$out/manifest.tsv"
fi
unset PGPASSWORD
# TLS-disabled connection must be explicitly rejected.
if PGSSLMODE=disable PGPASSWORD="$migrator" psql "${common[@]}" -Atc 'SELECT 1' \
    > "$out/non-tls-attempt.log" 2>&1; then
  fail 'SECURITY: PostgreSQL accepted plaintext TCP'
fi
printf 'plaintext-tcp-rejected\tPASS\n' >> "$out/manifest.tsv"
unset PGSSLMODE PGSSLROOTCERT migrator app
printf 'production-migration\tNOT_RUN_BY_AUDIT\n' >> "$out/manifest.tsv"
printf 'public-go-live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
