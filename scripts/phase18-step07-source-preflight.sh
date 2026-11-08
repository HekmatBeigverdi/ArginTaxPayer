#!/usr/bin/env bash
# Phase18 Step07 read-only macOS/Ubuntu source checks and Ubuntu host isolation preflight.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked files'
for t in node git; do command -v "$t" >/dev/null || fail "Missing: $t"; done
sha=$(git rev-parse HEAD)
platform=$(uname -s)
out="$PWD/artifacts/phase18-step07/preflight/$sha/$platform-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\ncheck\tresult\n' "$sha" > "$out/manifest.tsv"
run(){
  local label="$1"; shift
  echo "[START] $label"
  if "$@" > "$out/$label.log" 2>&1; then
    printf '%s\tPASS\n' "$label" >> "$out/manifest.tsv"
    echo "[PASS] $label"
  else
    printf '%s\tFAIL\n' "$label" >> "$out/manifest.tsv"
    echo "[FAIL] $label; see private log $out/$label.log"
    exit 1
  fi
}
run isolated-db-contract node --test apps/web/tests/phase18-step07-production-db-contract.test.mjs
for script in scripts/phase18-step07-*.sh ops/phase18-production-db/init/*.sh; do
  run "syntax-$(basename "$script" .sh)" bash -n "$script"
done
if command -v dotnet >/dev/null; then
  run guarded-migration-tests dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj \
    --configuration Release \
    --filter FullyQualifiedName~Phase18LegacyMigrationAdmissionTests
else
  fail 'dotnet 9 SDK required on both platforms'
fi
if [[ "$platform" == Linux ]]; then
  [[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[a-f0-9]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
    fail 'Provide full independently verified macOS SHA as PHASE18_EXPECTED_SHA'
  for t in sudo docker openssl ss psql pg_dump pg_restore; do
    command -v "$t" >/dev/null || fail "Missing: $t"
  done
  [[ -r /etc/os-release ]] || fail 'Missing Ubuntu version'
  . /etc/os-release
  [[ "${ID:-}" == ubuntu && "${VERSION_ID:-}" == 24.04 ]] || fail 'Ubuntu 24.04 required'
  sudo -n docker compose version >/dev/null 2>&1 ||
    fail 'sudo Docker Compose v2 inaccessible; run sudo -v separately'
  [[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
    fail 'Production service activation marker exists'
  for unit in argintaxpayer-api.service argintaxpayer-web.service; do
    if systemctl is-active --quiet "$unit"; then fail "$unit active; prohibit database provisioning"; fi
  done
  [[ ! -e "$PWD/artifacts/phase18-step07/production-private" ]] ||
    fail 'Dedicated production credentials already exist; use post-provision audit, do not reprovision'
  if sudo -n docker container inspect argintaxpayer-phase18-production-postgres >/dev/null 2>&1; then
    fail 'Dedicated production container already exists'
  fi
  if sudo -n docker volume inspect argintaxpayer-phase18-production_prod_pgdata >/dev/null 2>&1; then
    fail 'Dedicated production volume already exists'
  fi
  if ss -ltnH | awk '{print $4}' | grep -Eq ':15434$'; then
    fail 'Port 15434 already occupied'
  fi
  printf 'private-database-port\tFREE\ncontainer-and-volume\tABSENT\nsystemd\tINACTIVE\n' >> "$out/manifest.tsv"
else
  printf 'ubuntu-docker-isolation\tUBUNTU_ONLY\n' >> "$out/manifest.tsv"
fi
printf 'database-provisioning\tNOT_PERFORMED\nproduction-go-live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
