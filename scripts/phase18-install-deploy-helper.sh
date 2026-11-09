#!/usr/bin/env bash
# One-time root installation of the fixed, narrow Phase 18 release entry point.
set -Eeuo pipefail
umask 077

refuse() {
  echo "[REFUSED] $*" >&2
  exit 2
}

[[ "$(uname -s)" == Linux && "$(id -u)" == 0 ]] || refuse 'Run this installer as root on Ubuntu'
[[ "$#" == 1 && "$1" == --install ]] || refuse 'Usage: sudo bash scripts/phase18-install-deploy-helper.sh --install'
for tool in bash chmod cut git getent id install mktemp mv stat visudo; do command -v "$tool" >/dev/null || refuse "Missing $tool"; done

repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
[[ -z "$(git -C "$repo_root" status --porcelain --untracked-files=no)" ]] || refuse 'Tracked source changes must be committed before installing'
source_sha=$(git -C "$repo_root" rev-parse HEAD)
[[ "$source_sha" =~ ^[a-f0-9]{40}$ ]] || refuse 'Cannot determine exact source SHA'

deploy_user=argin-deploy
getent passwd "$deploy_user" >/dev/null || refuse 'Create the restricted argin-deploy Unix account first'
deploy_group=$(id -gn "$deploy_user")
deploy_home=$(getent passwd "$deploy_user" | cut -d: -f6)
[[ "$deploy_home" == /* && -d "$deploy_home" ]] || refuse 'Deployment account has no valid home directory'

lib_dir=/usr/local/lib/argin-taxpayer-deploy
wrapper=/usr/local/sbin/argin-taxpayer-release
state_root=/var/lib/argin-taxpayer-deploy
incoming="$state_root/incoming"
sudoers_file=/etc/sudoers.d/argin-taxpayer-release
sudoers_line="$deploy_user ALL=(root) NOPASSWD: $wrapper --sha *"

for path in "$lib_dir" "$wrapper" "$state_root" "$incoming" "$sudoers_file"; do
  [[ ! -L "$path" ]] || refuse "Refusing symlink at protected path: $path"
done
if [[ -e "$lib_dir" && ! -d "$lib_dir" ]]; then refuse 'Release library path is not a directory'; fi
if [[ -e "$state_root" && ( ! -d "$state_root" || $(stat -c %U "$state_root") != root ) ]]; then
  refuse 'Deployment state root exists with unexpected type or owner'
fi
if [[ -e "$incoming" && ( ! -d "$incoming" || $(stat -c %U "$incoming") != "$deploy_user" ) ]]; then
  refuse 'Incoming directory exists with unexpected type or owner'
fi
if [[ -e "$wrapper" && ( ! -f "$wrapper" || $(stat -c %U "$wrapper") != root ) ]]; then
  refuse 'Release wrapper exists with unexpected type or owner'
fi
if [[ -e "$sudoers_file" ]] && ! grep -Fxq "$sudoers_line" "$sudoers_file"; then
  refuse 'Existing sudoers file differs from the fixed release-helper rule'
fi

install -d -o root -g root -m 0750 "$lib_dir"
install -d -o root -g root -m 0755 "$state_root"
install -d -o "$deploy_user" -g "$deploy_group" -m 0750 "$incoming"

install_atomic() {
  local source="$1" destination="$2" mode="$3" temp
  [[ -f "$source" && ! -L "$source" ]] || refuse "Missing trusted source file: $source"
  temp=$(mktemp "$(dirname -- "$destination")/.phase18-install.XXXXXX")
  install -o root -g root -m "$mode" "$source" "$temp"
  mv -f -- "$temp" "$destination"
}

install_atomic "$repo_root/ops/phase18-deploy/release-core.sh" "$lib_dir/release-core.sh" 0750
for script in phase18-release-stage.sh phase18-release-promote.sh phase18-step06-local-health.sh; do
  install_atomic "$repo_root/scripts/$script" "$lib_dir/$script" 0750
done
install_atomic "$repo_root/ops/phase18-deploy/phase18-release-root.sh" "$wrapper" 0750

for script in "$lib_dir"/*.sh "$wrapper"; do bash -n "$script" || refuse "Shell syntax check failed: $script"; done

sudoers_temp=$(mktemp /etc/sudoers.d/.argin-taxpayer-release.XXXXXX)
created_rule=0
trap 'rm -f -- "$sudoers_temp"' EXIT
printf '%s\n' "$sudoers_line" > "$sudoers_temp"
chmod 0440 "$sudoers_temp"
visudo -cf "$sudoers_temp" >/dev/null || refuse 'Narrow sudoers rule failed validation'
if [[ ! -e "$sudoers_file" ]]; then
  install -o root -g root -m 0440 "$sudoers_temp" "$sudoers_file"
  created_rule=1
fi
if ! visudo -cf /etc/sudoers >/dev/null; then
  [[ "$created_rule" == 1 ]] && rm -f -- "$sudoers_file"
  refuse 'System sudoers validation failed; newly installed rule removed'
fi

echo "[PASS] Restricted release helper installed for source SHA $source_sha."
echo '[INFO] No service was restarted; no database, application secret, activation marker, or authority setting was changed.'
