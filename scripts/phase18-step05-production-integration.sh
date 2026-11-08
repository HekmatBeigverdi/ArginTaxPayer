#!/usr/bin/env bash
# Phase18 isolated Production integration runner (never deploys).
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || exit 2
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || exit 2
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step05-integration/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
echo "check result" > "$out/manifest.txt"
run() {
  local label="$1"; shift
  echo "[START] $label"
  if "$@" > "$out/$label.log" 2>&1; then
    echo "$label PASS" >> "$out/manifest.txt"
    echo "[PASS] $label"
  else
    echo "$label FAIL" >> "$out/manifest.txt"
    echo "[FAIL] $label; see $out/$label.log"
    exit 1
  fi
}
run production-api-isolation dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --filter FullyQualifiedName~Phase18ProductionIsolationTests
run web-production-boundaries bash -c 'cd apps/web && node --test tests/phase18-production-boundaries.test.mjs'
run web-identity-regression bash -c 'cd apps/web && node --test tests/identity-ui.test.mjs'
echo "production_mfa_tenancy PENDING_DEDICATED_DB" >> "$out/manifest.txt"
echo "production_go_live BLOCKED" >> "$out/manifest.txt"
echo "[DONE] Source SHA $sha"
echo "[DONE] Local report $out/manifest.txt"
