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
if [[ -z "${ConnectionStrings__ArginTaxPayerDb:-}" ||
      "${ConnectionStrings__ArginTaxPayerDb}" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 'Refusing to run: only the dedicated argintaxpayer_test DB is allowed.'
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
started_api=0
started_web=0
cleanup_on_failure() {
  local pid
  for pid in ${started_pids[@]}; do
    stop_pid_tree "$pid"
  done
  (( started_api )) && rm -f "$STATE/api.pid"
  (( started_web )) && rm -f "$STATE/web.pid"
  (( started_autossh )) && rm -f "$STATE/autossh.pid"
}
trap cleanup_on_failure EXIT

# Do not start duplicate processes or hijack an existing listener.
if ! lsof -nP -iTCP:15432 -sTCP:LISTEN >/dev/null 2>&1; then
  print 'Starting AutoSSH PostgreSQL tunnel...'
  AUTOSSH_GATETIME=0 nohup autossh -M 0 -N \
    -o BatchMode=yes -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
    -o ConnectTimeout=10 -o ExitOnForwardFailure=yes \
    -L 127.0.0.1:15432:127.0.0.1:5432 argindev@85.133.244.156 \
    > "$STATE/autossh.log" 2>&1 < /dev/null &
  echo $! > "$STATE/autossh.pid"
  started_pids+=("$!")
  started_autossh=1
fi
tunnel_ready=0
for i in {1..30}; do
  if lsof -nP -iTCP:15432 -sTCP:LISTEN >/dev/null 2>&1; then
    tunnel_ready=1
    break
  fi
  sleep 1
done
if (( ! tunnel_ready )); then
  print -u2 "AutoSSH failed. Check $STATE/autossh.log and SSH key authentication."
  exit 1
fi

# A listening local port is not proof that the SSH forward reaches PostgreSQL.
# Validate the configured database before starting either application.
db_host="$(sed -n 's/.*Host=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_port="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_name="$(sed -n 's/.*Database=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_user="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_password="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
if [[ "$db_name" != "argintaxpayer_test" || -z "$db_host" || -z "$db_port" ||
      -z "$db_user" || -z "$db_password" ]]; then
  print -u2 'Invalid test PostgreSQL connection settings.'
  exit 1
fi
probe_database() {
  PGPASSWORD="$db_password" psql -X -q -h "$db_host" -p "$db_port" \
    -U "$db_user" -d "$db_name" -tAc 'SELECT current_database()'
}
if ! db_probe="$(probe_database 2>"$STATE/psql.log" | tr -d '[:space:]')"; then
  if [[ ! -t 0 ]]; then
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
if [[ "$db_probe" != "argintaxpayer_test" ]]; then
  print -u2 'PostgreSQL preflight returned an unexpected database.'
  exit 1
fi
print 'PostgreSQL preflight: argintaxpayer_test'

# Confirm DB name using the configured connection string through the .NET readiness
# endpoint after API starts. No migrations are applied by the launcher.
if lsof -nP -iTCP:15089 -sTCP:LISTEN >/dev/null 2>&1; then
  print -u2 'Port 15089 already in use. Stop old API before using dev-up.'
  exit 1
fi
print 'Starting .NET API...'
nohup dotnet run --no-launch-profile --project src/ArginTaxPayer.Api \
  --urls http://127.0.0.1:15089 > "$STATE/api.log" 2>&1 < /dev/null &
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
print "Logs: $STATE/{api,web,autossh}.log"
print 'SMS remains under your private Ghasedak__EnableLiveSending setting; no test sends are triggered here.'
trap - EXIT
