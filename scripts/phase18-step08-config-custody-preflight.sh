#!/usr/bin/env bash
# Phase18 Step08: read-only production configuration and custody preflight.
# Never prints secret contents, writes system configuration or starts services.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
fail(){ printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked source'
sha=$(git rev-parse HEAD)
platform=$(uname -s)
for tool in node git; do command -v "$tool" >/dev/null || fail "Missing $tool"; done
out="$PWD/artifacts/phase18-step08/$sha/${platform}-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\nplatform\t%s\n' "$sha" "$platform" > "$out/manifest.tsv"
echo '[START] Step08 configuration contract checks'
if node --test apps/web/tests/phase18-step08-config-custody.test.mjs > "$out/config-contract.log" 2>&1; then
  printf 'config-contract\tPASS\n' >> "$out/manifest.tsv"
  echo '[PASS] config-contract'
else
  printf 'config-contract\tFAIL\n' >> "$out/manifest.tsv"
  fail "Config contract test failed; see $out/config-contract.log"
fi
bash -n scripts/phase18-step08-config-custody-preflight.sh
bash -n scripts/phase18-step08-stage-private-config.sh
bash -n scripts/phase18-step08-private-config-audit.sh
printf 'bash-syntax\tPASS\n' >> "$out/manifest.tsv"
if command -v dotnet >/dev/null 2>&1; then
  echo '[START] cryptographic-custody-unit-tests'
  if dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --configuration Release --filter 'FullyQualifiedName~Phase09DatabaseCustodyEncryptionTests|FullyQualifiedName~Phase09SigningProtectionConfigurationTests|FullyQualifiedName~IdentityMfa' > "$out/crypto-unit-tests.log" 2>&1; then
    printf 'crypto-unit-tests\tPASS\n' >> "$out/manifest.tsv"
    echo '[PASS] crypto-unit-tests'
  else
    printf 'crypto-unit-tests\tFAIL\n' >> "$out/manifest.tsv"
    fail "Crypto unit tests failed; see $out/crypto-unit-tests.log"
  fi
else
  fail 'dotnet SDK needed for crypto-custody tests'
fi
if [[ "$platform" == Linux ]]; then
  [[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[a-f0-9]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
    fail 'Independent macOS/Ubuntu exact SHA required'
  [[ "$(id -u)" != 0 ]] || fail 'Run under non-root operator'
  for unit in argintaxpayer-api.service argintaxpayer-web.service; do
    if systemctl is-active --quiet "$unit"; then fail "Unexpected active service: $unit"; fi
  done
  [[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] || fail 'Activation marker exists'
  [[ -f artifacts/phase18-step07/production-private/compose.env ]] ||
    fail 'Dedicated Step07 PostgreSQL credentials missing'
  [[ -f artifacts/phase18-step07/production-private/ca/ca.pem ]] ||
    fail 'Dedicated PostgreSQL CA missing'
  if [[ -e /etc/argintaxpayer/api.env ]]; then
    sudo -n test "$(sudo -n stat -c %a /etc/argintaxpayer/api.env)" = 600 ||
      fail 'api.env must be mode 0600'
    printf 'api-env\tPRESENT_PRIVATE\n' >> "$out/manifest.tsv"
  else
    printf 'api-env\tNOT_YET_STAGED\n' >> "$out/manifest.tsv"
  fi
  printf 'api-web-services\tINACTIVE\n' >> "$out/manifest.tsv"
else
  printf 'api-env\tUBUNTU_ONLY\n' >> "$out/manifest.tsv"
fi
printf 'key-restart-recovery\tNOT_YET_TESTED\nprovider-credentials\tNOT_YET_VERIFIED\nservice-activation\tBLOCKED\nproduction-go-live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
