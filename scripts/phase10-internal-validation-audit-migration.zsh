#!/bin/zsh
# Applies ONLY the isolated test-db lifecycle audit migration after an explicit approval.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
cd "${0:A:h:h}"
umask 077
: "${ConnectionStrings__ArginTaxPayerDb:?Source trusted .env.dev.local into this shell first}"
eval "$(python3 - <<'PY'
import os,shlex
raw=os.environ['ConnectionStrings__ArginTaxPayerDb']
entries=dict(p.split('=',1) for p in raw.split(';') if '=' in p)
if (entries.get('Host'), entries.get('Port'), entries.get('Database')) != ('127.0.0.1','15432','argintaxpayer_test'):
 raise SystemExit('STOP: expected only local SSH tunnel to argintaxpayer_test')
for k,v in [('PGHOST','Host'),('PGPORT','Port'),('PGDATABASE','Database'),('PGUSER','Username'),('PGPASSWORD','Password')]:
 if not entries.get(v):raise SystemExit('STOP: missing '+v)
 print('export '+k+'='+shlex.quote(entries[v]))
PY
)"
for cmd in psql pg_dump pg_restore dotnet;do
 command -v "$cmd" >/dev/null || { echo "Missing $cmd" >&2;exit 2; }
done
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT current_database()')" == "argintaxpayer_test" ]] || exit 2
prev='20261025183000_AddSandboxInvoiceFiscalReservations'
target='20261025230000_AddSandboxInvoiceLifecycleEvents'
latest="$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')"
echo "Applied migration head: $latest"
if [[ "$latest" == "$target" ]];then
 echo 'Already applied; checking table'
 psql -X -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_invoice_lifecycle_events')"
 exit 0
fi
[[ "$latest" == "$prev" ]] || {
 echo "STOP: migration history must end at $prev. Review earlier migrations separately." >&2
 exit 2
}
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_invoice_lifecycle_events') IS NULL")" == "t" ]] || {
 echo 'STOP: audit table exists but migration not recorded' >&2;exit 2
}
mkdir -p "$HOME/.argintaxpayer-test/backups"
dir="$HOME/.argintaxpayer-test/backups"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="$dir/phase10-validation-before-$stamp.dump"
sql="$dir/phase10-validation-$stamp.sql"
pg_dump -Fc -f "$backup"
[[ -s "$backup" ]] || exit 2
pg_restore --list "$backup" >/dev/null
dotnet ef migrations script "$prev" "$target" --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext --output "$sql"
[[ -s "$sql" ]] || exit 2
echo "Backup: $backup"
echo "Review migration SQL: $sql"
read 'answer?After reviewing SQL, type APPLY to change only argintaxpayer_test: '
[[ "$answer" == 'APPLY' ]] || { echo 'Cancelled; no schema modifications';exit 0; }
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')" == "$prev" ]] || exit 2
dotnet ef database update "$target" --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
psql -X -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_invoice_lifecycle_events')"
echo 'Audit migration applied. No invoice submitted.'
