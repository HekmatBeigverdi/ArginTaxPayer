#!/usr/bin/env bash
# Immutable release staging. Does NOT switch current, start services or change DB.
set -Eeuo pipefail
umask 077
die(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || die 'Ubuntu nonroot operator required'
[[ "$#" == 6 && "$1" == --stage && "$3" == --bundle-dir && "$5" == --sha256-manifest ]] ||
  die 'Usage: --stage <40-char-SHA> --bundle-dir <dir> --sha256-manifest <file>'
sha="$2"
[[ "$sha" =~ ^[a-f0-9]{40}$ ]] || die 'Full Git SHA required'
for tool in sudo tar sha256sum mktemp node npm realpath; do command -v "$tool" >/dev/null || die "Missing $tool"; done
bundle=$(realpath -e -- "$4")
sums=$(realpath -e -- "$6")
[[ "$sums" == "$bundle/SHA256SUMS" ]] || die 'Checksum manifest must be in bundle directory'
for name in api.tar.gz web.tar.gz; do
  [[ -f "$bundle/$name" && ! -L "$bundle/$name" ]] || die "Missing or unsafe $name"
  count=$(grep -Ec "^[0-9a-f]{64}  $name$" "$sums" || true)
  [[ "$count" == 1 ]] || die "Incorrect checksum entry for $name"
done
(cd "$bundle" && sha256sum -c --status SHA256SUMS) || die 'Archive checksum mismatch'
[[ -f "$bundle/build-info.txt" ]] || die 'Missing build-info'
grep -Fxq "sha=$sha" "$bundle/build-info.txt" || die 'Source SHA mismatch'
sudo -v
[[ -d /opt/argintaxpayer/releases ]] || die 'Step06 release root absent'
if sudo test -e "/opt/argintaxpayer/releases/$sha"; then die 'Immutable release already staged'; fi
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/stage"
for name in api web; do
  tar -tzf "$bundle/$name.tar.gz" | grep -q "^$name/" || die "Missing $name archive root"
  if tar -tzf "$bundle/$name.tar.gz" | grep -Eq '(^/|(^|/)\.\.(/|$))'; then die 'Unsafe archive path'; fi
  tar -xzf "$bundle/$name.tar.gz" -C "$tmp/stage" --no-same-owner --no-same-permissions
done
[[ -f "$tmp/stage/api/ArginTaxPayer.Api.dll" &&
   -f "$tmp/stage/web/.next/BUILD_ID" &&
   -f "$tmp/stage/web/package-lock.json" ]] || die 'Incomplete release'
node_dir=/opt/argintaxpayer/runtime/node/bin
[[ -x "$node_dir/node" ]] || die 'Dedicated Node missing'
(cd "$tmp/stage/web" && PATH="$node_dir:$PATH" NODE_ENV=production npm ci --omit=dev --ignore-scripts --no-audit --no-fund) ||
  die 'Locked production dependency installation failed'
sudo install -d -o root -g root -m 0755 "/opt/argintaxpayer/releases/$sha"
sudo cp -a "$tmp/stage/api" "$tmp/stage/web" "/opt/argintaxpayer/releases/$sha/"
sudo chown -R root:root "/opt/argintaxpayer/releases/$sha"
sudo chmod -R go-w "/opt/argintaxpayer/releases/$sha"
sudo install -d -o argintaxsvc -g argintaxsvc -m 0700 "/opt/argintaxpayer/releases/$sha/web/.next/cache"
sudo install -o root -g root -m 0644 "$bundle/build-info.txt" "/opt/argintaxpayer/releases/$sha/build-info.txt"
echo "[PASS] Staged release $sha, activation unchanged."
