#!/usr/bin/env bash
# Ubuntu-only Step05 QA. No production deploy, no public listener.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
fail() { echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux ]] || fail 'Ubuntu required'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked files'
for port in 15090 15994; do
  ! ss -ltnH | grep -Eq "[:.]$port[[:space:]]" || fail "Port $port in use"
done
root="$PWD/artifacts/phase18-step05-db/isolated"
[[ -f "$root/compose.env" && -f "$root/ca/ca.pem" ]] || fail 'Dedicated QA database missing'
[[ "$(stat -c '%a' "$root/compose.env")" == 600 ]] || fail 'Private env permissions incorrect'
dbpass=$(sed -n 's/^PHASE18_DB_PASSWORD=//p' "$root/compose.env")
[[ "$dbpass" =~ ^[0-9a-f]{64}$ ]] || fail 'Invalid QA-only password'
connection="Host=127.0.0.1;Port=15433;Database=argin_phase18_acceptance;Username=phase18_acceptance;Password=$dbpass;SSL Mode=VerifyFull;Root Certificate=$root/ca/ca.pem;Timeout=5"
export PGPASSWORD="$dbpass" PGSSLMODE=verify-full PGSSLROOTCERT="$root/ca/ca.pem"
unset dbpass
psql_args=(-h 127.0.0.1 -p 15433 -U phase18_acceptance -d argin_phase18_acceptance -X -At)
proof=$(psql "${psql_args[@]}" -c "SELECT current_database(),current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid());")
[[ "$proof" == 'argin_phase18_acceptance|phase18_acceptance|t' ]] || fail 'QA database identity or TLS invalid'
email=$(psql "${psql_args[@]}" -c "SELECT u.normalized_email FROM taxpayer_profiles p JOIN identity_users u ON u.id=p.customer_user_id WHERE p.display_name='Phase18 isolated taxpayer A' AND u.normalized_email LIKE 'PHASE18-%@EXAMPLE.INVALID' ORDER BY p.created_at_utc DESC LIMIT 1")
[[ "$email" == PHASE18-* && "$email" == *@EXAMPLE.INVALID ]] || fail 'Synthetic tenant fixture missing'
unset PGPASSWORD PGSSLMODE PGSSLROOTCERT
[[ -f apps/web/.next/BUILD_ID ]] || fail 'Next.js Production build required'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[0-9a-f]{40}$ ]] ||
  fail 'Provide full macOS SHA as PHASE18_EXPECTED_SHA (same-source parity gate)'
[[ "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'macOS and Ubuntu SHA mismatch; do not run acceptance'
out="$PWD/artifacts/phase18-step05-e2e/reports/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\ncheck\tresult\n' "$sha" > "$out/manifest.tsv"
run() {
  local label="$1"; shift
  echo "[START] $label"
  if "$@" > "$out/$label.log" 2>&1; then
    printf '%s\tPASS\n' "$label" >> "$out/manifest.tsv"
    echo "[PASS] $label"
  else
    printf '%s\tFAIL\n' "$label" >> "$out/manifest.tsv"
    echo "[FAIL] $label (inspect private report)"
    exit 1
  fi
}
run real-production-http-tenant env \
  RUN_PHASE18_PRODUCTION_HTTP_ACCEPTANCE=1 \
  PHASE18_DISPOSABLE_DB_CONNECTION="$connection" \
  dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj \
    -c Release --filter FullyQualifiedName~Phase18ProductionHttpTenantAcceptanceTests
mkdir -p artifacts/phase18-step05-e2e/api
run qa-api-publish dotnet publish src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  -c Release -o artifacts/phase18-step05-e2e/api
run real-production-api-bff env \
  PHASE18_QA_USER_EMAIL="$email" \
  PHASE18_QA_USER_PASSWORD='Phase18-Test-Tenancy-Only-Password!' \
  PHASE18_DISPOSABLE_DB_CONNECTION="$connection" \
  node scripts/phase18-step05-real-api-bff.mjs
unset connection email
printf 'production-https-browser\tDEFERRED_TO_SYSTEMD_NGINX\n' >> "$out/manifest.tsv"
printf 'invoice-http-tenant-authorization\tPASS_POPULATED_SYNTHETIC_DRAFT_HTTP_IDOR\n' >> "$out/manifest.tsv"
printf 'production-go-live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
