#!/usr/bin/env bash
# Build, package, stage, and manually promote the exact clean origin/main SHA.
# Run as the unprivileged Ubuntu operator (normally argindev), never as root.
# Database migrations and provider/authority operations are intentionally excluded.
set -Eeuo pipefail
umask 077

die() { echo "[REFUSED] $*" >&2; exit 2; }
log() { printf '[%s] %s\n' "$1" "$2"; }

[[ "$(uname -s)" == Linux ]] || die 'Ubuntu/Linux required'
[[ "$(id -u)" != 0 ]] || die 'Run as an unprivileged deployment operator, not root'
[[ -t 0 ]] || die 'Interactive terminal required for the final release confirmation'

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
cd "$repo_root"

[[ "$(git branch --show-current)" == main ]] || die 'Checkout must be on main'
[[ -z "$(git status --porcelain=v1 --untracked-files=all)" ]] || die 'Working tree must be completely clean'

for tool in git dotnet node npm tar sha256sum realpath find grep; do
  command -v "$tool" >/dev/null || die "Missing required tool: $tool"
done

node_dir=/opt/argintaxpayer/runtime/node/bin
[[ -x "$node_dir/node" && -x "$node_dir/npm" ]] || die 'Dedicated production Node/npm runtime missing'
export PATH="$node_dir:$PATH"

log START 'Fetching main and checking exact source SHA'
git fetch --prune origin main
sha="$(git rev-parse HEAD)"
remote_sha="$(git rev-parse refs/remotes/origin/main)"
[[ "$sha" =~ ^[a-f0-9]{40}$ ]] || die 'Unable to determine full source SHA'
[[ "$sha" == "$remote_sha" ]] || die "Checkout is not exactly origin/main (HEAD=$sha origin/main=$remote_sha)"

log START 'Checking VPS release prerequisites'
sudo -v
sudo test -d /opt/argintaxpayer/releases || die 'Phase 18 release root is absent'
sudo test -f /etc/argintaxpayer/phase18-activation-approved || die 'Phase 18 activation marker is absent'
sudo test -f /etc/argintaxpayer/api.env || die 'API private configuration is absent'
sudo test -f /etc/argintaxpayer/web.env || die 'Web private configuration is absent'
systemctl cat argintaxpayer-api.service >/dev/null || die 'API systemd unit is absent'
systemctl cat argintaxpayer-web.service >/dev/null || die 'Web systemd unit is absent'

release_root="$HOME/.cache/argin-taxpayer/manual-releases"
build_stamp="$(date -u +%Y%m%dT%H%M%SZ)"
release_dir="$release_root/${sha}-${build_stamp}"
bundle="$release_dir/bundle"
[[ ! -e "$release_dir" ]] || die "Build directory already exists: $release_dir"
install -d -m 0700 "$bundle/stage/api" "$bundle/stage/web" "$bundle/scripts"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export NEXT_TELEMETRY_DISABLED=1
export RUN_ARGINTAXPAYER_PG_INTEGRATION=0
export Ghasedak__EnableLiveSending=false
export SandboxInvoiceLegacy__Enabled=false

run() {
  local name="$1"; shift
  log START "$name"
  "$@"
  log PASS "$name"
}

run dotnet-restore dotnet restore ArginTaxPayer.sln
run dotnet-release-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
run dotnet-api-publish dotnet publish src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  -c Release --no-restore -o "$release_dir/api-publish"

run web-dependencies npm --prefix apps/web ci --no-audit --no-fund
run web-tests npm --prefix apps/web test
run web-typecheck npm --prefix apps/web run typecheck
run web-production-build env NODE_ENV=production npm --prefix apps/web run build

cp -a "$release_dir/api-publish/." "$bundle/stage/api/"
cp apps/web/package.json apps/web/package-lock.json "$bundle/stage/web/"
cp -a apps/web/.next "$bundle/stage/web/"
if [[ -d apps/web/public ]]; then cp -a apps/web/public "$bundle/stage/web/"; fi
run web-runtime-dependencies npm --prefix "$bundle/stage/web" ci --omit=dev --no-audit --no-fund

if find "$bundle/stage" -type f \
  \( -name '.env' -o -name '.env.*' -o -name '*.key' -o -name '*.pem' -o -name '*.pfx' -o -name '*.p12' \) \
  -print -quit | grep -q .; then
  die 'Private configuration or key material found in release staging'
fi

[[ -s "$bundle/stage/api/ArginTaxPayer.Api.dll" ]] || die 'Published API assembly is missing'
[[ -s "$bundle/stage/web/.next/BUILD_ID" ]] || die 'Next.js BUILD_ID is missing'
[[ -s "$bundle/stage/web/package-lock.json" ]] || die 'Web lockfile is missing'
[[ -s "$bundle/stage/web/node_modules/next/dist/bin/next" ]] || die 'Next.js runtime dependencies are missing'

(cd "$bundle/stage" && tar -czf "$bundle/api.tar.gz" api && tar -czf "$bundle/web.tar.gz" web)
printf 'sha=%s\n' "$sha" > "$bundle/build-info.txt"
cp scripts/phase18-release-stage.sh scripts/phase18-release-promote.sh \
  scripts/phase18-step06-local-health.sh "$bundle/scripts/"
(cd "$bundle" && sha256sum api.tar.gz web.tar.gz > SHA256SUMS && sha256sum -c SHA256SUMS)
rm -rf "$bundle/stage" "$release_dir/api-publish"

log PASS "Built and checksummed release bundle for $sha"
printf '\nRelease summary:\n  branch: main\n  SHA:    %s\n  bundle: %s\n  Node:   %s\n  .NET:   %s\n\n' \
  "$sha" "$bundle" "$(node --version)" "$(dotnet --version)"

read -r -p "To stage and deploy this exact Production release, type the full SHA: " approval
[[ "$approval" == "$sha" ]] || die 'SHA confirmation did not match; no release staged or promoted'

run stage-release bash "$bundle/scripts/phase18-release-stage.sh" \
  --stage "$sha" --bundle-dir "$bundle" --sha256-manifest "$bundle/SHA256SUMS"

log START 'Promoting release; application services will restart and health checks will run'
PHASE18_OWNER_APPROVED_RELEASE=YES \
  bash "$bundle/scripts/phase18-release-promote.sh" --promote "$sha"
log PASS "Release $sha promoted"
echo '[INFO] No database migrations, fiscal-authority calls, SMS settings, or payment settings were changed.'
