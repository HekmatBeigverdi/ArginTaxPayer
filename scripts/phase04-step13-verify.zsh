#!/bin/zsh
# Phase04 final local verification. No schema mutations, release/tag or payment actions.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"
if [[ "$(git branch --show-current)" != "phase/04-multi-taxpayer-management" ]]; then
  print -u2 'Refusing: switch to phase/04-multi-taxpayer-management first.'
  exit 2
fi
if [[ -n "$(git status --porcelain)" ]]; then
  print -u2 'Uncommitted files found. Commit or stash before evaluating release readiness.'
  git status --short
  exit 2
fi
print '=== Phase04 Step12+Step13 local Build, tests and read-only DB checks ==='
zsh scripts/phase04-step12-check.zsh

set -a
source .env.dev.local
set +a
CONN="${ConnectionStrings__ArginTaxPayerDb:-}"
if [[ "$CONN" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 'Refusing non-test database'; exit 2
fi
HOST="$(sed -n 's/.*Host=\([^;]*\).*/\1/p' <<< "$CONN")"
PORT="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<< "$CONN")"
DB="$(sed -n 's/.*Database=\([^;]*\).*/\1/p' <<< "$CONN")"
USER="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$CONN")"
PASS="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$CONN")"
if [[ "$DB" != "argintaxpayer_test" || -z "$HOST" || -z "$PORT" ||
      -z "$USER" || -z "$PASS" ]]; then
  print -u2 'Incomplete database connection'; exit 2
fi
export PGPASSWORD="$PASS"
print '=== Phase04 required EF migrations applied ==='
db_found="$(psql -X -h "$HOST" -p "$PORT" -U "$USER" -d "$DB" -Atqc 'SELECT current_database()')"
if [[ "$db_found" != "argintaxpayer_test" ]]; then
  print -u2 'Unexpected database on connection'; exit 2
fi
# Verification-only: list missing migration identifiers, never apply migrations.
missing="$(psql -X -h "$HOST" -p "$PORT" -U "$USER" -d "$DB" -v ON_ERROR_STOP=1 -Atqc "
WITH required(name) AS (VALUES
 ('20261013220000_AddFiscalCertificateMetadata'),
 ('20261013230000_AddTaxpayerDelegateGrants'),
 ('20261013233000_AddCustomerCommercialOwnership'),
 ('20261014002000_AllowLegacyCommercialAcquisition'))
SELECT name FROM required
WHERE NOT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" h WHERE h.\"MigrationId\"=required.name)
ORDER BY name;
")"
if [[ -n "$missing" ]]; then
  print -u2 "Required Phase04 migrations not applied:"
  print -u2 "$missing"
  exit 2
fi
unset PGPASSWORD PASS CONN
print 'All required Phase04 migrations are recorded.'
print 'Git HEAD:'
git log -1 --format='%h %s'
print 'Local verification completed. Outstanding manual gate: partner/session HTTP smoke and owner browser acceptance.'
print 'Ubuntu final test (operator): bash scripts/ubuntu-dev.sh verify'
print 'Do NOT create release tag until the required checks are independently accepted.'
