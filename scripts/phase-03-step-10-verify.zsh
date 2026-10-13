#!/bin/zsh
# Phase 03 / Step 10 focused security integration suite — macOS.
# Run after: zsh scripts/dev-up.zsh
# Uses only the dedicated argintaxpayer_test PostgreSQL over established AutoSSH.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"
if [[ ! -f .env.dev.local ]]; then
  print -u2 "Missing .env.dev.local. Configure the standard Mac test environment first."
  exit 1
fi
set -a
source ./.env.dev.local
set +a
if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 'Refusing non-test DB'
  exit 1
fi
if ! curl --noproxy '*' -fsS http://127.0.0.1:15089/health/ready >/dev/null 2>&1; then
  print -u2 'API not ready; start the standard Mac dev environment first.'
  exit 1
fi
print 'Phase 03 Step 10: focused .NET unit + dedicated PostgreSQL integration tests'
export RUN_ARGINTAXPAYER_PG_INTEGRATION=1
dotnet test ArginTaxPayer.sln -c Release \
  --filter 'FullyQualifiedName~Identity|FullyQualifiedName~GhasedakOtpSenderTests|FullyQualifiedName~IranianMobileNumberTests'
print 'Phase 03 Step 10: Next.js identity security assertions'
(cd apps/web && npm test && npm run typecheck)
print 'Phase 03 Step 10: HTTP negative smoke (will refuse live SMS)'
python3 scripts/phase-03-step-10-identity-smoke.py
print 'PHASE 03 STEP 10 LOCAL TEST COMMANDS PASSED'
