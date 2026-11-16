#!/usr/bin/env bash
# One-time, operator-run installation. No deployment, restart or DB operations. Optional upgrade pins the installed helper hash.
set -Eeuo pipefail
umask 077
refuse() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(id -u)" == 0 ]] || refuse 'Run with sudo as the VPS operator'
[[ ( "$#" == 2 || "$#" == 4 || "$#" == 6 ) && "$1" == --install && "$2" =~ ^[a-f0-9]{40}$ ]] ||
  refuse 'Usage: --install <reviewed-full-source-SHA> [--replace-helper-sha256 <installed-hash>] [--replace-probe-sha256 <installed-hash>]'
reviewed_sha="$2"
shift 2
helper_hash=""; probe_hash=""
while (( $# )); do
  [[ "$2" =~ ^[a-f0-9]{64}$ ]] || refuse 'Invalid installed artifact hash'
  case "$1" in
    --replace-helper-sha256) [[ -z "$helper_hash" ]] || refuse 'Duplicate helper hash'; helper_hash="$2" ;;
    --replace-probe-sha256) [[ -z "$probe_hash" ]] || refuse 'Duplicate probe hash'; probe_hash="$2" ;;
    *) refuse 'Unknown replacement option' ;;
  esac
  shift 2
done
source_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
[[ "$(git -c safe.directory="$source_root" -C "$source_root" rev-parse HEAD)" == "$reviewed_sha" ]] ||
  refuse 'Checkout does not match reviewed source SHA'
[[ -z "$(git -c safe.directory="$source_root" -C "$source_root" status --porcelain --untracked-files=no)" ]] ||
  refuse 'Tracked source files are modified'
getent passwd argin-deploy >/dev/null || refuse 'Create argin-deploy first'
getent passwd argintaxsvc >/dev/null || refuse 'Application service account is absent'
[[ -x /opt/argintaxpayer/runtime/node/bin/node && -x /opt/argintaxpayer/runtime/node/bin/npm ]] ||
  refuse 'Dedicated Node/npm runtime is absent'
[[ -d /opt/argintaxpayer/releases ]] || refuse 'Release root is absent'

helper_source="$source_root/ops/phase18-deploy/argin-taxpayer-release.py"
probe_source="$source_root/scripts/phase18-step06-local-health.sh"
backup_helper_source="$source_root/scripts/phase18-backup-host-helper.sh"
backup_unit_source="$source_root/ops/phase18-systemd/argintaxpayer-backup.service"
helper=/usr/local/sbin/argin-taxpayer-release
probe=/usr/local/libexec/argintaxpayer/local-health.sh

python3 -I - "$helper_source" <<'PY'
import ast
import sys
from pathlib import Path
if sys.version_info < (3, 11):
    raise SystemExit('Python 3.11 or later is required')
ast.parse(Path(sys.argv[1]).read_text())
PY
bash -n "$probe_source"
bash -n "$backup_helper_source"
systemd-analyze verify "$backup_unit_source"

for pair in "$helper:$helper_source" "$probe:$probe_source"; do
  target="${pair%%:*}"
  original="${pair#*:}"
  [[ ! -L "$target" ]] || refuse "Refusing symlink: $target"
  if [[ -e "$target" ]] && ! cmp -s "$original" "$target"; then
    expected_hash="$helper_hash"
    [[ "$target" != "$probe" ]] || expected_hash="$probe_hash"
    [[ -n "$expected_hash" && "$(sha256sum "$target" | cut -d' ' -f1)" == "$expected_hash" ]] ||
      refuse "Different installed helper exists: $target; supply its reviewed hash for upgrade"
    install -d -o root -g root -m 0700 /var/backups/argintaxpayer-helper
    cp -p "$target" "/var/backups/argintaxpayer-helper/${expected_hash}-${target##*/}"
  fi
done

policy="$(mktemp)"
trap 'rm -f "$policy"' EXIT
printf '%s\n' \
  'argin-deploy ALL=(root) NOPASSWD: /usr/local/sbin/argin-taxpayer-release --deploy *' > "$policy"
visudo -cf "$policy"
[[ ! -L /etc/sudoers.d/argin-deploy ]] || refuse 'Sudoers path is a symlink'
if [[ -e /etc/sudoers.d/argin-deploy ]] && ! cmp -s "$policy" /etc/sudoers.d/argin-deploy; then
  refuse 'Existing deployment sudo policy differs; inspect it first'
fi

# Root-created destinations; a deployment SSH user cannot replace any ancestor.
python3 -I <<'PY'
from pathlib import Path
for name in ('/usr/local/sbin', '/usr/local/libexec', '/usr/local/libexec/argintaxpayer', '/etc/sudoers.d', '/etc/argintaxpayer', '/etc/systemd/system', '/etc/systemd/system/argintaxpayer-api.service.d'):
    path = Path(name)
    for item in (path, *path.parents):
        if item.is_symlink():
            raise SystemExit('Refusing privileged symlink: '+str(item))
        if item.exists():
            st = item.stat()
            if st.st_uid != 0 or st.st_mode & 0o022:
                raise SystemExit('Privileged path is writable by a non-root identity: '+str(item))
PY
for target in /usr/local/sbin/argin-taxpayer-backup-control /etc/systemd/system/argintaxpayer-backup.service /etc/sudoers.d/argintaxpayer-backup /etc/systemd/system/argintaxpayer-api.service.d/backup.conf; do
  [[ ! -L "$target" ]] || refuse "Privileged destination is a symlink: $target"
done
install -d -o root -g root -m 0755 /etc/systemd/system/argintaxpayer-api.service.d
install -o root -g root -m 0644 "$source_root/ops/phase18-systemd/argintaxpayer-api-backup.conf" /etc/systemd/system/argintaxpayer-api.service.d/backup.conf
install -d -o root -g root -m 0755 /usr/local/sbin /usr/local/libexec/argintaxpayer
install -o root -g root -m 0755 "$helper_source" "$helper"
install -o root -g root -m 0755 "$probe_source" "$probe"
install -o root -g root -m 0755 "$source_root/scripts/phase18-backup-preflight.sh" /usr/local/libexec/argintaxpayer/backup-preflight.sh
install -o root -g root -m 0755 "$backup_helper_source" /usr/local/sbin/argin-taxpayer-backup-control
install -o root -g root -m 0644 "$backup_unit_source" /etc/systemd/system/argintaxpayer-backup.service
install -d -o argintaxsvc -g argintaxsvc -m 0700 /var/lib/argintaxpayer/database-backups
install -o root -g root -m 0440 "$policy" /etc/sudoers.d/argin-deploy
backup_policy="$(mktemp)"
trap 'rm -f "$policy" "$backup_policy"' EXIT
printf '%s\n' \
  'argintaxsvc ALL=(root) NOPASSWD: /usr/local/sbin/argin-taxpayer-backup-control stop-business, /usr/local/sbin/argin-taxpayer-backup-control start-business, /usr/local/sbin/argin-taxpayer-backup-control status-business, /usr/local/sbin/argin-taxpayer-backup-control activate-db *' > "$backup_policy"
visudo -cf "$backup_policy"
install -o root -g root -m 0440 "$backup_policy" /etc/sudoers.d/argintaxpayer-backup
visudo -cf /etc/sudoers
systemctl daemon-reload
if [[ ! -e /etc/argintaxpayer/migration-backup.key ]]; then
  (umask 077; openssl rand -hex 32 > /etc/argintaxpayer/migration-backup.key)
fi
[[ ! -L /etc/argintaxpayer/migration-backup.key ]] || refuse 'Backup key must not be a symlink'
chown root:root /etc/argintaxpayer/migration-backup.key
chmod 0600 /etc/argintaxpayer/migration-backup.key
sha256sum "$helper" "$probe"
echo '[PASS] Restricted deployment helper installed. No application or database was changed.'
