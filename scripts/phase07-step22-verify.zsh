#!/bin/zsh
set -euo pipefail
cd "${0:A:h}/.."

print -- "=== Phase07 Step22: offline + full solution regressions ==="
command -v dotnet >/dev/null || { print -u2 -- "dotnet SDK required"; exit 2; }
command -v node >/dev/null || { print -u2 -- "Node.js required"; exit 2; }
command -v npm >/dev/null || { print -u2 -- "npm required"; exit 2; }

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release \
  --no-build --filter 'FullyQualifiedName~Phase07' \
  --logger 'console;verbosity=normal'
dotnet test ArginTaxPayer.sln -c Release --no-build

node --test apps/web/tests/phase07-regression-suite.test.mjs
node --test apps/web/tests/phase07-invoice-entry-ui.test.mjs

(
  cd apps/web
  npm test
  ARGIN_NEXT_DIST_DIR=.next npm run typecheck
  ARGIN_NEXT_DIST_DIR=.next npm run build
)
print -- "Offline regression gates completed."
print -- "Optional read-only HTTP smoke uses dedicated synthetic sessions."
if [[ "${PHASE07_RUN_HTTP_SMOKE:-0}" == "1" ]]; then
  node scripts/phase07-step22-readonly-smoke.mjs
else
  print -- "HTTP smoke skipped (set PHASE07_RUN_HTTP_SMOKE=1 and the documented test variables)."
fi
print -- "No database migration or writing has been performed by this script."
