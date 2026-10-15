#!/bin/zsh
# Phase05 Step14 — fast Mac verification. No migration, seed or permanent mutation.
# Optional --pg runs ONLY rollback-only PostgreSQL fixture tests.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"

print '=== Phase05 Step14: build, security and Phase03/04 regression ==='
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build \
  --filter 'FullyQualifiedName~Phase05Step14QuerySecurityTests|FullyQualifiedName~Phase04ScopeRegressionTests|FullyQualifiedName~CustomerCommercialOwnershipTests|FullyQualifiedName~IdentityRoleAuthorizationTests|FullyQualifiedName~PartyPersistenceMetadataTests'
print '=== Phase05 Step14: frontend typecheck, tests, build ==='
(cd apps/web && npm run typecheck && npm test && npm run build)

if [[ "${1:-}" != "--pg" ]]; then
  print 'Offline regression PASS. To run opt-in rollback-only PostgreSQL fixture test:'
  print 'zsh scripts/phase05-step14-verify.zsh --pg'
  print 'To run authenticated READ-ONLY API checks use node scripts/phase05-step14-smoke.mjs with six PHASE05_* values.'
  exit 0
fi

if [[ ! -f .env.dev.local ]]; then
  print -u2 'Missing .env.dev.local. No database test ran.'; exit 2
fi
set -a
source .env.dev.local
set +a
conn="${ConnectionStrings__ArginTaxPayerDb:-}"
if [[ "$conn" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 'Refusing non-test database. No database test ran.'; exit 2
fi
for cli in psql dotnet; do
  command -v "$cli" >/dev/null || { print -u2 "Missing $cli"; exit 2; }
done
dbhost="$(sed -n 's/.*Host=\\([^;]*\\).*/\\1/p' <<< "$conn")"
dbport="$(sed -n 's/.*Port=\\([^;]*\\).*/\\1/p' <<< "$conn")"
dbname="$(sed -n 's/.*Database=\\([^;]*\\).*/\\1/p' <<< "$conn")"
dbuser="$(sed -n 's/.*Username=\\([^;]*\\).*/\\1/p' <<< "$conn")"
dbpass="$(sed -n 's/.*Password=\\([^;]*\\).*/\\1/p' <<< "$conn")"
if [[ -z "$dbhost" || -z "$dbport" || -z "$dbuser" || -z "$dbpass" || "$dbname" != "argintaxpayer_test" ]]; then
  print -u2 'Incomplete test database settings'; exit 2
fi
export PGPASSWORD="$dbpass"
actual="$(psql -X -h "$dbhost" -p "$dbport" -U "$dbuser" -d "$dbname" -v ON_ERROR_STOP=1 -Atqc 'SELECT current_database()' | tr -d '[:space:]')"
if [[ "$actual" != "argintaxpayer_test" ]]; then
  print -u2 'Server database mismatch: refusing integration test'; exit 2
fi
missing="$(psql -X -h "$dbhost" -p "$dbport" -U "$dbuser" -d "$dbname" -v ON_ERROR_STOP=1 -Atqc "
WITH required(id) AS (VALUES
 ('20261015101000_AddInvoiceParticipantDetails'),
 ('20261015143000_AddFiscalDirectoryIndexes'),
 ('20261015170000_AddPartyIdentityHistory'),
 ('20261015190000_AddSoftwareUserProfiles'))
SELECT required.id FROM required
WHERE NOT EXISTS (
 SELECT 1 FROM \"__EFMigrationsHistory\" h WHERE h.\"MigrationId\" = required.id
) ORDER BY required.id;
")"
if [[ -n "$missing" ]]; then
  print -u2 'Required migrations are pending (NOT auto-applied):'
  print -u2 "$missing"
  exit 2
fi
unset PGPASSWORD dbpass conn
print 'Confirmed argintaxpayer_test and required EF migrations.'
print '=== Phase05 rollback-only PostgreSQL scope test ==='
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build \
  --filter 'FullyQualifiedName~ParticipantAdministratorScopeTests'
print 'PG integration test completed; isolated fixture transaction was explicitly rolled back.'
