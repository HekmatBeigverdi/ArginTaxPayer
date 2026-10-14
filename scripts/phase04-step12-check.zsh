#!/bin/zsh
# Phase04 Step12 fast non-mutating macOS acceptance preflight.
# Must run from git checkout root. Does NOT apply schema migrations.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"
if [[ ! -f .env.dev.local ]]; then
  print -u2 "Missing .env.dev.local. Aborting."; exit 2
fi
set -a
source .env.dev.local
set +a
if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 "Refusing anything other than argintaxpayer_test."; exit 2
fi
for cli in dotnet npm psql node; do
  command -v "$cli" >/dev/null || { print -u2 "Missing $cli"; exit 2; }
done
# The standard developer launcher manages the persistent AutoSSH tunnel.
# Avoid restarting a running server here.
print "=== Step12 .NET build ==="
dotnet build ArginTaxPayer.sln -c Release
print "=== Step12 .NET policy and ownership regression ==="
dotnet test ArginTaxPayer.sln -c Release --no-build \
  --filter 'FullyQualifiedName~Phase04ScopeRegressionTests|FullyQualifiedName~CustomerCommercialOwnershipTests|FullyQualifiedName~TaxpayerScopePolicyTests|FullyQualifiedName~TaxpayerDelegatePersistenceTests'
print "=== Step12 Next.js static tests and TS/build ==="
(cd apps/web && npm test && npm run typecheck && npm run build)
print "=== Step12 database guard/read-only audit ==="
DB_HOST="$(sed -n 's/.*Host=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
DB_PORT="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
DB_NAME="$(sed -n 's/.*Database=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
DB_USER="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
DB_PASSWORD="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
if [[ "$DB_NAME" != "argintaxpayer_test" || -z "$DB_HOST" || -z "$DB_PORT" ||
      -z "$DB_USER" || -z "$DB_PASSWORD" ]]; then
  print -u2 "Invalid test DB settings"; exit 2
fi
export PGPASSWORD="$DB_PASSWORD"
db_actual="$(psql -X -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" \
  -v ON_ERROR_STOP=1 -tAc 'SELECT current_database()' | tr -d '[:space:]')"
if [[ "$db_actual" != "argintaxpayer_test" ]]; then
  print -u2 "Unexpected server database"; exit 2
fi
psql -X -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" \
  -v ON_ERROR_STOP=1 -f scripts/phase04-step12-audit.sql
unset PGPASSWORD DB_PASSWORD
print "Local static/model/database checks complete."
print "For live role isolation: PHASE04_ROOT_TOKEN=... PHASE04_PARTNER_TOKEN=... PHASE04_OTHER_TOKEN=... PHASE04_CUSTOMER_ID=... node scripts/phase04-step12-smoke.mjs"
print "Mutation smoke uses separate explicit opt-in; see docs/phases/phase-04.md."
