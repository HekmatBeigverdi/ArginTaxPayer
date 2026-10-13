#!/bin/zsh
# Phase 03 / Step 10 focused security integration suite — macOS.
# Run after: zsh scripts/dev-up.zsh
# Uses only the dedicated argintaxpayer_test PostgreSQL over established AutoSSH.
# Prompts privately if the configured DB password is stale (terminal runs only).
# HTTP smoke uses a temporary API with SMS disabled; existing services are untouched.
emulate -L zsh
setopt ERR_EXIT NO_UNSET PIPE_FAIL
ROOT="${0:A:h:h}"
cd "$ROOT"
if [[ ! -f .env.dev.local ]]; then
  print -u2 "Missing .env.dev.local. Configure the standard Mac test environment first."
  exit 1
fi
# Match dev-up: private settings override stale application variables in the shell.
# A verification-only override must be intentional, not an inherited app setting.
connection_override="${ARGIN_VERIFY_DB_CONNECTION:-}"
unset ConnectionStrings__ArginTaxPayerDb
set -a
source ./.env.dev.local
set +a
if [[ -n "$connection_override" ]]; then
  export ConnectionStrings__ArginTaxPayerDb="$connection_override"
fi
unset connection_override
if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 'Refusing non-test DB'
  exit 1
fi
command -v psql >/dev/null || { print -u2 'PostgreSQL client missing: brew install libpq'; exit 1; }
command -v python3 >/dev/null || { print -u2 'python3 missing'; exit 1; }

umask 077
verify_dir="$(mktemp -d "${TMPDIR:-/tmp}/argintaxpayer-verify.XXXXXX")"
verify_api_pid=''
cleanup() {
  if [[ -n "$verify_api_pid" ]]; then
    kill "$verify_api_pid" 2>/dev/null || true
    wait "$verify_api_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# API readiness does not validate the credentials loaded by this test process.
db_host="$(sed -n 's/.*Host=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_port="$(sed -n 's/.*Port=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_name="$(sed -n 's/.*Database=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_user="$(sed -n 's/.*Username=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
db_password="$(sed -n 's/.*Password=\([^;]*\).*/\1/p' <<< "$ConnectionStrings__ArginTaxPayerDb")"
if [[ "$db_name" != argintaxpayer_test || -z "$db_host" || -z "$db_port" || -z "$db_user" ]]; then
  print -u2 'Invalid dedicated test database settings.'
  exit 1
fi
print "PostgreSQL preflight: $db_host:$db_port / $db_name"
probe_database() {
  PGCONNECT_TIMEOUT=5 PGPASSWORD="$db_password" psql -X -w -q \
    -h "$db_host" -p "$db_port" -U "$db_user" -d "$db_name" \
    -tAc 'SELECT current_database()' 2>"$verify_dir/postgresql.log"
}
if ! db_probe="$(probe_database)"; then
  if ! grep -q 'password authentication failed' "$verify_dir/postgresql.log"; then
    print -u2 "Test PostgreSQL preflight failed. Check the tunnel and .env.dev.local. Details: $verify_dir/postgresql.log"
    exit 1
  fi
  if [[ ! -t 0 ]]; then
    print -u2 'Configured PostgreSQL password was rejected. Run this script in a terminal for a hidden password prompt, or supply ARGIN_VERIFY_DB_CONNECTION.'
    exit 1
  fi
  print -u2 'Configured PostgreSQL password was rejected.'
  read -rs 'db_password?PostgreSQL test password (hidden): '
  print
  if [[ -z "$db_password" ]] || ! db_probe="$(probe_database)"; then
    print -u2 "PostgreSQL authentication failed. Details: $verify_dir/postgresql.log"
    exit 1
  fi
  # Append an escaped override, preserving SSL and other connection options.
  escaped_password="${db_password//\"/\"\"}"
  export ConnectionStrings__ArginTaxPayerDb="${ConnectionStrings__ArginTaxPayerDb%;};Password=\"$escaped_password\";"
  unset escaped_password
fi
unset db_password
if [[ "${db_probe//[[:space:]]/}" != argintaxpayer_test ]]; then
  print -u2 'Refusing unexpected database.'
  exit 1
fi
print 'Phase 03 Step 10: focused .NET unit + dedicated PostgreSQL integration tests'
export RUN_ARGINTAXPAYER_PG_INTEGRATION=1
dotnet test ArginTaxPayer.sln -c Release \
  --filter 'FullyQualifiedName~Identity|FullyQualifiedName~GhasedakOtpSenderTests|FullyQualifiedName~IranianMobileNumberTests'
print 'Phase 03 Step 10: Next.js identity security assertions'
(cd apps/web && npm test && npm run typecheck)
print 'Phase 03 Step 10: HTTP negative smoke (will refuse live SMS)'
# Use a separate process so verification does not change the running app's SMS mode.
verify_port="$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')"
export ARGINTAXPAYER_API_BASE="http://127.0.0.1:$verify_port"
ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development \
  Ghasedak__EnableLiveSending=false Identity__OtpDeliveryMode=Ghasedak \
  Identity__OtpPepper="$(openssl rand -hex 32)" \
  dotnet src/ArginTaxPayer.Api/bin/Release/net9.0/ArginTaxPayer.Api.dll \
  --urls "$ARGINTAXPAYER_API_BASE" >"$verify_dir/api.log" 2>&1 &
verify_api_pid=$!
verify_ready=0
for attempt in {1..30}; do
  if ! kill -0 "$verify_api_pid" 2>/dev/null; then break; fi
  if curl --noproxy '*' --connect-timeout 2 --max-time 3 -fsS \
      "$ARGINTAXPAYER_API_BASE/health/ready" >/dev/null 2>&1; then
    verify_ready=1
    break
  fi
  sleep 1
done
if (( ! verify_ready )); then
  print -u2 "Verification API failed to start. See $verify_dir/api.log"
  exit 1
fi
python3 scripts/phase-03-step-10-identity-smoke.py
print 'PHASE 03 STEP 10 LOCAL TEST COMMANDS PASSED'
