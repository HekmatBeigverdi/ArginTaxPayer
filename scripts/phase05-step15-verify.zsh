#!/bin/zsh
# ArginTaxPayer Phase05 final Mac release candidate checks.
# No schema migrations, real SMS, customer changes, git merges, tags or releases.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"

if [[ "$(git branch --show-current)" != "phase/05-parties-customers" ]]; then
  print -u2 'Refusing: switch to phase/05-parties-customers'; exit 2
fi
if [[ -n "$(git status --porcelain)" ]]; then
  print -u2 'Refusing release-candidate verification on a dirty working tree.'
  git status --short
  exit 2
fi
print '=== Phase05 final local static and security regression ==='
if [[ "${1:-}" == "--pg" ]]; then
  print '=== Phase05 explicit opt-in rollback-only PostgreSQL checks ==='
  zsh scripts/phase05-step14-verify.zsh --pg
else
  zsh scripts/phase05-step14-verify.zsh
fi
print '=== Phase05 final API/UI readiness (read-only) ==='
zsh scripts/dev-status.zsh
print '=== Git release candidate identity ==='
git log -1 --format='%h %s'
print '=== Phase05 final test script ==='
print 'Optional authenticated read-only Level1/Level2 HTTP smoke: node scripts/phase05-step14-smoke.mjs'
print 'Ubuntu test environment: bash scripts/phase05-step15-ubuntu.sh'
print 'Step15 is not approved until macOS, Ubuntu and any applicable HTTP/PG acceptance gates are acknowledged.'
print 'No merge, tag or release was created.'
