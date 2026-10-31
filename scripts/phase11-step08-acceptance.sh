#!/usr/bin/env bash
# Phase 11 Step 08 final cross-platform OFFLINE release gate.
# No migrations, no provider traffic, no invoice submission.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
case "$(uname -s)" in
 Darwin) platform=macos ;;
 Linux) . /etc/os-release; [[ "${ID:-}" == ubuntu ]] || { echo "Ubuntu required"; exit 2; }; platform=ubuntu ;;
 *) echo "Unsupported OS"; exit 2 ;;
esac
[[ "$(git branch --show-current)" == "phase/11-inquiry-reconciliation" ]] || { echo "Wrong branch"; exit 2; }
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || { echo "Tracked working tree not clean"; exit 2; }
for command in dotnet npm node git; do command -v "$command" >/dev/null || { echo "Missing $command"; exit 2; }; done
sha="$(git rev-parse HEAD)"
if [[ -n "${PHASE11_EXPECT_SHA:-}" && "${PHASE11_EXPECT_SHA}" != "$sha" ]]; then echo "SHA mismatch"; exit 2; fi
dir="artifacts/phase11-step08/$sha/$platform-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$dir"
printf 'check\tstatus\tlog\n' > "$dir/manifest.tsv"
fail=0
run(){
 name="$1"; shift
 echo "[START] $name"
 if "$@" > "$dir/$name.log" 2>&1; then
  echo "[PASS] $name"
  printf '%s\tPASS\t%s\n' "$name" "$dir/$name.log" >> "$dir/manifest.tsv"
 else
  echo "[FAIL] $name"
  tail -n 25 "$dir/$name.log"
  printf '%s\tFAIL\t%s\n' "$name" "$dir/$name.log" >> "$dir/manifest.tsv"
  fail=$((fail+1))
 fi
}
run dotnet-restore dotnet restore ArginTaxPayer.sln
run dotnet-build dotnet build ArginTaxPayer.sln -c Release --no-restore
run dotnet-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
run web-phase11 node --test apps/web/tests/phase11-step02-dispatch-regression.test.mjs apps/web/tests/phase11-step02-queue-observer.test.mjs apps/web/tests/phase11-step03-inquiry-regression.test.mjs apps/web/tests/phase11-step06-history-evidence.test.mjs
run web-phase10-legal node --test apps/web/tests/phase10-step28-sales-return-flow.test.mjs apps/web/tests/phase10-legacy-reference-inquiry-contract.test.mjs apps/web/tests/phase10-legacy-uid-inquiry-contract.test.mjs apps/web/tests/phase10-correction-send-route.test.mjs
if [[ -d apps/web/node_modules ]];then
 run web-tests bash -c 'cd apps/web && npm test'
 run web-typecheck bash -c 'cd apps/web && npm run typecheck'
 run web-build bash -c 'cd apps/web && npm run build'
else
 echo "Run (cd apps/web && npm ci) first"; fail=$((fail+1))
fi
printf 'db-audit\tMANUAL_PENDING\tRun guarded read-only SQL audit against argintaxpayer_test\n' >> "$dir/manifest.tsv"
printf 'live-sandbox\tOWNER_BASELINE\tPhase10 owner accepted; no live request in this gate\n' >> "$dir/manifest.tsv"
[[ "$(git rev-parse HEAD)" == "$sha" ]] || { echo "HEAD changed"; exit 1; }
cat "$dir/manifest.tsv"
echo "PHASE11_SHA=$sha PLATFORM=$platform FAILURES=$fail EVIDENCE=$dir"
(( fail == 0 )) || exit 1
echo "OFFLINE PASS; DB audit, host parity, owner release approval separate."
