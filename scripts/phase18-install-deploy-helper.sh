#!/usr/bin/env bash
# One-time, operator-run installation. No deployment, restart or DB operations.
set -Eeuo pipefail
umask 077
refuse() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(id -u)" == 0 ]] || refuse 'Run with sudo as the VPS operator'
[[ "$#" == 2 && "$1" == --install && "$2" =~ ^[a-f0-9]{40}$ ]] ||
  refuse 'Usage: --install <reviewed-full-source-SHA>'
source_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
[[ "$(git -c safe.directory="$source_root" -C "$source_root" rev-parse HEAD)" == "$2" ]] ||
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

for pair in "$helper:$helper_source" "$probe:$probe_source"; do
  target="${pair%%:*}"
  original="${pair#*:}"
  [[ ! -L "$target" ]] || refuse "Refusing symlink: $target"
  if [[ -e "$target" ]] && ! cmp -s "$original" "$target"; then
    refuse "Different installed helper exists: $target; review upgrade separately"
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
for name in ('/usr/local/sbin', '/usr/local/libexec', '/usr/local/libexec/argintaxpayer', '/etc/sudoers.d'):
    path = Path(name)
    for item in (path, *path.parents):
        if item.is_symlink():
            raise SystemExit('Refusing privileged symlink: '+str(item))
        if item.exists():
            st = item.stat()
            if st.st_uid != 0 or st.st_mode & 0o022:
                raise SystemExit('Privileged path is writable by a non-root identity: '+str(item))
PY
install -d -o root -g root -m 0755 /usr/local/sbin /usr/local/libexec/argintaxpayer
install -o root -g root -m 0755 "$helper_source" "$helper"
install -o root -g root -m 0755 "$probe_source" "$probe"
install -o root -g root -m 0440 "$policy" /etc/sudoers.d/argin-deploy
visudo -cf /etc/sudoers
sha256sum "$helper" "$probe"
echo '[PASS] Restricted deployment helper installed. No application or database was changed.'
