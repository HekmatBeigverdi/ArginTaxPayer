#!/bin/zsh
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
cd "${0:A:h:h}"
umask 077
: "${ConnectionStrings__ArginTaxPayerDb:?Source .env.dev.local in this shell first}"
eval "$(python3 - <<'PY'
import os,shlex
p=dict(x.split('=',1) for x in os.environ['ConnectionStrings__ArginTaxPayerDb'].split(';') if '=' in x)
assert p.get('Database')=='argintaxpayer_test' and p.get('Host')=='127.0.0.1' and p.get('Port')=='15432','Test database only'
for name,field in [('PGHOST','Host'),('PGPORT','Port'),('PGDATABASE','Database'),('PGUSER','Username'),('PGPASSWORD','Password')]:
 print('export '+name+'='+shlex.quote(p[field]))
PY
)"
export PGCONNECT_TIMEOUT=10
previous=20261023190000_AddPhase10InvoiceDispatchOutbox
target=20261024103000_AddSandboxFiscalMemoryMetadata
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT current_database()')" == 'argintaxpayer_test' ]] || exit 1
head="$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')"
[[ "$head" == "$previous" ]] || { echo "Unexpected migration head: $head"; exit 1; }
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_fiscal_memories') IS NULL")" == 't' ]] || { echo 'Sandbox table already exists'; exit 1; }
mkdir -p artifacts
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="artifacts/sandbox-before-$stamp.dump"
sql="artifacts/sandbox-$stamp.sql"
pg_dump -Fc -f "$backup"
pg_restore --list "$backup" >/dev/null
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet ef migrations script "$previous" "$target" --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext --output "$sql"
echo "Backup: $backup"
echo "Review SQL before APPLY: $sql"
read 'approval?Type APPLY only after SQL review (Enter cancels): '
[[ "$approval" == 'APPLY' ]] || { echo 'Cancelled; schema unchanged.'; exit 0; }
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')" == "$previous" ]] || exit 1
dotnet ef database update "$target" --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
psql -X -v ON_ERROR_STOP=1 -c "SELECT current_database(),to_regclass('public.sandbox_fiscal_memories')"
