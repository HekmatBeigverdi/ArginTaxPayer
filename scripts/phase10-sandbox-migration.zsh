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
if [[ "$head" == "$target" ]]; then
  echo "Migration already recorded in EF history; checking physical Sandbox schema read-only."
  [[ "$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_fiscal_memories') IS NOT NULL")" == 't' ]] || {
    echo "SCHEMA DRIFT: Migration is recorded, but Sandbox table is missing. STOP."; exit 2;
  }
  columns="$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='sandbox_fiscal_memories' ORDER BY column_name")"
  for field in id customer_user_id taxpayer_id memory_identifier public_spki_der public_fingerprint_sha256 private_envelope_json signing_key_version_id origin_operation_id revision created_at_utc updated_at_utc; do
    if ! print -r -- "$columns" | grep -Fxq -- "$field"; then
      echo "SCHEMA DRIFT: Missing column: $field. STOP. Do not reset migration history."; exit 2;
    fi
  done
  echo "PASS: expected Sandbox table and columns exist; nothing to migrate."
  echo "No writes performed. For encrypted custody checks, run application tests separately."
  exit 0
fi
[[ "$head" == "$previous" ]] || { echo "Unexpected migration head: $head. STOP without modifying DB."; exit 1; }
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_fiscal_memories') IS NULL")" == 't' ]] || { echo 'Sandbox table already exists; stop without modifying DB'; exit 1; }
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
