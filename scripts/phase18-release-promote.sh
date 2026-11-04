#!/usr/bin/env bash
# Switch to already staged immutable release. No migrations, provider calls or DNS changes.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot only'
[[ ( "$#" == 2 || "$#" == 3 ) && "$1" == --promote ]] || fail 'Usage: --promote <40-character-SHA> [--initial-disabled-ui]'
if [[ "$#" == 3 && "$3" != --initial-disabled-ui ]]; then fail 'Unknown mode'; fi
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
[[ -f /etc/argintaxpayer/phase18-activation-approved ]] ||
  fail 'Production activation marker absent; no release activation approved'
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  systemctl cat "$unit" >/dev/null || fail "Missing unit: $unit"
done
[[ -f /etc/argintaxpayer/api.env && -f /etc/argintaxpayer/web.env ]] || fail 'Missing private config'
root=/opt/argintaxpayer
release="$root/releases/$target"
[[ -d "$release" && ! -L "$release" ]] || fail 'Release not staged'
[[ -f "$release/api/ArginTaxPayer.Api.dll" &&
   -f "$release/web/.next/BUILD_ID" &&
   -f "$release/web/node_modules/next/dist/bin/next" ]] || fail 'Incomplete staged release'
grep -Fxq "sha=$target" "$release/build-info.txt" || fail 'Release SHA mismatch'
previous=''
if [[ -L "$root/current" ]]; then
  previous=$(readlink -f "$root/current")
  case "$previous" in "$root"/releases/*) ;; *) fail 'Unexpected active symlink target';; esac
elif [[ -e "$root/current" ]]; then
  fail 'Existing current path is not a symlink'
fi
sudo -v
tmp="$root/.current-next-$$"
cleanup(){ sudo rm -f -- "$tmp"; }
trap cleanup EXIT
sudo ln -s "$release" "$tmp"
sudo mv -Tf "$tmp" "$root/current"
if sudo systemctl restart argintaxpayer-api.service &&
   sudo systemctl restart argintaxpayer-web.service &&
   bash "$(dirname "$0")/phase18-step06-local-health.sh" "$health_mode"; then
  echo "[PASS] Release $target promoted and loopback health PASS."
  [[ -z "$previous" ]] || echo '[INFO] Previous version retained for manual rollback.'
  exit 0
fi
echo '[FAIL] New release health failed. Rolling back application only.' >&2
if [[ -n "$previous" ]]; then
  sudo ln -s "$previous" "$tmp"
  sudo mv -Tf "$tmp" "$root/current"
  sudo systemctl restart argintaxpayer-api.service || true
  sudo systemctl restart argintaxpayer-web.service || true
  bash "$(dirname "$0")/phase18-step06-local-health.sh" --probe-local ||
    echo '[CRITICAL] Previous version also failed health; manual incident required.' >&2
else
  sudo systemctl stop argintaxpayer-web.service argintaxpayer-api.service || true
  sudo rm -f "$root/current"
  echo '[CRITICAL] Initial release failed; application services stopped.' >&2
fi
exit 1
