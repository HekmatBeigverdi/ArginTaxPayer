#!/usr/bin/env bash
# Phase18 Step06: READ-ONLY preflight; safe on Ubuntu or macOS.
# Does not install, start, enable or reload anything.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
refuse(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(git branch --show-current)" == "phase/18-production-deployment" ]] || refuse 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || refuse 'Dirty tracked files'
for t in node git; do command -v "$t" >/dev/null || refuse "Missing $t"; done
sha=$(git rev-parse HEAD)
platform=$(uname -s)
out="$PWD/artifacts/phase18-step06/$sha/$platform-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\ncheck\tstatus\n' "$sha" > "$out/manifest.tsv"
run(){
  local name="$1"; shift
  echo "[START] $name"
  if "$@" > "$out/$name.log" 2>&1; then
    printf '%s\tPASS\n' "$name" >> "$out/manifest.tsv"
    echo "[PASS] $name"
  else
    printf '%s\tFAIL\n' "$name" >> "$out/manifest.tsv"
    echo "[FAIL] $name; private log at $out/$name.log"
    exit 1
  fi
}
run unit-contract node --test apps/web/tests/phase18-step06-systemd-contract.test.mjs
if [[ "$platform" == "Linux" ]]; then
  [[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[0-9a-f]{40}$ ]] ||
    refuse 'Provide independently captured full macOS SHA as PHASE18_EXPECTED_SHA'
  [[ "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
    refuse 'macOS/Ubuntu SHA mismatch'
  command -v systemd-analyze >/dev/null || refuse 'systemd-analyze missing'
  command -v ss >/dev/null || refuse 'ss missing'
  # argintaxsvc does not inherit argindev NVM or ~/.dotnet. Check only
  # system-wide directories that systemd's /usr/bin/env can actually see.
  node_system=$(PATH=/usr/local/bin:/usr/bin:/bin command -v node || true)
  dotnet_system=$(PATH=/usr/local/bin:/usr/bin:/bin command -v dotnet || true)
  [[ -n "$node_system" && -x "$node_system" ]] ||
    refuse 'System-wide Node.js missing (argindev NVM is insufficient)'
  [[ -n "$dotnet_system" && -x "$dotnet_system" ]] ||
    refuse 'System-wide dotnet missing (user-local SDK is insufficient)'
  node_major=$(PATH=/usr/local/bin:/usr/bin:/bin "$node_system" -p 'Number(process.versions.node.split(".")[0])')
  [[ "$node_major" =~ ^[0-9]+$ && "$node_major" -ge 20 ]] ||
    refuse 'System-wide Node.js 20+ required for Next.js Production'
  PATH=/usr/local/bin:/usr/bin:/bin "$dotnet_system" --list-runtimes |
    grep -Eq '^Microsoft\\.AspNetCore\\.App 9\\.' ||
    refuse 'System-wide ASP.NET Core 9 runtime missing'
  run systemd-syntax systemd-analyze verify \
    ops/phase18-systemd/argintaxpayer-api.service \
    ops/phase18-systemd/argintaxpayer-web.service
  for port in 15090 15091; do
    if ss -ltnH | awk '{print $4}' | grep -Eq "([.:])${port}$"; then
      printf 'port-%s\tBLOCKED_IN_USE\n' "$port" >> "$out/manifest.tsv"
      refuse "Port $port already in use; never disrupt an existing process"
    fi
    printf 'port-%s\tFREE\n' "$port" >> "$out/manifest.tsv"
  done
  if [[ -e /etc/argintaxpayer/phase18-activation-approved ]]; then
    refuse 'Activation marker exists; manual review required'
  fi
  for unit in argintaxpayer-api.service argintaxpayer-web.service; do
    if systemctl is-active --quiet "$unit"; then
      refuse "Already active: $unit"
    fi
    printf '%s\tNOT_ACTIVE\n' "$unit" >> "$out/manifest.tsv"
  done
else
  printf 'systemd-syntax\tUBUNTU_ONLY\n' >> "$out/manifest.tsv"
fi
printf 'installation\tNOT_PERFORMED_BY_PREFLIGHT\n' >> "$out/manifest.tsv"
printf 'public-nginx\tUNCHANGED\n' >> "$out/manifest.tsv"
printf 'production-go-live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
