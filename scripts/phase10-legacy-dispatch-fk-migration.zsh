#!/bin/zsh
# Phase 10 Sandbox Legacy dispatch FK migration; read/backup/review before approval.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
cd "${0:A:h:h}"
umask 077
: "${ConnectionStrings__ArginTaxPayerDb:?Source trusted .env.dev.local first}"
eval "$(python3 - <<'PY'
import os,shlex
parts=dict(p.split('=',1) for p in os.environ['ConnectionStrings__ArginTaxPayerDb'].split(';') if '=' in p)
if (parts.get('Host'),parts.get('Port'),parts.get('Database'))!=('127.0.0.1','15432','argintaxpayer_test'):
 raise SystemExit('STOP: expected 127.0.0.1:15432/argintaxpayer_test')
for name,key in [('PGHOST','Host'),('PGPORT','Port'),('PGDATABASE','Database'),('PGUSER','Username'),('PGPASSWORD','Password')]:
 if not parts.get(key):raise SystemExit('Missing DB connection part '+key)
 print('export '+name+'='+shlex.quote(parts[key]))
PY
)"
for tool in psql pg_dump pg_restore dotnet; do
 command -v "$tool" >/dev/null || { echo "Missing $tool" >&2;exit 2; }
done
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT current_database()')" == 'argintaxpayer_test' ]] || exit 2
previous='20261025230000_AddSandboxInvoiceLifecycleEvents'
target='20261025235900_AlignSandboxDispatchMemoryForeignKey'
latest="$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')"
echo "Migration head: $latest"
fk_check="SELECT EXISTS(SELECT 1 FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_class f ON f.oid=c.confrelid WHERE t.relname='invoice_dispatch_operations' AND f.relname='sandbox_fiscal_memories' AND c.contype='f')"
if [[ "$latest" == "$target" ]];then
 [[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c "$fk_check")" == 't' ]] || {
  echo 'STOP: migration history/schema drift' >&2;exit 2
 }
 echo 'PASS: sandbox dispatch FK already applied; no changes'
 exit 0
fi
[[ "$latest" == "$previous" ]] || {
 echo "STOP: expected exactly prior migration $previous; reconcile history first" >&2;exit 2
}
count="$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM invoice_dispatch_operations')"
[[ "$count" == '0' ]] || { echo "STOP: $count existing operations; never drop/remap evidence" >&2;exit 2; }
dir="$HOME/.argintaxpayer-test/backups"
mkdir -p "$dir"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="$dir/phase10-dispatch-before-$stamp.dump"
sql="$dir/phase10-dispatch-fk-$stamp.sql"
pg_dump -Fc -f "$backup"
[[ -s "$backup" ]] || exit 2
pg_restore --list "$backup" >/dev/null
dotnet ef migrations script "$previous" "$target" \
 --project src/ArginTaxPayer.Infrastructure \
 --startup-project src/ArginTaxPayer.Api \
 --context ArginTaxPayerDbContext --output "$sql"
[[ -s "$sql" ]] || exit 2
chmod 600 "$backup" "$sql"
echo "Backup: $backup"
echo "Review SQL: $sql"
read 'answer?After SQL review type APPLY to change only the isolated test DB: '
[[ "$answer" == 'APPLY' ]] || { echo 'Cancelled: no schema change';exit 0; }
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')" == "$previous" ]] || exit 2
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM invoice_dispatch_operations')" == '0' ]] || exit 2
dotnet ef database update "$target" \
 --project src/ArginTaxPayer.Infrastructure \
 --startup-project src/ArginTaxPayer.Api \
 --context ArginTaxPayerDbContext
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c "$fk_check")" == 't' ]] || exit 2
echo 'PASS: Sandbox dispatch FK; existing invoices and keys unchanged.'
