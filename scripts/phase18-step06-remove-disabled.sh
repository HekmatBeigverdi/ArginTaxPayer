#!/usr/bin/env bash
# Phase18 Step06: remove ONLY our never-activated units, leaving data, users and
# existing services untouched. Explicit operator request, no automatic rollback.
set -Eeuo pipefail
umask 077
refuse(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "${1:-}" == "--remove-disabled-units" && "$#" == 1 ]] ||
  refuse 'Only explicit --remove-disabled-units is accepted'
[[ "$(uname -s)" == Linux && "$(id -u)" == 0 ]] || refuse 'sudo on Ubuntu required'
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
  refuse 'Approval marker exists. Escalate to controlled release rollback, not Step06 removal'
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  destination="/etc/systemd/system/$unit"
  [[ -f "$destination" && ! -L "$destination" ]] || refuse "Missing or symlinked $unit"
  cmp -s "ops/phase18-systemd/$unit" "$destination" ||
    refuse "Installed $unit differs from the reviewed Step06 template"
  [[ "$(systemctl is-enabled "$unit" 2>/dev/null || true)" == disabled ]] ||
    refuse "$unit is enabled; refuse uninstallation"
  [[ "$(systemctl is-active "$unit" 2>/dev/null || true)" != active ]] ||
    refuse "$unit is active; refuse uninstallation"
done
# This operation removes only the exact two owned files; it never stops, reloads
# or resets PostgreSQL, Nginx, SSH, system users or unrelated services.
rm -- /etc/systemd/system/argintaxpayer-api.service \
      /etc/systemd/system/argintaxpayer-web.service
systemctl daemon-reload
echo '[PASS] Two inactive disabled Step06 unit files removed.'
echo '[INFO] Dedicated service account/directories intentionally retained for owner review.'
