#!/bin/zsh
# Phase07 Step23 macOS operator acceptance. Never writes DB or changes services.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"
[[ "$(git branch --show-current)" == "phase/07-invoice-domain-rule-engine" ]] || {
 print -u2 -- "Wrong branch"; exit 2
}
[[ -z "$(git status --porcelain)" ]] || {
 print -u2 -- "Dirty worktree: commit/stash before acceptance"; exit 2
}
print -- "Phase07 Step23 macOS candidate: $(git rev-parse --short HEAD)"
print -- "=== Full Phase07 + Phase03-06 regression ==="
zsh scripts/phase07-step22-verify.zsh
print -- "=== Database readiness (standard AutoSSH dev-up must already be running) ==="
[[ -f .env.dev.local ]] || { print -u2 -- "Missing private .env.dev.local"; exit 2; }
set -a
source .env.dev.local
set +a
bash scripts/phase07-step23-db-gate.sh
print -- "=== Loopback HTTP readiness ==="
curl --noproxy '*' --fail --silent --show-error --max-time 15 \
  http://127.0.0.1:15089/health/ready >/dev/null
curl --noproxy '*' --fail --silent --show-error --max-time 15 \
  http://127.0.0.1:3000/identity >/dev/null
print -- "PASS: API and web endpoints responding locally"
if [[ "${PHASE07_RUN_HTTP_SMOKE:-0}" == "1" ]]; then
  : "${PHASE07_API_ORIGIN:=http://127.0.0.1:15089}"
  export PHASE07_API_ORIGIN
  node scripts/phase07-step22-readonly-smoke.mjs
else
  print -- "SKIPPED: authenticated scoped HTTP smoke (PHASE07_RUN_HTTP_SMOKE=1)"
fi
print -- "MANUAL PENDING: browser wizard/role/multi-tenant/CAS verification checklist in phase doc."
print -- "No migration, database mutation, release, tag or merge performed."
