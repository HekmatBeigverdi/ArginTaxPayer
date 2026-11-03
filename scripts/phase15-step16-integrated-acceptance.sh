#!/usr/bin/env bash
# Phase15 Step16: reproducible, non-mutating macOS integration acceptance.
# Produces restricted local evidence; does not issue invoices, call Sandbox or run migrations.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
umask 077

[[ "$(uname -s)" == "Darwin" ]] || {
  echo "REFUSED: Step16 integration acceptance is macOS-first; Ubuntu is a later gate." >&2; exit 2;
}
[[ "$(git branch --show-current)" == "phase/15-role-based-dashboard" ]] || {
  echo "REFUSED: checkout phase/15-role-based-dashboard." >&2; exit 2;
}
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || {
  echo "REFUSED: tracked changes are present. Commit/stash intentionally before acceptance." >&2; exit 2;
}
for bin in git dotnet node npm; do
  command -v "$bin" >/dev/null || { echo "REFUSED: missing $bin" >&2; exit 2; }
done
sha="$(git rev-parse HEAD)"
if [[ -v PHASE15_EXPECT_SHA && "$sha" != "$PHASE15_EXPECT_SHA" ]]; then
  echo "REFUSED: checked-out SHA does not match PHASE15_EXPECT_SHA." >&2; exit 2
fi

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
dir="artifacts/phase15-step16/$sha/macos-$stamp"
mkdir -p "$dir"
chmod 700 "$dir"
manifest="$dir/manifest.tsv"
printf 'check\tresult\tevidence\n' > "$manifest"
fails=0
record() { printf '%s\t%s\t%s\n' "$1" "$2" "$3" >> "$manifest"; }
check(){
  local name="$1"; shift
  local log="$dir/$name.log"
  echo "[START] $name $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if "$@" > "$log" 2>&1; then
    record "$name" PASS "$log"
    echo "[PASS] $name"
  else
    local code="$?"
    record "$name" "FAIL($code)" "$log"
    echo "[FAIL] $name (code=$code); last 25 lines:"
    tail -n 25 "$log" || true
    fails=$((fails+1))
  fi
}
check dotnet-restore dotnet restore ArginTaxPayer.sln
check dotnet-build dotnet build ArginTaxPayer.sln -c Release --no-restore
check dotnet-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
check web-install bash -c 'cd apps/web && npm ci'
if [[ -d apps/web/node_modules ]]; then
  check web-integrated-acceptance bash -c 'cd apps/web && node --test tests/phase15-step16-integrated-acceptance.test.mjs'
  check web-dashboard-regression bash -c 'cd apps/web && node --test tests/phase15-step03-dashboard-proxy.test.mjs tests/phase15-step06-dashboard-components.test.mjs tests/phase15-step07-customer-ux.test.mjs tests/phase15-step08-level2-dashboard.test.mjs tests/phase15-step09-level1-dashboard.test.mjs tests/phase15-step10-financial-trends.test.mjs tests/phase15-step11-subscription-watch.test.mjs tests/phase15-step12-action-center.test.mjs tests/phase15-step13-dashboard-drilldown.test.mjs tests/phase15-step14-dashboard-performance.test.mjs tests/phase15-step15-query-performance.test.mjs'
  check web-full-tests bash -c 'cd apps/web && npm test'
  check web-typecheck bash -c 'cd apps/web && npm run typecheck'
  check web-build bash -c 'cd apps/web && npm run build'
else
  record web-checks FAIL 'npm ci did not create node_modules'
  fails=$((fails+1))
fi

# Explicitly manual: do not infer app+DB+browser tests passed from compilation.
record live-role-matrix MANUAL_PENDING 'Customer/Level2/Level1 browser and network verification'
record financial-reconciliation MANUAL_PENDING 'Compare received cash and settlement totals with authorized Phase12 ledger'
record api-runtime-smoke MANUAL_PENDING 'Run bounded Step15 localhost smoke under each test role'
record invoice-regression MANUAL_PENDING 'Operator verifies Phase10 send, correction, cancellation and return on Sandbox independently'
record database-cross-owner-isolation MANUAL_PENDING 'Use TEST owners and cross-tenant access probes'
record cross-platform-parity MANUAL_PENDING 'Step17 Ubuntu requires same commit SHA and separate evidence'

if [[ "$(git rev-parse HEAD)" != "$sha" ]]; then
  record same-sha FAIL 'HEAD changed during acceptance'
  fails=$((fails+1))
else
  record same-sha PASS "$sha"
fi
printf '{"phase":15,"step":16,"platform":"macos","sha":"%s","automaticFailures":%d,"manualAcceptance":"PENDING","sandbox":"NOT_RUN"}\n' \
  "$sha" "$fails" > "$dir/status.json"
echo
echo "Step16 SHA=$sha"
echo "Evidence directory: $dir"
cat "$manifest"
if ((fails>0)); then
  echo "AUTOMATED GATE FAILED: do not advance to Ubuntu acceptance or release." >&2
  exit 1
fi
echo "AUTOMATED MACOS GATE PASS ONLY; manual browser, financial, API, Sandbox and Ubuntu gates are pending."
