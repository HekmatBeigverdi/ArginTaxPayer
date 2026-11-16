#!/bin/zsh
# ArginTaxPayer — one-command macOS UI/API/PostgreSQL developer launch.
# Usage: zsh scripts/dev-up.zsh
# Sensitive values are never echoed or committed. Only for local Development.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"
STATE="$HOME/.argintaxpayer-test"
mkdir -p "$STATE"
chmod 700 "$STATE"
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true

# Private configuration is preserved across all phases. Add new settings here.
if [[ ! -f "$ROOT/.env.dev.local" ]]; then
  print -u2 'Missing .env.dev.local. Copy .env.dev.local.example, set TEST database connection, chmod 600.'
  exit 1
fi
chmod 600 "$ROOT/.env.dev.local"
set -a
source "$ROOT/.env.dev.local"
if [[ -f "$ROOT/.env.ghasedak.local" ]]; then
  chmod 600 "$ROOT/.env.ghasedak.local"
  source "$ROOT/.env.ghasedak.local"
fi
set +a

# Never regenerate the pepper for an existing DB; old unexpired challenges depend on it.
PEPPER="$STATE/otp-pepper"
if [[ ! -s "$PEPPER" ]]; then
  umask 077
  openssl rand -hex 32 > "$PEPPER"
  chmod 600 "$PEPPER"
fi
export Identity__OtpPepper="$(< "$PEPPER")"
# Development remains the default. Production diagnostics uses its own private profile.
DB_TARGET="${ARGIN_DB_TARGET:-test}"
case "$DB_TARGET" in
  test) expected_database=argintaxpayer_test; tunnel_port=15432 ;;
  production)
    [[ -f "$ROOT/.env.production.local" ]] || { print -u2 'Missing .env.production.local'; exit 1; }
    chmod 600 "$ROOT/.env.production.local"
    # Do not silently reuse test identity/custody material for Production.
    unset Identity__OtpPepper Identity__MfaEncryptionKey SigningKeyProtection__DatabaseMasterKey
    unset ConnectionStrings__FiscalSerialRegistry
    set -a
    source "$ROOT/.env.production.local"
    set +a
    expected_database=argintaxpayer_prod; tunnel_port=15434
    export Identity__AllowDevelopmentRegistration=false
    [[ -n "${Identity__OtpPepper:-}" && -n "${SigningKeyProtection__DatabaseMasterKey:-}" ]] || { print -u2 'Production profile needs its existing OTP pepper and signing custody key.'; exit 1; }
    ;;
  *) print -u2 'ARGIN_DB_TARGET must be test or production'; exit 1 ;;
esac
if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Database=$expected_database;"* ]]; then
  print -u2 "Connection must select $expected_database for profile $DB_TARGET."
  exit 1
fi
if [[ "${Identity__OtpDeliveryMode:-Ghasedak}" == "LocalFile" ]]; then
  export Ghasedak__EnableLiveSending=false
else
  export Identity__OtpDeliveryMode=Ghasedak
fi

command -v autossh >/dev/null || { print -u2 'Install autossh: brew install autossh'; exit 1; }
command -v dotnet >/dev/null || { print -u2 'dotnet SDK missing'; exit 1; }
command -v npm >/dev/null || { print -u2 'npm missing'; exit 1; }
command -v psql >/dev/null || { print -u2 'PostgreSQL client missing: brew install libpq'; exit 1; }

if [[ "$DB_TARGET" == test ]]; then
  BACKUP_KEY="$STATE/database-backup.key"
  if [[ -L "$BACKUP_KEY" || ( -e "$BACKUP_KEY" && ! -f "$BACKUP_KEY" ) ]]; then
    print -u2 'Refusing a symlink or non-file local backup key.'
    exit 1
  fi
  if [[ ! -e "$BACKUP_KEY" ]]; then
    if ! (umask 077; setopt NO_CLOBBER; { printf '%s\n' 'dev-mac-1'; openssl rand -hex 32; } > "$BACKUP_KEY"); then
      print -u2 'Could not create the local backup key without replacing an existing file.'
      exit 1
    fi
  fi
  if ! awk 'NR==1 { if (length($0)<1 || length($0)>32 || $0 !~ /^[A-Za-z0-9_-]+$/) exit 1 }
    NR==2 { if (length($0)!=64 || $0 !~ /^[A-Fa-f0-9]+$/) exit 1 }
    NR>2 { exit 1 } END { if (NR!=2) exit 1 }' "$BACKUP_KEY"; then
    print -u2 'The existing local backup key is malformed; it was not replaced.'
    exit 1
  fi
  chmod 600 "$BACKUP_KEY"
  BACKUP_KEY_VERSION="$(sed -n '1p' "$BACKUP_KEY")"
  LOCAL_PG_BIN="$(dirname "$(command -v psql)")"
  [[ -x "$LOCAL_PG_BIN/pg_dump" && -x "$LOCAL_PG_BIN/pg_restore" ]] || {
    print -u2 'pg_dump and pg_restore must be installed beside psql (brew install libpq).'
    exit 1
  }
  mkdir -p "$STATE/database-backups"
  chmod 700 "$STATE/database-backups"
  export BackupController__LocalDevelopmentMode=true
  export BackupController__UseSudo=false
  export BackupController__HostHelperPath="$ROOT/scripts/phase18-dev-backup-host-helper.sh"
  export BackupController__LocalPath="$STATE/database-backups"
  export BackupController__EncryptionKeyPath="$BACKUP_KEY"
  export BackupController__DevelopmentKeyVersion="$BACKUP_KEY_VERSION"
  export BackupController__PgDumpPath="$LOCAL_PG_BIN/pg_dump"
  export BackupController__PgRestorePath="$LOCAL_PG_BIN/pg_restore"
else
  export BackupController__LocalDevelopmentMode=false
fi

stop_pid_tree() {
  local pid="$1"
  local child
  for child in ${(f)"$(pgrep -P "$pid" 2>/dev/null || true)"}; do
    stop_pid_tree "$child"
  done
  kill "$pid" 2>/dev/null || true
}

typeset -a started_pids=()
started_autossh=0
started_adminer=0
started_api=0
started_backup=0
started_web=0
created_api_launch_nonce=0
launch_succeeded=0
cleanup_on_failure() {
  (( launch_succeeded )) && return 0
  local pid
  for pid in ${started_pids[@]}; do
    stop_pid_tree "$pid"
  done
  (( started_api )) && rm -f "$STATE/api.pid"
  (( started_backup )) && rm -f "$STATE/backup.pid"
  (( started_web )) && rm -f "$STATE/web.pid"
  (( started_autossh )) && rm -f "$STATE/autossh.pid"
  (( started_adminer )) && rm -f "$STATE/adminer.pid"
  (( created_api_launch_nonce )) && rm -f "$STATE/api.launch-nonce"
}
trap cleanup_on_failure EXIT

# Do not start duplicate processes or hijack an existing listener.
if ! lsof -nP -iTCP:$tunnel_port -sTCP:LISTEN >/dev/null 2>&1; then
  print 'Starting AutoSSH PostgreSQL tunnel...'
  AUTOSSH_GATETIME=0 nohup autossh -M 0 -N \
    -o BatchMode=yes -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
    -o ConnectTimeout=10 -o ExitOnForwardFailure=yes \
    -p "${ARGIN_SSH_PORT:-2222}" \
    -L "127.0.0.1:$tunnel_port:127.0.0.1:$tunnel_port" "${ARGIN_SSH_HOST:-argindev@109.70.78.95}" \
    > "$STATE/autossh.log" 2>&1 < /dev/null &
  echo $! > "$STATE/autossh.pid"
  started_pids+=("$!")
  started_autossh=1
fi
tunnel_ready=0
for i in {1..30}; do
  if lsof -nP -iTCP:$tunnel_port -sTCP:LISTEN >/dev/null 2>&1; then
    tunnel_ready=1
    break
  fi
  sleep 1
done
if (( ! tunnel_ready )); then
  print -u2 "AutoSSH failed. Check $STATE/autossh.log and SSH key authentication."
  exit 1
fi

# A TEST application can use the Production allocator over an independent tunnel.
if [[ "$DB_TARGET" == test && -n "${ConnectionStrings__FiscalSerialRegistry:-}" ]] &&
   ! lsof -nP -iTCP:15434 -sTCP:LISTEN >/dev/null 2>&1; then
  AUTOSSH_GATETIME=0 nohup autossh -M 0 -N \
    -p "${ARGIN_SSH_PORT:-2222}" -o BatchMode=yes -o ExitOnForwardFailure=yes \
    -o ServerAliveInterval=15 -o ServerAliveCountMax=3 -o ConnectTimeout=10 \
    -L 127.0.0.1:15434:127.0.0.1:15434 "${ARGIN_SSH_HOST:-argindev@109.70.78.95}" \
    > "$STATE/serial-registry.log" 2>&1 < /dev/null &
  started_pids+=("$!")
  echo $! > "$STATE/serial-registry.pid"
fi

# Optional Adminer forward. Adminer runs in Docker on the VPS and is published
# only on VPS loopback port 18082. This avoids unrelated services on 8080/8081.
if ! lsof -nP -iTCP:18081 -sTCP:LISTEN >/dev/null 2>&1; then
  print 'Starting AutoSSH Adminer tunnel...'
  AUTOSSH_GATETIME=0 nohup autossh -M 0 -N \
    -o BatchMode=yes -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
    -o ConnectTimeout=10 -o ExitOnForwardFailure=yes \
    -p "${ARGIN_SSH_PORT:-2222}" \
    -L 127.0.0.1:18081:127.0.0.1:18082 "${ARGIN_SSH_HOST:-argindev@109.70.78.95}" \
    > "$STATE/adminer.log" 2>&1 < /dev/null &
  adminer_pid="$!"
  started_pids+=("$adminer_pid")
  started_adminer=1
  echo "$adminer_pid" > "$STATE/adminer.pid"
else
  print 'Port 18081 already in use; not starting or taking ownership of an Adminer tunnel.'
fi
adminer_ready=0
for i in {1..20}; do
  if curl --noproxy '*' -fsS --max-time 2 http://127.0.0.1:18081/ 2>/dev/null | grep -qi 'Adminer'; then
    adminer_ready=1
    break
  fi
  sleep 1
done
if (( adminer_ready )); then
  print 'Adminer ready: http://127.0.0.1:18081'
else
  print 'Adminer forward unavailable; inspect ~/.argintaxpayer-test/adminer.log'
fi

# A listening local port is not proof that the SSH forward reaches PostgreSQL.
# Validate the configured database before starting either application.
db_host="$(sed -n 's/.*Host=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_port="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_name="$(sed -n 's/.*Database=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_user="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_password="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
if [[ "$db_name" != "$expected_database" || -z "$db_host" || -z "$db_port" ||
      -z "$db_user" || -z "$db_password" ]]; then
  print -u2 'Invalid PostgreSQL connection settings.'
  exit 1
fi
probe_database() {
  PGSSLMODE="${PGSSLMODE:-prefer}" PGPASSWORD="$db_password" psql -X -q -h "$db_host" -p "$db_port" \
    -U "$db_user" -d "$db_name" -tAc 'SELECT current_database()'
}
if ! db_probe="$(probe_database 2>"$STATE/psql.log" | tr -d '[:space:]')"; then
  if [[ "$DB_TARGET" == production || ! -t 0 ]]; then
    print -u2 "PostgreSQL connection failed. Check $STATE/psql.log and .env.dev.local."
    exit 1
  fi
  print -u2 'Configured PostgreSQL password was rejected.'
  read -rs 'retry_password?PostgreSQL test password (hidden): '
  print
  if [[ -z "$retry_password" ]]; then
    print -u2 'No PostgreSQL password supplied.'
    exit 1
  fi
  db_password="$retry_password"
  unset retry_password
  if ! db_probe="$(probe_database 2>"$STATE/psql.log" | tr -d '[:space:]')"; then
    print -u2 "PostgreSQL connection failed. Check $STATE/psql.log."
    exit 1
  fi
  ConnectionStrings__ArginTaxPayerDb="Host=$db_host;Port=$db_port;Database=$db_name;Username=$db_user;Password=$db_password;SSL Mode=Prefer"
  export ConnectionStrings__ArginTaxPayerDb
fi
if [[ "$db_probe" != "$expected_database" ]]; then
  print -u2 'PostgreSQL preflight returned an unexpected database.'
  exit 1
fi
print "PostgreSQL preflight: $expected_database (no migrations)"

# Confirm DB name using the configured connection string through the .NET readiness
# endpoint after API starts. No migrations are applied by the launcher.
if lsof -nP -iTCP:15089 -sTCP:LISTEN >/dev/null 2>&1; then
  print -u2 'Port 15089 already in use. Stop old API before using dev-up.'
  exit 1
fi
if [[ -L "$STATE/api.launch-nonce" ]]; then
  print -u2 'Refusing a symlink at the local API launch marker.'
  exit 1
fi
DEV_LAUNCH_NONCE="$(openssl rand -hex 32)"
nonce_tmp="$(mktemp "$STATE/api-launch-nonce.XXXXXX")"
(umask 077; printf '%s\n' "$DEV_LAUNCH_NONCE" > "$nonce_tmp")
mv -f "$nonce_tmp" "$STATE/api.launch-nonce"
chmod 600 "$STATE/api.launch-nonce"
export ARGINTAX_DEV_LAUNCH_NONCE="$DEV_LAUNCH_NONCE"
created_api_launch_nonce=1
print 'Starting .NET API with automatic rebuild/restart...'
# Restart after backend edits and pulls so new dashboard routes are available.
nohup dotnet watch --no-hot-reload --project src/ArginTaxPayer.Api run \
  --no-launch-profile --urls http://127.0.0.1:15089 > "$STATE/api.log" 2>&1 < /dev/null &
api_pid="$!"
started_pids+=("$api_pid")
started_api=1
echo "$api_pid" > "$STATE/api.pid"

api_ready=0
for i in {1..50}; do
  if curl --noproxy '*' -fsS http://127.0.0.1:15089/health/ready > /dev/null 2>&1; then
    api_ready=1
    break
  fi
  sleep 1
done
if (( ! api_ready )); then
  print -u2 "API not ready. Check $STATE/api.log"
  exit 1
fi

# The backup UI proxies to this dedicated loopback controller. Keep its mutable
# state under the developer's private state directory instead of system paths.
if lsof -nP -iTCP:15092 -sTCP:LISTEN >/dev/null 2>&1; then
  print -u2 'Port 15092 already in use. Stop old backup controller before using dev-up.'
  exit 1
fi
export BackupController__ListenUrl=http://127.0.0.1:15092
export BackupController__StateDirectory="$STATE/backup-controller"
export BackupController__RecoveryStatePath="$STATE/backup-controller/recovery-state.json"
export BackupController__FtpsSecretPath="$STATE/backup-controller/ftps.json"
print 'Starting database backup controller...'
nohup dotnet watch --no-hot-reload --project src/ArginTaxPayer.Workers run \
  --no-launch-profile > "$STATE/backup.log" 2>&1 < /dev/null &
backup_pid="$!"
started_pids+=("$backup_pid")
started_backup=1
echo "$backup_pid" > "$STATE/backup.pid"
backup_ready=0
for i in {1..50}; do
  if curl --noproxy '*' -fsS --max-time 2 http://127.0.0.1:15092/health > /dev/null 2>&1; then
    backup_ready=1
    break
  fi
  sleep 1
done
if (( ! backup_ready )); then
  print -u2 "Backup controller not ready. Check $STATE/backup.log"
  exit 1
fi

# Fail closed unless the runtime has actually received its identity settings.
diagnostics="$(curl --noproxy '*' -fsS http://127.0.0.1:15089/health/dev-identity)" || exit 1
if ! echo "$diagnostics" | grep -q '"identityEndpointsRegistered":true'; then
  print -u2 "Identity API is not active. See $STATE/api.log"
  exit 1
fi
if ! echo "$diagnostics" | grep -q '"otpPepperConfigured":true'; then
  print -u2 "OTP pepper not configured in running API."
  exit 1
fi
if [[ "${Identity__OtpDeliveryMode}" != "LocalFile" ]]; then
  for field in ghasedakApiKeyConfigured ghasedakTemplateConfigured ghasedakLoginTemplateConfigured; do
    if ! echo "$diagnostics" | grep -q "\"$field\":true"; then
      print -u2 "Ghasedak preflight FAILED: $field. Check .env.ghasedak.local."
      exit 1
    fi
  done
  if echo "$diagnostics" | grep -q '"ghasedakRecipientRestrictionEnabled":true' &&
      ! echo "$diagnostics" | grep -q '"ghasedakTestRecipientConfigured":true'; then
    print -u2 "Ghasedak preflight FAILED: ghasedakTestRecipientConfigured. Check .env.ghasedak.local."
    exit 1
  fi
fi
if echo "$diagnostics" | grep -q '"smsSendingEnabled":true'; then
  if echo "$diagnostics" | grep -q '"ghasedakRecipientRestrictionEnabled":true'; then
    print 'SMS MODE: LIVE (only permitted test recipient; provider charges possible)'
  else
    print 'SMS MODE: LIVE (recipient restriction disabled; provider charges possible)'
  fi
else
  print 'SMS MODE: DISABLED (no real messages will be sent)'
fi

if lsof -nP -iTCP:3000 -sTCP:LISTEN >/dev/null 2>&1; then
  print -u2 'Port 3000 already in use. Stop old Next.js before using dev-up.'
  exit 1
fi
print 'Starting Next.js...'
export ARGIN_NEXT_DIST_DIR=.next-dev
export ARGIN_IDENTITY_UI_ENABLED=true
export ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089
export ARGIN_NEXT_DIST_DIR=.next-dev
cd apps/web
nohup npm run dev -- --hostname 127.0.0.1 --port 3000 > "$STATE/web.log" 2>&1 < /dev/null &
web_pid="$!"
started_pids+=("$web_pid")
started_web=1
echo "$web_pid" > "$STATE/web.pid"
cd "$ROOT"
web_ready=0
for i in {1..30}; do
  if curl --noproxy '*' -fsS http://127.0.0.1:3000/identity > /dev/null 2>&1; then
    web_ready=1
    break
  fi
  sleep 1
done
if (( ! web_ready )); then
  print -u2 "Next.js not ready. Check $STATE/web.log"
  exit 1
fi
print 'Services launched. Panel: http://127.0.0.1:3000/identity'
print 'Status: zsh scripts/dev-status.zsh'
print 'Stop: zsh scripts/dev-down.zsh'
launch_succeeded=1
print "Logs: $STATE/{api,backup,web,autossh,adminer}.log"
print 'SMS remains under your private Ghasedak__EnableLiveSending setting; no test sends are triggered here.'
trap - EXIT
