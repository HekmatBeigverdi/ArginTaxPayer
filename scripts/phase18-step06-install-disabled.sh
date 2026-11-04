#!/usr/bin/env bash
# Phase18 Step06 -- privileged INSTALL-DISABLED only.
# Does NOT enable/start/restart any service; no Nginx/DB/DNS changes.
set -Eeuo pipefail
umask 077
refuse() { echo "[REFUSED] $*" >&2; exit 2; }
[[ "${1:-}" == "--install-disabled" && "$#" == 1 ]] ||
  refuse 'Only explicit --install-disabled is supported'
[[ "$(uname -s)" == Linux ]] || refuse 'Ubuntu/Linux required'
[[ "$(id -u)" == 0 ]] || refuse 'Run with sudo only after nonroot preflight'
cd "$(dirname "${BASH_SOURCE[0]}")/.."
base="$PWD"
[[ -r /etc/os-release ]] || refuse 'Missing OS release'
. /etc/os-release
[[ "${ID:-}" == ubuntu && "${VERSION_ID:-}" == 24.04 ]] || refuse 'Ubuntu 24.04 required'
[[ "$(git -c safe.directory="$base" branch --show-current)" == phase/18-production-deployment ]] ||
  refuse 'Wrong branch'
[[ -z "$(git -c safe.directory="$base" status --porcelain --untracked-files=no)" ]] ||
  refuse 'Dirty tracked tree'
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[0-9a-f]{40}$ ]] ||
  refuse 'Full independent macOS SHA required via PHASE18_EXPECTED_SHA'
[[ "$(git -c safe.directory="$base" rev-parse HEAD)" == "$PHASE18_EXPECTED_SHA" ]] ||
  refuse 'Refusing installer for unverified source SHA'
command -v systemd-analyze >/dev/null || refuse 'systemd-analyze missing'
command -v systemctl >/dev/null || refuse 'systemctl missing'
command -v ss >/dev/null || refuse 'ss missing'
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
  refuse 'Activation marker already present; do not install'
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  [[ ! -e "/etc/systemd/system/$unit" ]] ||
    refuse "Target unit exists: $unit; refusing overwrite"
  if systemctl is-active --quiet "$unit"; then
    refuse "Service active: $unit"
  fi
done
for port in 15090 15091; do
  if ss -ltnH | awk '{print $4}' | grep -Eq "([.:])${port}$"; then
    refuse "Port $port already occupied"
  fi
done
systemd-analyze verify \
  ops/phase18-systemd/argintaxpayer-api.service \
  ops/phase18-systemd/argintaxpayer-web.service ||
  refuse 'Unit syntax verification failed'
# Refuse ambiguous pre-existing identity; Step06 is a fresh isolated account.
if getent passwd argintaxsvc >/dev/null || getent group argintaxsvc >/dev/null; then
  refuse 'argintaxsvc already exists; inspect manually before making changes'
fi
# All validation precedes the first privileged modification.
useradd --system --user-group --home-dir /var/lib/argintaxpayer \
  --shell /usr/sbin/nologin argintaxsvc
install -d -m 0755 -o root -g root /opt/argintaxpayer
install -d -m 0755 -o root -g root /opt/argintaxpayer/releases
install -d -m 0750 -o argintaxsvc -g argintaxsvc /var/lib/argintaxpayer
install -d -m 0750 -o argintaxsvc -g argintaxsvc /var/cache/argintaxpayer-web
install -d -m 0750 -o root -g argintaxsvc /etc/argintaxpayer
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  install -m 0644 -o root -g root "ops/phase18-systemd/$unit" "/etc/systemd/system/$unit"
done
systemctl daemon-reload
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  [[ "$(systemctl is-enabled "$unit" 2>/dev/null || true)" == disabled ]] ||
    refuse "$unit not disabled after installation"
  [[ "$(systemctl is-active "$unit" 2>/dev/null || true)" != active ]] ||
    refuse "Unexpected active service $unit"
done
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
  refuse 'Unexpected activation marker'
echo '[PASS] Dedicated unprivileged user and two disabled, gated systemd unit files installed.'
echo '[PASS] No process started; ports remain local and unbound; public site unchanged.'
echo '[NOTE] Do not create the activation marker or start/enable units before later Step07/08 approval.'
