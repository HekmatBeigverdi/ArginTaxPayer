#!/usr/bin/env bash
# Phase05 final Ubuntu VPS test verification: read-only preflight + existing
# Ubuntu build/test harness. Requires private ~/.argintaxpayer-test/ubuntu.env.
# Never creates migrations, transfers accounts or changes production records.
set -Eeuo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
if [[ "$(git branch --show-current)" != "phase/05-parties-customers" ]]; then
  echo 'Refusing: switch to phase/05-parties-customers' >&2; exit 2
fi
if [[ -n "$(git status --porcelain)" ]]; then
  echo 'Refusing: working tree contains uncommitted changes' >&2; exit 2
fi
CONFIG="$HOME/.argintaxpayer-test/ubuntu.env"
if [[ ! -f "$CONFIG" ]]; then echo "Missing private Ubuntu test config" >&2; exit 2; fi
set -a
# Local trusted operator file, NEVER checked into git.
source "$CONFIG"
set +a
conn="${ConnectionStrings__ArginTaxPayerDb:-}"
if [[ "$conn" != *"Database=argintaxpayer_test;"* ||
      "$conn" != *"Host=127.0.0.1;"* ||
      "$conn" != *"Port=5432;"* ]]; then
  echo "Refusing non-local/non-test PostgreSQL" >&2; exit 2
fi
user="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$conn")"
password="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$conn")"
if [[ -z "$user" || -z "$password" ]]; then
  echo "Missing PostgreSQL test credential configuration" >&2; exit 2
fi
export PGPASSWORD="$password"
found="$(psql -X -h 127.0.0.1 -p 5432 -U "$user" -d argintaxpayer_test -v ON_ERROR_STOP=1 -Atqc 'SELECT current_database()')"
if [[ "$found" != "argintaxpayer_test" ]]; then
  echo "Wrong connected database" >&2; exit 2
fi
missing="$(psql -X -h 127.0.0.1 -p 5432 -U "$user" -d argintaxpayer_test -v ON_ERROR_STOP=1 -Atqc "
WITH required(id) AS (VALUES
 ('20261015101000_AddInvoiceParticipantDetails'),
 ('20261015143000_AddFiscalDirectoryIndexes'),
 ('20261015170000_AddPartyIdentityHistory'),
 ('20261015190000_AddSoftwareUserProfiles'))
SELECT required.id FROM required
WHERE NOT EXISTS (
 SELECT 1 FROM \"__EFMigrationsHistory\" history WHERE history.\"MigrationId\" = required.id
) ORDER BY required.id;
")"
if [[ -n "$missing" ]]; then
  echo 'Required Phase05 migrations missing; no migrations were applied:' >&2
  printf '%s\n' "$missing" >&2
  exit 2
fi
unset password PGPASSWORD conn
echo "PASS: Ubuntu test PostgreSQL and Phase05 migrations verified"
echo '=== Ubuntu Phase05 .NET/Next.js/PG regression ==='
bash scripts/ubuntu-dev.sh verify
echo '=== Ubuntu live services status (not a login/session test) ==='
bash scripts/ubuntu-dev.sh status
echo 'Final operator browser and HTTP session checks are separate.'
