#!/usr/bin/env bash
# Phase10 Step20 macOS regression/security gate.
# Read/test-only: NO DB migration, NO pg_restore, NO production endpoint tests.
set -uo pipefail
cd "$(dirname "$0")/.."
umask 077

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "REFUSED: Step20 macOS gate is macOS-only; Ubuntu stays Step23."
  exit 2
fi
if [[ -n "$(git status --porcelain)" ]]; then
  echo "REFUSED: working tree has uncommitted edits; record/review them first."
  git status --short
  exit 2
fi
branch="$(git branch --show-current)"
[[ "$branch" == "phase/10-invoice-submission" ]] || {
  echo "REFUSED: unexpected branch $branch"; exit 2;
}
sha="$(git rev-parse HEAD)"
echo "Step20 immutable source SHA: $sha"

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
root="artifacts/phase10-step20-$stamp"
mkdir -p "$root"
summary="$root/manifest.tsv"
printf 'step\tstatus\tlog\n' > "$summary"
FAILURES=0

run() {
  local name="$1"; shift
  local logfile="$root/$name.log"
  echo "Running $name ..."
  if "$@" > "$logfile" 2>&1; then
    printf '%s\tPASS\t%s\n' "$name" "$logfile" >> "$summary"
    echo "PASS: $name"
  else
    local code="$?"
    printf '%s\tFAIL(%s)\t%s\n' "$name" "$code" "$logfile" >> "$summary"
    echo "FAIL: $name (exit $code); see $logfile"
    FAILURES=$((FAILURES+1))
  fi
}

run dotnet-restore dotnet restore ArginTaxPayer.sln
run dotnet-build dotnet build ArginTaxPayer.sln --no-restore
run dotnet-tests dotnet test ArginTaxPayer.sln --no-restore

if [[ -d apps/web/node_modules ]]; then
  run web-structural bash -c 'cd apps/web && node --test tests/phase10-step20-security-gate.test.mjs tests/phase10-internal-snapshot-preparation.test.mjs tests/phase10-invoice-sandbox-official-id-readiness.test.mjs tests/phase10-step16-dispatch-gate.test.mjs tests/phase10-step17-dispatch-ui.test.mjs tests/phase10-step18-dispatch-history.test.mjs tests/phase10-step19-recovery-followup.test.mjs tests/phase10-sandbox-authority-connection.test.mjs tests/phase10-sandbox-memory-isolation.test.mjs tests/phase10-sandbox-metadata.test.mjs'
  run web-tests bash -c 'cd apps/web && npm test -- --runInBand'
  run web-typecheck bash -c 'cd apps/web && npm run typecheck'
  run web-build bash -c 'cd apps/web && npm run build'
else
  printf 'web-dependencies\tNOT_RUN(node_modules missing)\tN/A\n' >> "$summary"
  echo "NOT RUN: apps/web/node_modules unavailable; run npm ci after reviewing lockfile."
  FAILURES=$((FAILURES+1))
fi

git rev-parse HEAD > "$root/head-sha.txt"
git status --short > "$root/worktree-after.txt"
if [[ -s "$root/worktree-after.txt" ]]; then
  echo "WARNING: test tools changed source worktree; see $root/worktree-after.txt"
  FAILURES=$((FAILURES+1))
fi

echo "Read-only gate manifest: $summary"
cat "$summary"
echo "Failures or required suites not run: $FAILURES"
[[ $FAILURES -eq 0 ]] || exit 1
echo "PASS: commanded Step20 macOS gate suites; manual/integration/restore evidence remains separate."
