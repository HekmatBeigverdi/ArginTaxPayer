#!/usr/bin/env bash
# Read-only Ubuntu operator preflight. Never starts/stops application services.
set -Eeuo pipefail
fail() { printf '[FAIL] %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" == 0 ]] || fail 'Run with sudo as the Ubuntu operator.'
for tool in /usr/bin/pg_dump /usr/bin/pg_restore /usr/bin/systemctl /usr/bin/systemd-run /usr/bin/sudo; do
  [[ -x "$tool" ]] || fail "Missing executable: $tool"
done
[[ ! -e /etc/argintaxpayer/backup-db.env && ! -L /etc/argintaxpayer/backup-db.env ]] || fail 'Active restored database selection exists; migration target review required before deploy.'
[[ "$(systemctl show argintaxpayer-api.service -p EnvironmentFiles --value)" == *'/etc/argintaxpayer/backup-db.env'* ]] || fail 'Install API backup database-selection drop-in first.'
helper=/usr/local/sbin/argin-taxpayer-backup-control
[[ -x "$helper" && ! -L "$helper" ]] || fail 'Install the reviewed backup helper first.'
[[ "$(stat -c '%U:%G:%a' "$helper")" == root:root:755 ]] || fail 'Backup helper must be root:root 0755.'
visudo -cf /etc/sudoers >/dev/null
python3 -I - <<'PY'
import os, pathlib, re, stat
root=pathlib.Path('/etc/argintaxpayer')
key=root/'database-backup.key'
if key.is_symlink() or not key.is_file(): raise SystemExit('[FAIL] Backup encryption key missing or symlink.')
info=key.stat()
import pwd
if info.st_uid != pwd.getpwnam('argintaxsvc').pw_uid or stat.S_IMODE(info.st_mode)!=0o600:
    raise SystemExit('[FAIL] Encryption key must belong to argintaxsvc, mode 0600.')
lines=key.read_text().splitlines()
if len(lines)!=2 or not re.fullmatch(r'[A-Za-z0-9_-]{1,32}',lines[0]) or not re.fullmatch(r'[A-Fa-f0-9]{64}',lines[1]):
    raise SystemExit('[FAIL] Invalid encryption key format.')
for filename,setting in [('backup-restore.env','ConnectionStrings__DatabaseBackupRestore'),('backup-read.env','ConnectionStrings__DatabaseBackupRead')]:
    credential=root/filename
    if credential.is_symlink() or not credential.is_file(): raise SystemExit('[FAIL] Missing separate database credential file: '+filename)
    info=credential.stat()
    if info.st_uid!=0 or info.st_gid!=pwd.getpwnam('argintaxsvc').pw_gid or stat.S_IMODE(info.st_mode)!=0o640:
        raise SystemExit('[FAIL] Database credential must be root:argintaxsvc 0640: '+filename)
    if not re.search(r'^'+setting+r'=.+',credential.read_text(),re.M):
        raise SystemExit('[FAIL] Missing separate database credential setting: '+setting)
print('[PASS] Key, backup reader and restore configuration present (values withheld).')
PY
[[ "$(systemctl show argintaxpayer-backup.service -p NoNewPrivileges --value)" == no ]] || fail 'Controller blocks the sudo lifecycle helper.'
[[ "$(systemctl show argintaxpayer-backup.service -p ReadWritePaths --value)" == *'/etc/argintaxpayer'* ]] || fail 'Controller cannot publish database selection.'
sudo -u argintaxsvc test -r /etc/argintaxpayer/database-backup.key
sudo -u argintaxsvc test -w /var/lib/argintaxpayer/database-backups
# Run the real helper with equivalent sandbox/capability properties. This only
# checks API status; it neither stops nor starts the API.
properties=(User Group NoNewPrivileges PrivateTmp ProtectSystem ProtectHome ProtectControlGroups CapabilityBoundingSet AmbientCapabilities ReadWritePaths)
args=()
for property in "${properties[@]}"; do
  value="$(systemctl show argintaxpayer-backup.service -p "$property" --value)"
  if [[ "$property" == CapabilityBoundingSet || "$property" == AmbientCapabilities ]]; then value="${value^^}"; fi
  args+=("--property=$property=$value")
done
systemd-run --quiet --collect --wait --pipe "${args[@]}" /usr/bin/sudo -n "$helper" status-business || fail 'Sandbox helper check failed or API is not active.'
echo '[PASS] Backup prerequisites and sandbox helper status check passed. Database restore privileges and full backup/restore still require an Ubuntu drill.'
