#!/usr/bin/env bash
# Phase18 Step05 final scoped-data + real Next.js BFF acceptance.
# Source is never deployed, no migrations, no live fiscal services.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
fail() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked files'
for cmd in git node dotnet; do command -v "$cmd" >/dev/null || fail "Missing $cmd"; done
sha=$(git rev-parse HEAD)
platform=$(uname -s)
out="$PWD/artifacts/phase18-step05-final/$sha/$platform-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'check\tresult\n' > "$out/manifest.tsv"
run() {
 local name="$1"; shift
 echo "[START] $name"
 if "$@" > "$out/$name.log" 2>&1; then
   printf '%s\tPASS\n' "$name" >> "$out/manifest.tsv"
   echo "[PASS] $name"
 else
   printf '%s\tFAIL\n' "$name" >> "$out/manifest.tsv"
   echo "[FAIL] $name; inspect private log $out/$name.log"
   exit 1
 fi
}
# This is a real Next.js production start with a harmless fake identity backend.
# It refuses port 15090 if already occupied and never starts an actual API.
[[ -f apps/web/.next/BUILD_ID ]] || fail 'Run phase18-step05-build-release.sh first to build Next.js Production'
run browser-bff-runtime node scripts/phase18-step05-production-bff-runtime.mjs
run web-boundary-tests bash -c 'cd apps/web && node --test tests/phase18-production-boundaries.test.mjs'
run dotnet-tenant-compile dotnet build tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --no-restore

if [[ "$platform" == Linux ]]; then
  root="$PWD/artifacts/phase18-step05-db/isolated"
  [[ -f "$root/compose.env" && -f "$root/ca/ca.pem" ]] ||
    fail 'Dedicated disposable PostgreSQL configuration missing on Ubuntu'
  [[ "$(stat -c '%a' "$root/compose.env")" == 600 ]] ||
    fail 'QA DB secrets permission is not 600'
  password=$(sed -n 's/^PHASE18_DB_PASSWORD=//p' "$root/compose.env")
  [[ "$password" =~ ^[0-9a-f]{64}$ ]] || fail 'Invalid QA DB password'
  connection="Host=127.0.0.1;Port=15433;Database=argin_phase18_acceptance;Username=phase18_acceptance;Password=$password;SSL Mode=VerifyFull;Root Certificate=$root/ca/ca.pem;Timeout=5"
  unset password
  run tenant-isolation-db env \
    -u RUN_ARGINTAXPAYER_PG_INTEGRATION \
    -u ConnectionStrings__ArginTaxPayerDb \
    RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1 \
    PHASE18_DISPOSABLE_DB_CONNECTION="$connection" \
    dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --no-restore \
      --filter FullyQualifiedName~Phase18TenantIsolationDatabaseTests
  unset connection
else
  printf 'tenant-isolation-db\tUBUNTU_ONLY\n' >> "$out/manifest.tsv"
fi
printf 'public-http-auth\tNOT_TESTED_NO_LIVE_SITE\n' >> "$out/manifest.tsv"
printf 'production-go-live\tBLOCKED_PENDING_SUBSEQUENT_PHASE18_STEPS\n' >> "$out/manifest.tsv"
printf '[DONE] Private manifest: %s\n' "$out/manifest.tsv"
cat "$out/manifest.tsv"
