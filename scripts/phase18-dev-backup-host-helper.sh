#!/usr/bin/env bash
# Local-only lifecycle helper used by dev-up.zsh; never uses sudo or system services.
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
STATE="$HOME/.argintaxpayer-test"
PID_FILE="$STATE/api.pid"
API_URL="http://127.0.0.1:15089/health/ready"
LAUNCH_NONCE="${ARGINTAX_DEV_LAUNCH_NONCE:-}"

log() {
  printf '%s action=%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${1:-unknown}" "${2:-}" >> "$STATE/backup-host-helper.log"
}

api_ready() {
  curl --noproxy '*' -fsS --max-time 2 "$API_URL" >/dev/null 2>&1
}

process_matches_local_api() {
  local pid="$1"
  local command_line process_cwd recorded_nonce
  [[ -n "$LAUNCH_NONCE" && -f "$STATE/api.launch-nonce" && ! -L "$STATE/api.launch-nonce" ]] || return 1
  recorded_nonce="$(<"$STATE/api.launch-nonce")"
  [[ "$recorded_nonce" == "$LAUNCH_NONCE" ]] || return 1
  command_line="$(ps -ww -p "$pid" -o command= 2>/dev/null || true)"
  case "$command_line" in
    *"dotnet watch --no-hot-reload --project src/ArginTaxPayer.Api run"*) ;;
    *) return 1 ;;
  esac
  process_cwd="$(lsof -a -p "$pid" -d cwd -Fn 2>/dev/null | sed -n 's/^n//p' | head -n 1)"
  [[ "$process_cwd" == "$ROOT" ]] || return 1
  ps eww -p "$pid" -o command= 2>/dev/null | grep -Fq -- "ARGINTAX_DEV_LAUNCH_NONCE=$LAUNCH_NONCE"
}

tracked_api_ready() {
  local pid
  [[ -f "$PID_FILE" ]] || return 1
  pid="$(<"$PID_FILE")"
  [[ "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null &&
    process_matches_local_api "$pid" && api_ready
}

stop_tree() {
  local pid="$1"
  local child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    stop_tree "$child"
  done
  kill -TERM "$pid" 2>/dev/null || true
}

stop_api() {
  local pid=""
  if [[ -f "$PID_FILE" ]]; then
    pid="$(<"$PID_FILE")"
    [[ "$pid" =~ ^[0-9]+$ ]] || { echo 'Invalid local API PID file.' >&2; return 2; }
  fi

  if [[ -z "$pid" ]] || ! kill -0 "$pid" 2>/dev/null; then
    if api_ready; then
      echo 'Refusing to stop an API that was not launched by dev-up.zsh.' >&2
      return 2
    fi
    rm -f "$PID_FILE"
    return 0
  fi
  process_matches_local_api "$pid" || {
    echo 'Refusing to stop a process that does not match this checkout API launcher.' >&2
    return 2
  }

  # dotnet watch can survive SIGTERM after its application child exits.
  # Capture only descendants of the launcher whose ownership we checked above.
  local tracked_pids=()
  collect_tree() {
    local parent="$1" child
    for child in $(pgrep -P "$parent" 2>/dev/null || true); do
      collect_tree "$child"
    done
    tracked_pids+=("$parent")
  }
  collect_tree "$pid"
  log stop-business "Stopping tracked launcher pid=$pid descendants=${#tracked_pids[@]}"
  stop_tree "$pid"
  for _ in {1..5}; do
    if ! kill -0 "$pid" 2>/dev/null && ! lsof -nP -iTCP:15089 -sTCP:LISTEN >/dev/null 2>&1; then
      rm -f "$PID_FILE"
      log stop-business 'Tracked API and launcher stopped after SIGTERM.'
      return 0
    fi
    sleep 1
  done
  # Recheck the nonce, command and checkout before escalating the same launcher.
  if kill -0 "$pid" 2>/dev/null; then
    process_matches_local_api "$pid" || {
      log stop-business "Launcher ownership changed; refusing forced termination pid=$pid"
      return 2
    }
    log stop-business "SIGTERM left dotnet watch running; terminating tracked tree pid=$pid"
    local tracked
    for tracked in "${tracked_pids[@]}"; do
      # Only terminate surviving members still descended from our launcher.
      if [[ "$tracked" == "$pid" ]] || [[ "$(ps -p "$tracked" -o ppid= 2>/dev/null | tr -d ' ')" == "$pid" ]]; then
        kill -KILL "$tracked" 2>/dev/null || true
      fi
    done
  fi
  for _ in {1..5}; do
    if ! kill -0 "$pid" 2>/dev/null && ! lsof -nP -iTCP:15089 -sTCP:LISTEN >/dev/null 2>&1; then
      rm -f "$PID_FILE"
      log stop-business 'Tracked API and launcher stopped successfully.'
      return 0
    fi
    sleep 1
  done
  log stop-business "Failed to stop tracked API pid=$pid"
  echo 'The tracked local API did not stop after TERM and KILL; inspect backup-host-helper.log.' >&2
  return 1
}

start_api() {
  local pid=""
  if api_ready; then
    tracked_api_ready || { echo 'A ready API is not tracked by this checkout; refusing to claim it.' >&2; return 2; }
    return 0
  fi
  if [[ -f "$PID_FILE" ]]; then
    pid="$(<"$PID_FILE")"
    [[ "$pid" =~ ^[0-9]+$ ]] || { echo 'Invalid local API PID file.' >&2; return 2; }
    if kill -0 "$pid" 2>/dev/null; then
      process_matches_local_api "$pid" || {
        echo 'Refusing to replace a process that does not match this checkout API launcher.' >&2
        return 2
      }
      echo 'The tracked local API process exists but is not ready.' >&2
      return 1
    fi
    rm -f "$PID_FILE"
  fi
  if lsof -nP -iTCP:15089 -sTCP:LISTEN >/dev/null 2>&1; then
    echo 'Port 15089 is owned by an untracked process; refusing to start another API.' >&2
    return 2
  fi

  cd "$ROOT"
  nohup dotnet watch --no-hot-reload --project src/ArginTaxPayer.Api run \
    --no-launch-profile --urls http://127.0.0.1:15089 \
    > "$STATE/api.log" 2>&1 < /dev/null &
  pid="$!"
  printf '%s\n' "$pid" > "$PID_FILE"
  for _ in {1..50}; do
    if api_ready; then
      process_matches_local_api "$pid" && return 0
      echo 'The API became ready but its tracked process does not match this checkout.' >&2
      return 2
    fi
    if ! kill -0 "$pid" 2>/dev/null; then break; fi
    sleep 1
  done
  stop_tree "$pid"
  rm -f "$PID_FILE"
  echo 'The local API did not pass readiness after restart; inspect api.log.' >&2
  return 1
}

case "${1:-}" in
  stop-business) [[ "$#" == 1 ]] || { echo 'Invalid action arguments.' >&2; exit 2; }; stop_api ;;
  start-business) [[ "$#" == 1 ]] || { echo 'Invalid action arguments.' >&2; exit 2; }; start_api ;;
  status-business) [[ "$#" == 1 ]] || { echo 'Invalid action arguments.' >&2; exit 2; }; tracked_api_ready ;;
  *) echo 'Unsupported local development lifecycle action.' >&2; exit 2 ;;
esac
