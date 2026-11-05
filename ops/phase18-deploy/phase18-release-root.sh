#!/usr/bin/env bash
# Root entry point installed at /usr/local/sbin/argin-taxpayer-release.
set -Eeuo pipefail
umask 077

refuse() {
  echo "[REFUSED] $*" >&2
  exit 2
}

[[ "$(uname -s)" == Linux && "$(id -u)" == 0 ]] || refuse 'Installed Ubuntu root helper required'
[[ "${SUDO_USER:-}" == argin-deploy ]] || refuse 'Only the restricted argin-deploy identity may invoke releases'
[[ "$#" == 2 && "$1" == --sha ]] || refuse 'Usage: argin-taxpayer-release --sha <40-character-SHA>'
sha="$2"
[[ "$sha" =~ ^[a-f0-9]{40}$ ]] || refuse 'Full lowercase 40-character Git SHA required'

lib_dir=/usr/local/lib/argin-taxpayer-deploy
incoming_root=/var/lib/argin-taxpayer-deploy/incoming
processing_root=/var/lib/argin-taxpayer-deploy/processing
bundle="$incoming_root/$sha"
[[ -d /var/lib/argin-taxpayer-deploy && ! -L /var/lib/argin-taxpayer-deploy &&
   $(stat -c %U /var/lib/argin-taxpayer-deploy) == root ]] || refuse 'Deployment state root missing or unsafe'
[[ -d "$lib_dir" && ! -L "$lib_dir" && $(stat -c %u "$lib_dir") == 0 ]] || refuse 'Trusted release library missing or unsafe'
for script in release-core.sh phase18-release-stage.sh phase18-release-promote.sh phase18-step06-local-health.sh; do
  path="$lib_dir/$script"
  [[ -f "$path" && ! -L "$path" && $(stat -c %u "$path") == 0 ]] || refuse "Trusted release component missing: $script"
  mode=$(stat -c %a "$path")
  (( (8#$mode & 0022) == 0 )) || refuse "Trusted release component is group/world writable: $script"
done
[[ -d "$incoming_root" && ! -L "$incoming_root" && $(stat -c %U "$incoming_root") == argin-deploy ]] ||
  refuse 'Fixed incoming directory is missing or has unsafe ownership'
[[ -d "$bundle" && ! -L "$bundle" && $(stat -c %U "$bundle") == argin-deploy ]] ||
  refuse 'SHA-specific bundle directory is missing or has unsafe ownership'
[[ "$(realpath -e -- "$bundle")" == "$bundle" ]] || refuse 'Bundle path resolved outside the fixed incoming directory'

exec 9>/run/lock/argin-taxpayer-release.lock
flock -n 9 || refuse 'Another release operation is already running'

[[ ! -e "$processing_root/$sha" && ! -L "$processing_root/$sha" ]] || refuse 'Private processing path already exists'
install -d -o root -g root -m 0700 "$processing_root"
mv -- "$bundle" "$processing_root/$sha"
processing_bundle="$processing_root/$sha"
trap 'rm -rf -- "$processing_bundle"' EXIT
chown -R root:root "$processing_bundle"
chmod -R go-rwx "$processing_bundle"

source "$lib_dir/release-core.sh"
phase18_verify_release_bundle "$processing_bundle" "$sha" || exit 2
echo "[INFO] Verified immutable artifact $sha"
PHASE18_ROOT_MODE=1 bash "$lib_dir/phase18-release-stage.sh" \
  --stage "$sha" --bundle-dir "$processing_bundle" --sha256-manifest "$processing_bundle/SHA256SUMS"
PHASE18_ROOT_MODE=1 PHASE18_OWNER_APPROVED_RELEASE=YES \
  bash "$lib_dir/phase18-release-promote.sh" --promote "$sha"
