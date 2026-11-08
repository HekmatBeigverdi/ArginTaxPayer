#!/usr/bin/env bash
# Phase18 Step06 read-only installed unit acceptance; no service starts.
set -Eeuo pipefail
umask 077
[[ "$(uname -s)" == Linux ]] || { echo '[REFUSED] Ubuntu required' >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || exit 2
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step06/$sha/install-audit-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\ncheck\tresult\n' "$sha" > "$out/manifest.tsv"
assert(){
  local name="$1"; shift
  if "$@" > "$out/$name.log" 2>&1; then
    printf '%s\tPASS\n' "$name" >> "$out/manifest.tsv"
    echo "[PASS] $name"
  else
    printf '%s\tFAIL\n' "$name" >> "$out/manifest.tsv"
    echo "[FAIL] $name; inspect private $out/$name.log"
    exit 1
  fi
}
assert unit-syntax systemd-analyze verify \
  /etc/systemd/system/argintaxpayer-api.service \
  /etc/systemd/system/argintaxpayer-web.service
assert service-user id argintaxsvc
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] || {
  echo '[FAIL] Activation marker should be absent' >&2; exit 1;
}
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  src="ops/phase18-systemd/$unit"
  installed="/etc/systemd/system/$unit"
  assert "$unit-template-equal" cmp -s "$src" "$installed"
  enabled=$(systemctl is-enabled "$unit" 2>/dev/null || true)
  active=$(systemctl is-active "$unit" 2>/dev/null || true)
  [[ "$enabled" == disabled && "$active" != active ]] || {
    echo "[FAIL] Unsafe state for $unit: $enabled / $active" >&2; exit 1;
  }
  printf '%s\tDISABLED_AND_INACTIVE\n' "$unit" >> "$out/manifest.tsv"
done
for port in 15090 15091; do
  if ss -ltnH | awk '{print $4}' | grep -Eq "([.:])${port}$"; then
    echo "[FAIL] Reserved port $port is occupied" >&2; exit 1
  fi
  printf 'port-%s\tFREE\n' "$port" >> "$out/manifest.tsv"
done
printf 'nginx\tUNCHANGED_BY_STEP06\n' >> "$out/manifest.tsv"
printf 'postgresql\tUNCHANGED_BY_STEP06\n' >> "$out/manifest.tsv"
printf 'service-start\tBLOCKED_UNTIL_LATER_APPROVAL\n' >> "$out/manifest.tsv"
printf 'production-go-live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
