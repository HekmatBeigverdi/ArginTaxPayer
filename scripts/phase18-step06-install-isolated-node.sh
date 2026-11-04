#!/usr/bin/env bash
# Step06 only: install a PRIVATE Node executable for argintaxsvc from a
# preselected local Node 20+ binary. Never change /usr/bin/node, apt or NVM.
set -Eeuo pipefail
umask 077
refuse(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "${1:-}" == "--copy-node" && "$#" == 2 ]] ||
  refuse 'Usage: sudo bash script --copy-node /absolute/path/to/node'
[[ "$(id -u)" == 0 && "$(uname -s)" == Linux ]] || refuse 'sudo on Ubuntu required'
src="$2"
[[ "$src" == /* && -f "$src" && -x "$src" && ! -L "$src" ]] ||
  refuse 'Source must be an absolute, regular, executable, non-symlink binary'
# Never accept Node from arbitrary world-writable/temp paths.
[[ "$src" != /tmp/* && "$src" != /var/tmp/* && "$src" != /dev/shm/* ]] ||
  refuse 'Untrusted temporary source refused'
realpath=$(readlink -f -- "$src")
[[ "$realpath" == "$src" ]] || refuse 'Use the fully resolved Node path (no symlink)'
[[ "$(stat -c %a "$src")" =~ ^[0-7]{3,4}$ ]] || refuse 'Invalid source file mode'
[[ "$(stat -c %a "$src")" != *7 ]] || refuse 'World-writable binary refused'
major=$("$src" -p 'Number(process.versions.node.split(".")[0])') ||
  refuse 'Cannot execute selected source Node'
[[ "$major" =~ ^[0-9]+$ && "$major" -ge 20 ]] ||
  refuse 'A supported Node.js 20+ executable is required'
dest=/opt/argintaxpayer/runtime/node/bin/node
[[ ! -e "$dest" && ! -L "$dest" ]] ||
  refuse 'Dedicated Node already exists; refusing silent replacement'
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
  refuse 'Phase18 activation already permitted; changing runtime is unsafe'
if systemctl is-active --quiet argintaxpayer-web.service; then
  refuse 'Web service is active; runtime update requires controlled maintenance'
fi
install -d -m 0755 -o root -g root /opt/argintaxpayer
install -d -m 0755 -o root -g root /opt/argintaxpayer/runtime
install -d -m 0755 -o root -g root /opt/argintaxpayer/runtime/node
install -d -m 0755 -o root -g root /opt/argintaxpayer/runtime/node/bin
install -m 0755 -o root -g root -- "$src" "$dest"
[[ "$("$dest" -p 'process.versions.node.split(".")[0]')" == "$major" ]] ||
  refuse 'Dedicated Node version mismatch'
echo "[PASS] Dedicated Node $( "$dest" --version ) installed at $dest"
echo '[SAFE] Host /usr/bin/node, NVM, apt, Next.js apps, Systemd and Nginx untouched.'
