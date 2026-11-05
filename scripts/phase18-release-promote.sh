#!/usr/bin/env bash
# Switch to already staged immutable release. No migrations, provider calls or DNS changes.
set -Eeuo pipefail
umask 077
script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
core="$script_dir/release-core.sh"
[[ -f "$core" ]] || core="$script_dir/../ops/phase18-deploy/release-core.sh"
[[ -f "$core" ]] || { echo '[REFUSED] Release core is missing' >&2; exit 2; }
source "$core"
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux ]] || fail 'Ubuntu only'
if [[ "$(id -u)" == 0 ]]; then
  [[ "${PHASE18_ROOT_MODE:-}" == 1 ]] || fail 'Root mode is reserved for the installed deployment wrapper'
else
  command -v sudo >/dev/null || fail 'Missing sudo'
fi
[[ ( "$#" == 2 || "$#" == 3 ) && "$1" == --promote ]] || fail 'Usage: --promote <40-character-SHA> [--initial-disabled-ui]'
if [[ "$#" == 3 && "$3" != --initial-disabled-ui ]]; then fail 'Unknown mode'; fi
if [[ "$(id -u)" != 0 ]]; then sudo -v; fi
health_mode=--probe-local
if [[ "$#" == 3 ]]; then
  health_mode=--probe-local-disabled-ui
  # Explicitly ensure no production business/identity/SMS routes are exposed.
  phase18_as_root grep -Fxq 'ArginTaxPayer__EnableOperationalEndpoints=false' /etc/argintaxpayer/api.env || fail 'Operational API not disabled'
  phase18_as_root grep -Fxq 'Ghasedak__EnableLiveSending=false' /etc/argintaxpayer/api.env || fail 'Live SMS not disabled'
  phase18_as_root grep -Fxq 'SandboxInvoiceLegacy__Enabled=false' /etc/argintaxpayer/api.env || fail 'Legacy Sandbox send not disabled'
  phase18_as_root grep -Fxq 'ARGIN_PRODUCTION_UI_ENABLED=false' /etc/argintaxpayer/web.env || fail 'Production UI not disabled'
fi
target="$2"
phase18_validate_sha "$target" || fail 'Full lowercase 40-character Git SHA required'
[[ "${PHASE18_OWNER_APPROVED_RELEASE:-}" == YES ]] || fail 'Explicit release approval required'
phase18_as_root test -f /etc/argintaxpayer/phase18-activation-approved ||
  fail 'Production activation marker absent; no release activation approved'
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  phase18_as_root systemctl cat "$unit" >/dev/null || fail "Missing unit: $unit"
done
phase18_as_root test -f /etc/argintaxpayer/api.env &&
  phase18_as_root test -f /etc/argintaxpayer/web.env || fail 'Missing private config'
root=/opt/argintaxpayer
release="$root/releases/$target"
phase18_as_root test -d "$release" && phase18_as_root test ! -L "$release" || fail 'Release not staged'
for path in api/ArginTaxPayer.Api.dll web/.next/BUILD_ID web/node_modules/next/dist/bin/next; do
  phase18_as_root test -f "$release/$path" || fail "Incomplete staged release: missing $path"
  phase18_as_runtime_user test -r "$release/$path" || fail "Runtime service cannot read $path"
done
phase18_as_root grep -Fxq "sha=$target" "$release/build-info.txt" || fail 'Release SHA mismatch'
previous=''
if phase18_as_root test -L "$root/current"; then
  previous=$(phase18_as_root readlink -f "$root/current")
  case "$previous" in "$root"/releases/*) ;; *) fail 'Unexpected active symlink target';; esac
elif phase18_as_root test -e "$root/current"; then
  fail 'Existing current path is not a symlink'
fi
if [[ "$previous" == "$release" ]]; then
  if bash "$script_dir/phase18-step06-local-health.sh" "$health_mode"; then
    echo "[PASS] Release $target is already active and healthy."
    exit 0
  fi
  fail 'Requested release is already active but failed health; no symlink change made'
fi
tmp="$root/.current-next-$$"
cleanup(){ phase18_as_root rm -f -- "$tmp"; }
trap cleanup EXIT
phase18_as_root ln -s "$release" "$tmp"
phase18_as_root mv -Tf "$tmp" "$root/current"
startup_ok=0
if phase18_as_root systemctl restart argintaxpayer-api.service &&
   phase18_as_root systemctl restart argintaxpayer-web.service; then
  for attempt in $(seq 1 20); do
    if bash "$(dirname "$0")/phase18-step06-local-health.sh" "$health_mode" >/dev/null 2>&1; then
      startup_ok=1
      break
    fi
    sleep 2
  done
else
  echo '[DIAGNOSTIC] A systemd restart command failed.' >&2
fi
if [[ "$startup_ok" != 1 ]]; then
  # Re-run the health probe with its safe, fixed failure labels visible.
  # Do NOT print environment files, connection strings or full exception logs.
  echo '[DIAGNOSTIC] Final localhost health checks:' >&2
  bash "$(dirname "$0")/phase18-step06-local-health.sh" "$health_mode" >&2 || true
  echo '[DIAGNOSTIC] systemd unit states before rollback:' >&2
  for unit in argintaxpayer-api.service argintaxpayer-web.service; do
    systemctl show "$unit" --no-pager \
      --property=ActiveState,SubState,Result,ExecMainCode,ExecMainStatus,ConditionResult \
      | sed "s/^/[$unit] /" >&2
  done
  echo '[DIAGNOSTIC] For exception details inspect journalctl on the VPS; never paste secrets.' >&2
fi
if [[ "$startup_ok" == 1 ]]; then
  echo "[PASS] Release $target promoted and loopback health PASS."
  [[ -z "$previous" ]] || echo '[INFO] Previous version retained for manual rollback.'
  exit 0
fi
echo '[FAIL] New release health failed. Rolling back application only.' >&2
if [[ -n "$previous" ]]; then
  phase18_as_root ln -s "$previous" "$tmp"
  phase18_as_root mv -Tf "$tmp" "$root/current"
  phase18_as_root systemctl restart argintaxpayer-api.service || true
  phase18_as_root systemctl restart argintaxpayer-web.service || true
  bash "$script_dir/phase18-step06-local-health.sh" --probe-local ||
    echo '[CRITICAL] Previous version also failed health; manual incident required.' >&2
else
  phase18_as_root systemctl stop argintaxpayer-web.service argintaxpayer-api.service || true
  phase18_as_root rm -f "$root/current"
  echo '[CRITICAL] Initial release failed; application services stopped.' >&2
fi
exit 1
