#!/usr/bin/env bash
# Phase 11 Step 07: macOS offline regression; never sends an invoice.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
[[ "$(uname -s)" == "Darwin" ]] || { echo "Step07 local gate requires macOS"; exit 2; }
[[ "$(git branch --show-current)" == "phase/11-inquiry-reconciliation" ]] || { echo "Wrong branch"; exit 2; }
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || { echo "Tracked tree is not clean"; exit 2; }
for bin in git node dotnet npm; do command -v "$bin" >/dev/null || { echo "Missing $bin"; exit 2; }; done
sha="$(git rev-parse HEAD)"
out="artifacts/phase11-step07/$sha/macos-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"
printf 'check\tstatus\tlog\n' > "$out/manifest.tsv"
fail=0
run() {
 name="$1"; shift
 echo "[START] $name"
 if "$@" >"$out/$name.log" 2>&1; then
  printf '%s\tPASS\t%s\n' "$name" "$out/$name.log" >> "$out/manifest.tsv"
  echo "[PASS] $name"
 else
  printf '%s\tFAIL\t%s\n' "$name" "$out/$name.log" >> "$out/manifest.tsv"
  tail -n 25 "$out/$name.log" || true
  fail=$((fail+1))
 fi
}
run dotnet-restore dotnet restore ArginTaxPayer.sln
run dotnet-build dotnet build ArginTaxPayer.sln -c Release --no-restore
run dotnet-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
run web-phase11 node --test apps/web/tests/phase11-step02-dispatch-regression.test.mjs apps/web/tests/phase11-step02-queue-observer.test.mjs apps/web/tests/phase11-step03-inquiry-regression.test.mjs apps/web/tests/phase11-step06-history-evidence.test.mjs
run web-phase10-legal node --test apps/web/tests/phase10-step28-sales-return-flow.test.mjs apps/web/tests/phase10-legacy-reference-inquiry-contract.test.mjs apps/web/tests/phase10-legacy-uid-inquiry-contract.test.mjs apps/web/tests/phase10-correction-send-route.test.mjs
if [[ -d apps/web/node_modules ]]; then
 run web-tests bash -c 'cd apps/web && npm test'
 run web-typecheck bash -c 'cd apps/web && npm run typecheck'
 run web-build bash -c 'cd apps/web && npm run build'
else
 echo 'Frontend dependencies absent (run npm ci in apps/web)' >&2
 fail=$((fail+1))
fi
printf 'manual-sandbox\tPENDING\tOwner must validate four legal flows and secure tenancy\n' >> "$out/manifest.tsv"
printf 'ubuntu-same-sha\tDEFERRED\tFinal Phase11 gate\n' >> "$out/manifest.tsv"
cat "$out/manifest.tsv"
echo "SHA=$sha failures=$fail evidence=$out"
((fail==0)) || exit 1
echo "OFFLINE PASS only. No live Sandbox acceptance implied."
