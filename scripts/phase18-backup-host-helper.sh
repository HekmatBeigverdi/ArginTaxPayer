#!/usr/bin/env bash
# Root-owned fixed allowlist for backup service lifecycle. No caller-controlled unit names.
set -Eeuo pipefail
[[ "$(id -u)" == 0 ]] || { echo 'root required' >&2; exit 2; }
[[ "$#" -ge 1 ]] || { echo 'invalid action' >&2; exit 2; }
case "$1" in
  status-business) [[ "$#" == 1 ]] || exit 2; exec /usr/bin/systemctl is-active argintaxpayer-api.service ;;
  activate-db)
    [[ "$#" == 2 && "$2" =~ ^recovery_[a-f0-9]{12}$ ]] || { echo 'invalid database' >&2; exit 2; }
    [[ ! -L /etc/argintaxpayer/backup-db.env ]] || { echo 'unsafe database config path' >&2; exit 2; }
    temporary="$(mktemp /etc/argintaxpayer/.backup-db.XXXXXXXX)"
    trap 'rm -f -- "$temporary"' EXIT
    printf 'Backup__ActiveDatabase=%s\n' "$2" > "$temporary"
    chown root:argintaxsvc "$temporary"
    chmod 0640 "$temporary"
    mv -f -- "$temporary" /etc/argintaxpayer/backup-db.env
    /usr/bin/systemctl start argintaxpayer-api.service
    # Restart from a separate manager-owned timer so this helper and its caller
    # can finish persisting operation status before their cgroup is stopped.
    /usr/bin/systemd-run --quiet --collect --on-active=60s \
      --unit="argintaxpayer-backup-reload-${2}" \
      /usr/bin/systemctl restart argintaxpayer-backup.service ;;
  stop-business|start-business) [[ "$#" == 1 ]] || exit 2; [[ "$1" == stop-business ]] && exec /usr/bin/systemctl stop argintaxpayer-api.service || exec /usr/bin/systemctl start argintaxpayer-api.service ;;
  *) echo 'invalid action' >&2; exit 2 ;;
esac
