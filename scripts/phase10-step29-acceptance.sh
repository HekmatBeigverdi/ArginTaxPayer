#!/usr/bin/env bash
# Phase 10 Step 29: cross-platform, same-SHA automated evidence gate.
# No real submissions, production access, migrations, or DB writes.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
umask 077
platform="$(uname -s)"
case "$platform" in Darwin) platform=macos;; Linux)
  [[ -f /etc/os-release ]] && . /etc/os-release
  [[ "${ID:-}" == ubuntu ]] || { echo "REFUSED: Ubuntu required" >&2; exit 2; }
  platform=ubuntu;; *) echo "Unsupported host" >&2; exit 2;; esac
[[ "$(git branch --show-current)" == "work/phase10-step29-cross-platform-acceptance" ]] || {
  echo "REFUSED: checkout Step29 branch" >&2; exit 2;
}
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || {
  echo "REFUSED: tracked working tree must be clean" >&2; exit 2;
}
for executable in git dotnet node npm; do
 command -v "$executable" >/dev/null || { echo "Missing: $executable" >&2; exit 2; }
done
sha="$(git rev-parse HEAD)"
if [[ -n "${PHASE10_EXPECT_SHA:-}" && "$sha" != "$PHASE10_EXPECT_SHA" ]]; then
 echo "REFUSED: expected SHA and HEAD differ" >&2; exit 2
fi
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
dir="artifacts/phase10-step29/$sha/$platform-$stamp"
mkdir -p "$dir"
manifest="$dir/manifest.tsv"
printf 'check\tstatus\tevidence\n' > "$manifest"
failures=0
record() { printf '%s\t%s\t%s\n' "$1" "$2" "$3" >> "$manifest"; }
check() {
 local name="$1";shift
 local output="$dir/$name.log"
 if "$@" > "$output" 2>&1; then record "$name" PASS "$output"
 else local code="$?";record "$name" "FAIL($code)" "$output";failures=$((failures+1));fi
}
check dotnet-restore dotnet restore ArginTaxPayer.sln
check dotnet-release-build dotnet build ArginTaxPayer.sln -c Release --no-restore
check dotnet-release-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
check sales-return-tests dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Phase10Step28'
if [[ -d apps/web/node_modules ]];then
 check web-tests bash -c 'cd apps/web && npm test'
 check web-step28-contract node --test apps/web/tests/phase10-step28-sales-return-flow.test.mjs
 check web-typecheck bash -c 'cd apps/web && npm run typecheck'
 check web-build bash -c 'cd apps/web && npm run build'
else
 record frontend-dependencies NOT_RUN 'apps/web/node_modules absent; run npm ci in apps/web'
 failures=$((failures+1))
fi
record postgres-test-db-audit MANUAL_PENDING 'Run guarded DB audit on macOS tunnel and on Ubuntu'
record sandbox-live-send-and-inquiry MANUAL_PENDING 'Operator must execute Type1/Pattern1 original, cancellation, correction and sales return with unique Tax IDs'
record scoped-browser-acceptance MANUAL_PENDING 'Operator must check Level1/Level2/customer, ownership, preview, chain, statuses and no duplicate rows'
record prod-isolation MANUAL_PENDING 'Operator must verify test credentials and Sandbox endpoint only'
[[ "$(git rev-parse HEAD)" == "$sha" ]] || { record same-sha FAIL 'HEAD moved';failures=$((failures+1)); }
printf '{"step":29,"platform":"%s","sha":"%s","automatic_failures":%s,"manual_acceptance":"PENDING","sandbox":"NOT_AUTOMATICALLY_TESTED"}\n' "$platform" "$sha" "$failures" > "$dir/status.json"
echo "Step29 $platform SHA=$sha evidence=$dir automatic_failures=$failures"
cat "$manifest"
(( failures == 0 )) || exit 1
echo 'OFFLINE AUTOMATED GATE PASS; live Sandbox and cross-host SHA matching require operator evidence.'
