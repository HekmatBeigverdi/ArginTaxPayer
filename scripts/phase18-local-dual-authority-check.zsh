#!/bin/zsh
# Local regression checks with synthetic credentials and disposable PostgreSQL schemas.
# Does not submit invoices, contact either authority, or apply migrations.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
cd "${0:A:h:h}"
if [[ ! -f .env.dev.local ]]; then
  print -u2 'Missing .env.dev.local.'
  exit 1
fi
source .env.dev.local
if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *'Database=argintaxpayer_test;'* ||
      ( "$ConnectionStrings__ArginTaxPayerDb" != 'Host=127.0.0.1;'* &&
        "$ConnectionStrings__ArginTaxPayerDb" != 'Host=localhost;'* ) ]]; then
  print -u2 'Checks require the loopback argintaxpayer_test database.'
  exit 1
fi
export PHASE18_PRODUCER_TEST_CONNECTION="$ConnectionStrings__ArginTaxPayerDb"
export PHASE18_ONBOARDING_TEST_CONNECTION="$ConnectionStrings__ArginTaxPayerDb"
dotnet test ArginTaxPayer.sln --nologo --verbosity minimal
cd apps/web
npm test
npm run typecheck
npm run build
print 'Local dual-authority regression checks passed. Owner-driven live acceptance remains pending.'
