#!/bin/zsh
# ArginTaxPayer Phase10 Step11: review-first, backup-before-apply migration.
# Run AFTER zsh scripts/dev-up.zsh and source .env.dev.local in this shell.
# Does not start an additional SSH tunnel; never prints secrets.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
cd "${0:A:h:h}"
umask 077

TARGET='20261023190000_AddPhase10InvoiceDispatchOutbox'
PREVIOUS='20261022150000_AllowDatabaseSigningCustodyReference'
EF=(--project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext)
connection="${ConnectionStrings__ArginTaxPayerDb:-}"
if [[ -z "$connection" ]]; then
  print -u2 'Missing connection. Run: set -a; source .env.dev.local; set +a'
  exit 2
fi
dbhost="$(sed -n 's/.*Host=\([^;]*\).*/\1/p' <<< "$connection")"
dbport="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<< "$connection")"
dbname="$(sed -n 's/.*Database=\([^;]*\).*/\1/p' <<< "$connection")"
dbuser="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$connection")"
dbpass="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$connection")"
if [[ "$dbhost" != '127.0.0.1' || "$dbport" != '15432' ||
      "$dbname" != 'argintaxpayer_test' || -z "$dbuser" || -z "$dbpass" ]]; then
  print -u2 'Refusing: configured DB must be 127.0.0.1:15432/argintaxpayer_test.'
  exit 2
fi
export PGHOST="$dbhost" PGPORT="$dbport" PGDATABASE="$dbname" PGUSER="$dbuser"
export PGPASSWORD="$dbpass"
export PGCONNECT_TIMEOUT=10
for cmd in psql pg_dump pg_restore dotnet; do
  command -v "$cmd" >/dev/null || { print -u2 "Missing: $cmd"; exit 2; }
done
if ! lsof -nP -iTCP:15432 -sTCP:LISTEN >/dev/null 2>&1; then
  print -u2 'Tunnel missing. Use project helper: zsh scripts/dev-up.zsh'
  exit 2
fi
identity="$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT current_database()' | tr -d '[:space:]')"
[[ "$identity" == 'argintaxpayer_test' ]] || { print -u2 'Unexpected database identity'; exit 2; }
print "Connected to isolated test database: $identity"
latest="$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1' | tr -d '[:space:]')"
print "Latest migration: $latest"
if [[ "$latest" == "$TARGET" ]]; then
  print 'Step11 migration is already applied. No changes made.'
  psql -X -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.invoice_dispatch_operations'), to_regclass('public.invoice_dispatch_attempts'), to_regclass('public.invoice_dispatch_receipts')"
  exit 0
fi
if [[ "$latest" != "$PREVIOUS" ]]; then
  print -u2 "Unexpected history. Expected $PREVIOUS; refusing."
  exit 2
fi
existing="$(psql -X -A -t -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public' AND tablename IN ('invoice_dispatch_operations','invoice_dispatch_attempts','invoice_dispatch_receipts');" | tr -d '[:space:]')"
[[ "$existing" == '0' ]] || { print -u2 'Dispatch tables already exist without migration history. Stop and investigate.'; exit 2; }

# Do not use a live send as a migration smoke check.
dotnet build ArginTaxPayer.sln --no-restore
backup_dir="$HOME/.argintaxpayer-test/backups"
mkdir -p "$backup_dir"
chmod 700 "$backup_dir"
stamp="$(date -u +%Y%m%d-%H%M%S)"
backup="$backup_dir/phase10-step11-before-$stamp.dump"
sql="$backup_dir/phase10-step11-review-$stamp.sql"

print 'Creating isolated test DB backup...'
pg_dump -Fc -f "$backup.partial"
[[ -s "$backup.partial" ]] || { print -u2 'Backup empty: migration refused'; exit 2; }
pg_restore --list "$backup.partial" >/dev/null || { print -u2 'Backup archive invalid'; exit 2; }
mv "$backup.partial" "$backup"
chmod 600 "$backup"
print "Backup: $backup"

dotnet ef migrations script "$PREVIOUS" "$TARGET" ${EF[@]} --output "$sql"
[[ -s "$sql" ]] || { print -u2 'SQL script missing'; exit 2; }
chmod 600 "$sql"
print "Generated SQL for mandatory review: $sql"
print 'Review with: less <file>. Do not paste secrets or signed payload.'
print 'Applying the migration requires separate owner approval.'
read 'approval?After review, type APPLY to update ONLY argintaxpayer_test; Enter cancels: '
if [[ "$approval" != 'APPLY' ]]; then
  print 'Cancelled. Backup and SQL kept; no schema changes.'
  exit 0
fi
# Recheck immediately before any DB mutation
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT current_database()' | tr -d '[:space:]')" == "$dbname" ]] || exit 2
[[ "$(psql -X -A -t -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1' | tr -d '[:space:]')" == "$PREVIOUS" ]] || exit 2
dotnet ef database update "$TARGET" ${EF[@]}
psql -X -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 3'
psql -X -v ON_ERROR_STOP=1 -c "SELECT to_regclass('public.invoice_dispatch_operations'), to_regclass('public.invoice_dispatch_attempts'), to_regclass('public.invoice_dispatch_receipts')"
print 'Review migration and table output above. No invoice has been transmitted.'
