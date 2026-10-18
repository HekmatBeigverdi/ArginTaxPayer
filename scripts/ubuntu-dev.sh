#!/usr/bin/env bash
# ArginTaxPayer Ubuntu-only test environment. Usage: bash scripts/ubuntu-dev.sh up|status|down|verify
# Never use with a production database. Never publish Development ports.
set -Eeuo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE="$HOME/.argintaxpayer-test"
CONFIG="$STATE/ubuntu.env"
mkdir -p "$STATE"
chmod 700 "$STATE"
cd "$ROOT"

require_test_config() {
  if [[ ! -f "$CONFIG" ]]; then
    echo "Missing private $CONFIG; configure it using docs/phases/phase-03.md" >&2
    exit 1
  fi
  chmod 600 "$CONFIG"
  set -a
  # Operator-controlled local shell file; never source files from untrusted users.
  source "$CONFIG"
  set +a
  export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
  export Identity__AllowDevelopmentRegistration=true
  export Ghasedak__EnableLiveSending=false
  if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Database=argintaxpayer_test;"* ]]; then
    echo "Refusing to run without argintaxpayer_test DB in connection string." >&2
    exit 1
  fi
  if [[ "$ConnectionStrings__ArginTaxPayerDb" != *"Host=127.0.0.1;"* ||
        "$ConnectionStrings__ArginTaxPayerDb" != *"Port=5432;"* ]]; then
    echo "Ubuntu scripts require local VPS PostgreSQL at 127.0.0.1:5432." >&2
    exit 1
  fi
  [[ -s "$STATE/otp-pepper" ]] || {
    umask 077
    openssl rand -hex 32 > "$STATE/otp-pepper"
  }
  export Identity__OtpPepper="$(cat "$STATE/otp-pepper")"
  if [[ "${Identity__OtpDeliveryMode:-Ghasedak}" != "LocalFile" ]]; then
    export Identity__OtpDeliveryMode=Ghasedak
  fi
  # Validate actual current_database(), not just a connection string substring.
  local db_user db_password
  db_user="$(printf '%s' "$ConnectionStrings__ArginTaxPayerDb" | sed -n 's/.*Username=\([^;]*\).*/\1/p')"
  db_password="$(printf '%s' "$ConnectionStrings__ArginTaxPayerDb" | sed -n 's/.*Password=\([^;]*\).*/\1/p')"
  if [[ -z "$db_user" || -z "$db_password" ]]; then
    echo "Missing Username/Password in Ubuntu test-only database configuration." >&2
    exit 1
  fi
  local current
  current="$(PGPASSWORD="$db_password" psql -X -q -h 127.0.0.1 -p 5432 \
    -U "$db_user" -d argintaxpayer_test -Atqc 'SELECT current_database();')"
  [[ "$current" == "argintaxpayer_test" ]] || {
    echo "Refusing non-test PostgreSQL database." >&2
    exit 1
  }
  echo "Ubuntu test PostgreSQL: verified argintaxpayer_test"
}

running() {
  local name="$1" pidfile="$STATE/$1.ubuntu.pid" pid
  [[ -f "$pidfile" ]] || return 1
  pid="$(cat "$pidfile")"
  [[ "$pid" =~ ^[0-9]+$ ]] || return 1
  kill -0 "$pid" 2>/dev/null
}

status() {
  for name in api web; do
    if running "$name"; then
      echo "$name: process running (PID $(cat "$STATE/$name.ubuntu.pid"))"
    else
      echo "$name: no tracked process running"
    fi
  done
  if curl --noproxy '*' -fsS http://127.0.0.1:15089/health/ready >/dev/null 2>&1; then
    echo "API readiness: healthy"
  else
    echo "API readiness: unavailable"
  fi
  if curl --noproxy '*' -fsS http://127.0.0.1:3000/identity >/dev/null 2>&1; then
    echo "Next.js: ready"
  else
    echo "Next.js: unavailable"
  fi
  echo "Logs: $STATE/api.ubuntu.log, $STATE/web.ubuntu.log"
}

up() {
  require_test_config
  command -v dotnet >/dev/null
  command -v npm >/dev/null
  if running api || running web; then
    echo "An Ubuntu test process is already running; run status or down first." >&2
    exit 1
  fi
  if ss -ltn '( sport = :15089 or sport = :3000 )' | grep -Eq ':(15089|3000)[[:space:]]'; then
    echo "One of the API/UI ports is already occupied; refusing to collide." >&2
    exit 1
  fi
  nohup dotnet run --no-launch-profile --project src/ArginTaxPayer.Api \
    --urls http://127.0.0.1:15089 > "$STATE/api.ubuntu.log" 2>&1 </dev/null &
  echo "$!" > "$STATE/api.ubuntu.pid"
  local ready=0
  for _ in {1..60}; do
    if curl --noproxy '*' -fsS http://127.0.0.1:15089/health/ready >/dev/null 2>&1; then
      ready=1; break
    fi
    sleep 1
  done
  if [[ "$ready" != 1 ]]; then
    echo "API failed readiness; see $STATE/api.ubuntu.log" >&2
    exit 1
  fi
  export ARGIN_NEXT_DIST_DIR=.next-dev
  export ARGIN_IDENTITY_UI_ENABLED=true
  export ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089
  nohup npm --prefix apps/web run dev -- --hostname 127.0.0.1 --port 3000 \
    > "$STATE/web.ubuntu.log" 2>&1 </dev/null &
  echo "$!" > "$STATE/web.ubuntu.pid"
  echo "Development services launched on VPS loopback only."
  status
}

down() {
  for name in web api; do
    local pidfile="$STATE/$name.ubuntu.pid" pid
    [[ -f "$pidfile" ]] || continue
    pid="$(cat "$pidfile")"
    if [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null; then
      # Process groups are not killed blindly; supervise any spawned children.
      kill "$pid" 2>/dev/null || true
    fi
    rm -f "$pidfile"
  done
  echo "Stopped tracked Ubuntu launchers. Check status for any remaining listeners."
}

verify() {
  require_test_config
  export RUN_ARGINTAXPAYER_PG_INTEGRATION=1
  dotnet restore ArginTaxPayer.sln
  dotnet build ArginTaxPayer.sln -c Release
  dotnet test ArginTaxPayer.sln -c Release --no-build
  (cd apps/web && npm ci --no-audit --no-fund && npm test && ARGIN_NEXT_DIST_DIR=.next npm run typecheck && ARGIN_NEXT_DIST_DIR=.next npm run build)
  echo "Ubuntu build and tests finished successfully. Manual UI/provider checks remain separate."
}

case "${1:-}" in
  up) up ;;
  down) down ;;
  status) status ;;
  verify) verify ;;
  *) echo "Usage: bash scripts/ubuntu-dev.sh up|status|down|verify" >&2; exit 2 ;;
esac
