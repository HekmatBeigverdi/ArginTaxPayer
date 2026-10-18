#!/usr/bin/env bash
# Phase07 Step23 Ubuntu operator acceptance. Test DB, no migrations/writes.
set -Eeuo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
[[ "$(git branch --show-current)" == phase/07-invoice-domain-rule-engine ]] || {
 echo "Wrong branch" >&2; exit 2;
}
[[ -z "$(git status --porcelain)" ]] || {
 echo "Dirty worktree: commit/stash before acceptance" >&2; exit 2;
}
CONFIG="$HOME/.argintaxpayer-test/ubuntu.env"
[[ -f "$CONFIG" ]] || { echo "Missing private Ubuntu test configuration" >&2; exit 2; }
set -a
# Only operator-authored private local config; never print secrets.
source "$CONFIG"
set +a
echo "Phase07 Step23 Ubuntu candidate: $(git rev-parse --short HEAD)"
echo "=== PostgreSQL test schema & EF migration history ==="
bash scripts/phase07-step23-db-gate.sh
echo "=== Ubuntu solution and UI regressions ==="
bash scripts/ubuntu-dev.sh verify
echo "=== Phase07 additional local/static contract checks ==="
node --test apps/web/tests/phase07-regression-suite.test.mjs
node --test apps/web/tests/phase07-invoice-entry-ui.test.mjs
echo "=== Local-only service readiness ==="
curl --noproxy '*' --fail --silent --show-error --max-time 15 \
  http://127.0.0.1:15089/health/ready >/dev/null
curl --noproxy '*' --fail --silent --show-error --max-time 15 \
  http://127.0.0.1:3000/identity >/dev/null
echo "PASS: API and web loopback endpoints responding"
if [[ "${PHASE07_RUN_HTTP_SMOKE:-0}" == 1 ]]; then
  export PHASE07_API_ORIGIN="http://127.0.0.1:15089"
  node scripts/phase07-step22-readonly-smoke.mjs
else
  echo "SKIPPED: authenticated HTTP smoke (requires distinct test tokens)"
fi
echo "MANUAL PENDING: browser workflows/role isolation/2-tab CAS acceptance."
echo "No migration, database write, release, tag, or merge performed."
