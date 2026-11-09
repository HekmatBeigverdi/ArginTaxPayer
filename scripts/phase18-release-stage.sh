#!/usr/bin/env bash
# Immutable release staging. Does NOT switch current, start services or change DB.
set -Eeuo pipefail
umask 077

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
core="$script_dir/release-core.sh"
[[ -f "$core" ]] || core="$script_dir/../ops/phase18-deploy/release-core.sh"
[[ -f "$core" ]] || { echo '[REFUSED] Release core is missing' >&2; exit 2; }
source "$core"
die() { echo "[REFUSED] $*" >&2; exit 2; }

[[ "$(uname -s)" == Linux ]] || die 'Ubuntu required'
if [[ "$(id -u)" == 0 ]]; then
  [[ "${PHASE18_ROOT_MODE:-}" == 1 ]] || die 'Root mode is reserved for the installed deployment wrapper'
else
  command -v sudo >/dev/null || die 'Missing sudo'
fi
[[ "$#" == 6 && "$1" == --stage && "$3" == --bundle-dir && "$5" == --sha256-manifest ]] ||
  die 'Usage: --stage <40-character-SHA> --bundle-dir <dir> --sha256-manifest <file>'
sha="$2"
bundle="$4"
sums="$6"
phase18_validate_sha "$sha" || die 'Invalid release SHA'
for tool in tar sha256sum mktemp realpath; do command -v "$tool" >/dev/null || die "Missing $tool"; done
bundle=$(realpath -e -- "$bundle") || die 'Bundle directory missing'
sums=$(realpath -e -- "$sums") || die 'Checksum manifest missing'
[[ "$sums" == "$bundle/SHA256SUMS" ]] || die 'Checksum manifest must be in bundle directory'
phase18_verify_release_bundle "$bundle" "$sha" || die 'Release bundle validation failed'
releases=/opt/argintaxpayer/releases
release="$releases/$sha"
phase18_as_root test -d "$releases" || die 'Step06 release root absent'
phase18_as_root test ! -L "$releases" || die 'Release root must not be a symlink'
if phase18_as_root test -e "$release"; then
  phase18_as_root test -d "$release" && phase18_as_root test ! -L "$release" || die 'Existing release path is unsafe'
  phase18_as_root cmp -s "$bundle/build-info.txt" "$release/build-info.txt" &&
    phase18_as_root cmp -s "$bundle/SHA256SUMS" "$release/SHA256SUMS" ||
    die 'This SHA is already staged with different metadata; immutable release refused'
  phase18_as_root test -f "$release/api/ArginTaxPayer.Api.dll" &&
    phase18_as_root test -f "$release/web/.next/BUILD_ID" &&
    phase18_as_root test -f "$release/web/node_modules/next/dist/bin/next" ||
    die 'Existing release directory is incomplete; operator repair required'
  echo "[PASS] Exact release $sha is already staged; activation unchanged."
  exit 0
fi

tmp=$(mktemp -d)
trap 'rm -rf -- "$tmp"' EXIT
mkdir -p "$tmp/stage"
chmod 0755 "$tmp" "$tmp/stage"
for name in api web; do
  tar -xzf "$bundle/$name.tar.gz" -C "$tmp/stage" --no-same-owner --no-same-permissions
done
if find "$tmp/stage" -type l -print -quit | grep -q .; then die 'Archive links are not allowed'; fi
[[ -f "$tmp/stage/api/ArginTaxPayer.Api.dll" &&
   -f "$tmp/stage/web/.next/BUILD_ID" &&
    -f "$tmp/stage/web/package-lock.json" &&
    -f "$tmp/stage/web/node_modules/next/dist/bin/next" ]] || die 'Incomplete release archive'

phase18_as_root install -d -o root -g root -m 0755 "$release"
phase18_as_root cp -a "$tmp/stage/api" "$tmp/stage/web" "$release/"
phase18_as_root chown -R root:argintaxsvc "$release"
# The service requires group read/traverse access, but never group write to deployed code.
phase18_as_root chmod -R g+rX,g-w,o-rwx "$release"
phase18_as_root install -d -o argintaxsvc -g argintaxsvc -m 0700 "$release/web/.next/cache"
phase18_as_root install -o root -g argintaxsvc -m 0640 "$bundle/build-info.txt" "$release/build-info.txt"
phase18_as_root install -o root -g argintaxsvc -m 0640 "$bundle/SHA256SUMS" "$release/SHA256SUMS"
echo "[PASS] Staged release $sha, activation unchanged."
