#!/bin/zsh
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
cd "${0:A:h:h}"
umask 077

: "${ConnectionStrings__ArginTaxPayerDb:?First: set -a; source .env.dev.local; set +a}"
eval "$(python3 - <<'PY'
import os,shlex
p=dict(x.split('=',1) for x in os.environ['ConnectionStrings__ArginTaxPayerDb'].split(';') if '=' in x)
assert p.get('Database')=='argintaxpayer_test' and p.get('Host')=='127.0.0.1' and p.get('Port')=='15432', 'Refusing anything other than local tunneled TEST database'
for k,v in [('PGHOST','Host'),('PGPORT','Port'),('PGDATABASE','Database'),('PGUSER','Username'),('PGPASSWORD','Password')]:
 print('export '+k+'='+shlex.quote(p[v]))
PY
)"
export PGCONNECT_TIMEOUT=10
previous=20261024103000_AddSandboxFiscalMemoryMetadata
target=20261024170000_AddSandboxEncryptedCustodyColumns
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT current_database()')" == "argintaxpayer_test" ]] || { echo 'Wrong DB!'; exit 1; }
head="$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')"
if [[ "$head" == "$target" ]]; then
  echo 'Repair migration already recorded; verifying final columns.'
  columns="$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='sandbox_fiscal_memories'")"
  for col in private_envelope_json signing_key_version_id origin_operation_id; do
    print -r -- "$columns" | grep -Fxq -- "$col" || { echo "SCHEMA DRIFT: missing $col"; exit 2; }
  done
  echo 'PASS: repair already applied; no writes.'
  exit 0
fi
[[ "$head" == "$previous" ]] || { echo "Unexpected migration history: $head"; exit 2; }
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.sandbox_fiscal_memories') IS NOT NULL")" == 't' ]] || { echo 'Missing metadata table'; exit 2; }
columns="$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='sandbox_fiscal_memories'")"
for col in id customer_user_id taxpayer_id memory_identifier public_spki_der public_fingerprint_sha256 revision created_at_utc updated_at_utc; do
  print -r -- "$columns" | grep -Fxq -- "$col" || { echo "Missing existing column: $col"; exit 2; }
done
for col in private_envelope_json signing_key_version_id origin_operation_id; do
  if print -r -- "$columns" | grep -Fxq -- "$col"; then
    echo "SCHEMA DRIFT: $col exists but migration missing. STOP."; exit 2
  fi
done
before="$(psql -X -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM public.sandbox_fiscal_memories")"
echo "Existing Sandbox records: $before"
echo 'Creating backup before any schema change.'
mkdir -p artifacts
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
backup="artifacts/sandbox-custody-before-$stamp.dump"
sql="artifacts/sandbox-custody-repair-$stamp.sql"
pg_dump -Fc -f "$backup"
pg_restore --list "$backup" >/dev/null
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet ef migrations script "$previous" "$target" \
 --project src/ArginTaxPayer.Infrastructure \
 --startup-project src/ArginTaxPayer.Api \
 --context ArginTaxPayerDbContext --output "$sql"
echo "Backup: $backup"
echo "SQL for human inspection: $sql"
echo "No migration has been applied."
read 'approval?After reviewing SQL and confirming backup, enter APPLY (Enter cancels): '
[[ "$approval" == 'APPLY' ]] || { echo 'Cancelled; database unchanged.'; exit 0; }
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1')" == "$previous" ]] || { echo 'Migration history changed; STOP';exit 2; }
[[ "$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM public.sandbox_fiscal_memories')" == "$before" ]] || { echo 'Sandbox row count changed; STOP';exit 2; }
dotnet ef database update "$target" \
 --project src/ArginTaxPayer.Infrastructure \
 --startup-project src/ArginTaxPayer.Api \
 --context ArginTaxPayerDbContext
after="$(psql -X -At -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM public.sandbox_fiscal_memories')"
[[ "$before" == "$after" ]] || { echo "ROW COUNT MISMATCH: before=$before after=$after";exit 2; }
psql -X -v ON_ERROR_STOP=1 -c "SELECT column_name,data_type,is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='sandbox_fiscal_memories' ORDER BY ordinal_position"
psql -X -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3'
echo "PASS: same Sandbox row count ($after); check output above. No production tables changed."
