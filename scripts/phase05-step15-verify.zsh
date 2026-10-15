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
zsh scripts/phase05-step14-verify.zsh
if [[ "${1:-}" == "--pg" ]]; then
  print '=== Phase05 explicit opt-in rollback-only PostgreSQL checks ==='
  # Prior checker performs the test DB and applied migration validation.
  zsh scripts/phase05-step14-verify.zsh --pg
fi
print '=== Phase05 final API/UI readiness (read-only) ==='
zsh scripts/dev-status.zsh
print '=== Git release candidate identity ==='
git log -1 --format='%h %s'
print '=== Phase05 final test script ==='
print 'Optional authenticated read-only Level1/Level2 HTTP smoke: node scripts/phase05-step14-smoke.mjs'
print 'Ubuntu test environment: bash scripts/ubuntu-dev.sh verify'
print 'Step15 is not approved until macOS, Ubuntu and any applicable HTTP/PG acceptance gates are acknowledged.'
print 'No merge, tag or release was created.'
