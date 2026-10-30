#!/usr/bin/env bash
# Step20 evidence: read-only SQL inspection against the explicitly tunneled test DB.
set -euo pipefail
cd "$(dirname "$0")/.."
umask 077
: "${ConnectionStrings__ArginTaxPayerDb:?Source .env.dev.local in the calling shell}"
eval "$(python3 - <<'PY'
import os,shlex
d=dict(x.split('=',1) for x in os.environ['ConnectionStrings__ArginTaxPayerDb'].split(';') if '=' in x)
assert d.get('Database')=='argintaxpayer_test' and d.get('Host')=='127.0.0.1' and d.get('Port')=='15432','Refused: not the tunneled test database'
for env,field in [('PGHOST','Host'),('PGPORT','Port'),('PGDATABASE','Database'),('PGUSER','Username'),('PGPASSWORD','Password')]:
 print('export '+env+'='+shlex.quote(d[field]))
PY
)"
export PGCONNECT_TIMEOUT=10
export PGOPTIONS='-c default_transaction_read_only=on -c statement_timeout=15000'
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT current_database()')" == 'argintaxpayer_test' ]] || {
 echo 'Refusing unexpected DB'; exit 2;
}
echo 'Step20: read-only migration/constraint/trigger audit on argintaxpayer_test'
psql -X -v ON_ERROR_STOP=1 -f scripts/phase10-step20-db-audit.sql
echo 'PASS: metadata-only audit. This does not establish backup-restore or HTTP integration success.'
