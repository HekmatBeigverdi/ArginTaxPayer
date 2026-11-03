#!/usr/bin/env bash
# Phase15 Step17: Ubuntu 24 same-SHA parity gate. Tests only; no schema or fiscal writes.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
umask 077

fail() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(uname -s)" == "Linux" ]] || fail "Ubuntu Linux required."
[[ -r /etc/os-release ]] || fail "Missing /etc/os-release."
. /etc/os-release
[[ "$ID" == "ubuntu" && "$VERSION_ID" == "24.04" ]] ||
  fail "Ubuntu 24.04 required; inspect /etc/os-release."
[[ "$(git branch --show-current)" == "phase/15-role-based-dashboard" ]] ||
  fail "Checkout phase/15-role-based-dashboard (do not detach HEAD)."
[[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
  fail "Tracked files are modified. Inspect before acceptance; do not reset automatically."
for exe in git dotnet node npm; do command -v "$exe" >/dev/null || fail "Missing $exe"; done

mac_sha="$(printenv PHASE15_MAC_ACCEPTED_SHA 2>/dev/null || true)"
[[ "$mac_sha" =~ ^[0-9a-f]{40}$ ]] ||
  fail "Set PHASE15_MAC_ACCEPTED_SHA to exact full SHA from a PASS macOS acceptance at the current code."
sha="$(git rev-parse HEAD)"
[[ "$sha" == "$mac_sha" ]] ||
  fail "Ubuntu HEAD $sha differs from accepted macOS SHA $mac_sha. Do not run mismatched acceptance."
[[ "$(git rev-parse --is-inside-work-tree)" == "true" ]] || fail "Not a Git worktree."

# Evidence is local and never committed. No private keys, tokens, test API payloads or DB output.
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
dir="artifacts/phase15-step17/$sha/ubuntu-$stamp"
mkdir -p "$dir"
chmod 700 "$dir"
manifest="$dir/manifest.tsv"
printf 'check\tresult\tevidence\n' > "$manifest"
fails=0
record() { printf '%s\t%s\t%s\n' "$1" "$2" "$3" >> "$manifest"; }
check() {
  local name="$1"; shift
  local log="$dir/$name.log"
  echo "[START] $name $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if "$@" > "$log" 2>&1; then
    record "$name" PASS "$log"
    echo "[PASS] $name"
  else
    local rc="$?"
    record "$name" "FAIL($rc)" "$log"
    fails=$((fails+1))
    echo "[FAIL] $name (exit=$rc); tail of $log"
    tail -n 20 "$log" || true
  fi
}
check platform-runtime bash -c 'set -eu; grep -E "^(ID|VERSION_ID)=" /etc/os-release; uname -m; dotnet --version; node --version; npm --version'
# Inspect nonsecret system capacity; no privileged commands and no services started.
check host-resources bash -c 'set -eu; free -h; df -h .'
check dotnet-restore dotnet restore ArginTaxPayer.sln
check dotnet-release-build dotnet build ArginTaxPayer.sln -c Release --no-restore
check dotnet-release-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
check web-install bash -c 'cd apps/web && npm ci'
if [[ -d apps/web/node_modules ]]; then
  check web-integrated-acceptance bash -c 'cd apps/web && node --test tests/phase15-step16-integrated-acceptance.test.mjs'
  check web-dashboard-regression bash -c 'cd apps/web && node --test tests/phase15-step03-dashboard-proxy.test.mjs tests/phase15-step06-dashboard-components.test.mjs tests/phase15-step07-customer-ux.test.mjs tests/phase15-step08-level2-dashboard.test.mjs tests/phase15-step09-level1-dashboard.test.mjs tests/phase15-step10-financial-trends.test.mjs tests/phase15-step11-subscription-watch.test.mjs tests/phase15-step12-action-center.test.mjs tests/phase15-step13-dashboard-drilldown.test.mjs tests/phase15-step14-dashboard-performance.test.mjs tests/phase15-step15-query-performance.test.mjs'
  check web-full-tests bash -c 'cd apps/web && npm test'
  check web-typecheck bash -c 'cd apps/web && npm run typecheck'
  check web-production-build bash -c 'cd apps/web && npm run build'
else
  record web-checks FAIL 'node_modules missing after npm ci'
  fails=$((fails+1))
fi

# No automatic Sandbox/API calls, DB mutations, migration, deploy or service restart.
record database-plan-check MANUAL_PENDING 'Read-only Step15 audit on test DB; compare with macOS evidence'
record live-role-access MANUAL_PENDING 'Run Step16 localhost role smoke using independent TEST accounts'
record financial-parity MANUAL_PENDING 'Compare same test data and authorized Phase12 ledger'
record browser-responsive-check MANUAL_PENDING 'Check Dashboard Customer/Level2/Level1 on supported browser'
record invoice-domain-regression MANUAL_PENDING 'Verify Phase10 send/cancel/correct/return unaffected; Sandbox separately if required'
record environment-configuration MANUAL_PENDING 'Operator verifies test endpoints/identities, no production actions'
record release-and-merge BLOCKED 'Step18 only after operator accepts manual gates'
if [[ "$(git rev-parse HEAD)" != "$sha" ]]; then
  record immutable-sha FAIL 'HEAD changed while testing'
  fails=$((fails+1))
else
  record immutable-sha PASS "$sha"
fi

printf '{"phase":15,"step":17,"platform":"ubuntu-24.04","sha":"%s","macAcceptedSha":"%s","automaticFailures":%d,"manualAcceptance":"PENDING","release":"BLOCKED"}\n' \
  "$sha" "$mac_sha" "$fails" > "$dir/status.json"
echo
echo "Step17 Ubuntu SHA=$sha macOS SHA=$mac_sha"
echo "Evidence directory: $dir"
cat "$manifest"
if ((fails>0)); then
  echo "AUTOMATED UBUNTU GATE FAILED ($fails). No release or merge." >&2
  exit 1
fi
echo 'AUTOMATED UBUNTU GATE PASS — manual checks, operator approval and Step18 release remain pending.'
