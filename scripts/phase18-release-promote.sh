#!/usr/bin/env bash
# Verify release schema, then switch to the staged immutable release. No provider calls.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot only'
[[ ( "$#" == 2 || "$#" == 3 ) && "$1" == --promote ]] || fail 'Usage: --promote <40-character-SHA> [--initial-disabled-ui]'
if [[ "$#" == 3 && "$3" != --initial-disabled-ui ]]; then fail 'Unknown mode'; fi
sudo -v
health_mode=--probe-local
if [[ "$#" == 3 ]]; then
  health_mode=--probe-local-disabled-ui
  # Explicitly ensure no production business/identity/SMS routes are exposed.
  sudo grep -Fxq 'ArginTaxPayer__EnableOperationalEndpoints=false' /etc/argintaxpayer/api.env || fail 'Operational API not disabled'
  sudo grep -Fxq 'Ghasedak__EnableLiveSending=false' /etc/argintaxpayer/api.env || fail 'Live SMS not disabled'
  sudo grep -Fxq 'SandboxInvoiceLegacy__Enabled=false' /etc/argintaxpayer/api.env || fail 'Legacy Sandbox send not disabled'
  sudo grep -Fxq 'ARGIN_PRODUCTION_UI_ENABLED=false' /etc/argintaxpayer/web.env || fail 'Production UI not disabled'
fi
target="$2"
[[ "$target" =~ ^[a-f0-9]{40}$ ]] || fail 'Full Git SHA required'
[[ "${PHASE18_OWNER_APPROVED_RELEASE:-}" == YES ]] || fail 'Explicit release approval required'
sudo test -f /etc/argintaxpayer/phase18-activation-approved ||
  fail 'Production activation marker absent; no release activation approved'
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  systemctl cat "$unit" >/dev/null || fail "Missing unit: $unit"
done
sudo test -f /etc/argintaxpayer/api.env &&
  sudo test -f /etc/argintaxpayer/web.env || fail 'Missing private config'
root=/opt/argintaxpayer
release="$root/releases/$target"
sudo test -d "$release" && sudo test ! -L "$release" || fail 'Release not staged'
for path in api/ArginTaxPayer.Api.dll web/.next/BUILD_ID web/node_modules/next/dist/bin/next; do
  sudo test -f "$release/$path" || fail "Incomplete staged release: missing $path"
  sudo -u argintaxsvc test -r "$release/$path" || fail "Runtime service cannot read $path"
done
sudo grep -Fxq "sha=$target" "$release/build-info.txt" || fail 'Release SHA mismatch'
# Root helper holds one lock across schema migration, symlink activation, health and rollback.
if [[ "$#" == 3 ]]; then
  sudo /usr/local/sbin/argin-taxpayer-release --promote-staged "$target" --initial-disabled-ui
else
  sudo /usr/local/sbin/argin-taxpayer-release --promote-staged "$target"
fi
