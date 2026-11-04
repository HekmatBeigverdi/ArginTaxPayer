#!/usr/bin/env bash
# One-shot Ubuntu rollout for the RESTRICTED Production login/dashboard only.
# Build/test -> immutable stage -> gated disabled promotion -> controlled activation.
# No tax-authority dispatch, SMS, production legal workflow, or DB migration.
set -Eeuo pipefail
umask 077
die(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] ||
  die 'Ubuntu nonroot required'
[[ "${1:-}" == "--rollout-dashboard-only" && "$#" == 1 ]] ||
  die 'Usage: --rollout-dashboard-only'
[[ "${PHASE18_OWNER_APPROVED_DASHBOARD_ONLY:-}" == YES ]] ||
  die 'Owner consent required'
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] ||
  die 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
  die 'Dirty tracked source'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" == "$sha" ]] ||
  die 'Explicit exact source SHA required'
sudo -v
sudo nginx -t >/dev/null 2>&1 ||
  die 'Nginx syntax must PASS before any deployment'
sudo test -f /etc/argintaxpayer/phase18-activation-approved ||
  die 'Active deployment approval marker missing'
echo "[START] Build, test and package reviewed exact SHA $sha"
bash scripts/phase18-step05-build-release.sh
bundle=$(find "$PWD/artifacts/phase18-step05/$sha" -name SHA256SUMS -type f |
    sort | tail -1)
[[ -n "$bundle" && -f "$bundle" ]] || die 'No signed immutable packaging manifest'
bundle_dir=$(dirname "$bundle")
echo '[START] Stage immutable binary bundle without changing active version'
bash scripts/phase18-release-stage.sh --stage "$sha" \
  --bundle-dir "$bundle_dir" --sha256-manifest "$bundle"
echo '[START] Promote new binaries with all operational features still disabled'
PHASE18_OWNER_APPROVED_RELEASE=YES \
  bash scripts/phase18-release-promote.sh --promote "$sha" --initial-disabled-ui
echo '[START] Enable only production login and read-only dashboard'
PHASE18_OWNER_APPROVED_DASHBOARD_ONLY=YES PHASE18_EXPECTED_SHA="$sha" \
  bash scripts/phase18-step19-activate-dashboard-only.sh --activate-dashboard-only
echo "[DONE] Login/dashboard activation successful for $sha"
echo '[BLOCKED] Tax authority dispatch, full business API, SMS and payments remain disabled.'
