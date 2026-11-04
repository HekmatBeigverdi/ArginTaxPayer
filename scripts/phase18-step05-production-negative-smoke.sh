#!/usr/bin/env bash
# Phase18 Step05: isolated Production-mode negative smoke test.
# No credentials, DB connection, services, DNS or real Tax Authority calls.
set -Eeuo pipefail
umask 077
die() { echo "[FAIL] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || die "Wrong branch"
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || die "Dirty tracked files"
for tool in dotnet curl git; do command -v "$tool" >/dev/null || die "Missing $tool"; done
# Dedicated high port to avoid existing API (15089), proposed Production API
# (15090), Nginx (80/443) and unrelated applications (8080/8081).
port=15991
url="http://127.0.0.1:$port"
if command -v ss >/dev/null; then
  if ss -ltnH | grep -Eq "(^|[.:])$port[[:space:]]"; then die "Temporary port in use"; fi
elif command -v lsof >/dev/null; then
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then die "Temporary port in use"; fi
fi
sha=$(git rev-parse HEAD)
stamp=$(date -u +%Y%m%dT%H%M%SZ)
out="$PWD/artifacts/phase18-step05-runtime/$sha/$stamp"
mkdir -p "$out/publish"; chmod 700 "$out"
echo "[INFO] Source SHA: $sha"
echo "[INFO] Private report: $out"
echo '[START] Isolated .NET Production publish'
dotnet publish src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj -c Release -o "$out/publish" > "$out/publish.log" 2>&1 || die "Publish failed; inspect private log"
pid=''
# Production requires syntactically valid database configuration even when
# operational routes are disabled. An empty private Unix socket directory makes
# readiness fail without contacting a TCP listener or using real credentials.
socket_dir=$(mktemp -d /tmp/argin-smoke-db.XXXXXX)
cleanup() {
  if [[ -n "$pid" ]]; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi
  rmdir "$socket_dir" 2>/dev/null || true
}
trap cleanup EXIT INT TERM
# Override inherited database configuration and explicitly prohibit operational APIs.
(
 cd "$out/publish"
 env -u ConnectionStrings__ArginTaxPayerDb -u ARGIN_IDENTITY_UI_ENABLED \
   ConnectionStrings__ArginTaxPayerDb="Host=$socket_dir;Database=negative_smoke;Username=negative_smoke;SSL Mode=Require;Timeout=1;Pooling=false" \
   ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production \
   ASPNETCORE_URLS="$url" \
   ArginTaxPayer__PublicBaseUrl=https://tax.argin.ir \
   ArginTaxPayer__EnableOperationalEndpoints=false \
   dotnet ArginTaxPayer.Api.dll
) > "$out/process.log" 2>&1 &
pid=$!
ready=0
for i in {1..60}; do
  if ! kill -0 "$pid" 2>/dev/null; then die "API exited during Production startup; inspect private log"; fi
  code=$(curl --noproxy '*' -sS -o /dev/null -w '%{http_code}' --max-time 2 "$url/health/live" 2>/dev/null || true)
  if [[ "$code" == 200 ]]; then ready=1; break; fi
  sleep 1
done
[[ "$ready" == 1 ]] || die "API did not become live; inspect private log"
printf 'check\tresult\n' > "$out/manifest.tsv"
assert_http() {
  local path="$1" expected="$2" name="$3"
  local code
  code=$(curl --noproxy '*' -sS -o /dev/null -w '%{http_code}' --max-time 5 "$url$path") || die "HTTP failed: $name"
  if [[ "$code" == "$expected" ]]; then
    printf '%s\tPASS (%s)\n' "$name" "$code" >> "$out/manifest.tsv"
    echo "[PASS] $name: HTTP $code"
  else
    printf '%s\tFAIL (%s, expected %s)\n' "$name" "$code" "$expected" >> "$out/manifest.tsv"
    die "$name returned $code instead of $expected"
  fi
}
assert_http /health/live 200 production_liveness
assert_http /health/ready 503 readiness_without_database
assert_http /health/dev-identity 404 development_diagnostic_disabled
assert_http /openapi/v1.json 404 production_openapi_disabled
assert_http /api/dev/identity/me 404 operational_identity_default_off
assert_http /api/dev/identity/authorization/me 404 operational_authorization_default_off
assert_http /api/dev/taxpayers/00000000-0000-0000-0000-000000000000/invoices/00000000-0000-0000-0000-000000000000/dispatch/lineage 404 operational_invoices_default_off
printf 'operational_auth_mfa_tenancy\tNOT_TESTED_ISOLATED_DB_REQUIRED\n' >> "$out/manifest.tsv"
printf 'production_go_live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] Read-only negative Production smoke PASS; no Production operational endpoints enabled."
echo "[INFO] More tests required before deployment: real isolated DB, MFA, access control, BFF cookies and external exposure."
