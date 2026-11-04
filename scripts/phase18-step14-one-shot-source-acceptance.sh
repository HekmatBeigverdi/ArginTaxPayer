#!/usr/bin/env bash
# Single offline source acceptance. NEVER touches production DB, keys, authority
# APIs, systemd, Nginx or deployment activation markers.
set -Eeuo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] ||
  { echo "[FAIL] Wrong branch"; exit 2; }
[[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
  { echo "[FAIL] Dirty tracked source"; exit 2; }
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step14/offline/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"
echo "[START] Phase18 complete offline source acceptance for $sha"
export RUN_ARGINTAXPAYER_PG_INTEGRATION=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NEXT_TELEMETRY_DISABLED=1
node --test \
  apps/web/tests/phase18-step09-dual-authority-audit.test.mjs \
  apps/web/tests/phase18-step10-fiscal-environment-onboarding.test.mjs \
  apps/web/tests/phase18-step13-inquiry-isolation.test.mjs \
  apps/web/tests/phase18-step14-operation-authority.test.mjs \
  apps/web/tests/phase18-step14-fiscal-key-ui-isolation.test.mjs \
  > "$out/route-tests.log" 2>&1
echo '[PASS] Authority source isolation checks'
dotnet restore ArginTaxPayer.sln > "$out/dotnet-restore.log" 2>&1
dotnet build ArginTaxPayer.sln --configuration Release --no-restore > "$out/dotnet-build.log" 2>&1
dotnet test ArginTaxPayer.sln --configuration Release --no-build > "$out/dotnet-tests.log" 2>&1
echo '[PASS] .NET Release build and offline tests'
(
  cd apps/web
  npm ci --no-audit --no-fund
  npm test
  npm run typecheck
  npm run build
) > "$out/web.log" 2>&1
echo '[PASS] Web test, typecheck and build'
printf 'source_sha\t%s\noffline_source_tests\tPASS\ndotnet_build_tests\tPASS\nweb_tests_typecheck_build\tPASS\nproduction_authority_integration\tNOT_TESTED\nproduction_invoice_dispatch\tBLOCKED\nproduction_go_live\tBLOCKED\n' "$sha" > "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
echo '[BLOCKED] One-shot source PASS is not live dual-authority acceptance or deployment permission.'
