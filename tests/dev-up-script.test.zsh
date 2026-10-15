#!/bin/zsh
set -e

root="${0:A:h:h}"
script="$root/scripts/dev-up.zsh"
source_text="$(< "$script")"

assert_contains() {
  local label="$1"
  local needle="$2"
  if [[ "$source_text" != *"$needle"* ]]; then
    print -u2 "FAIL: $label"
    exit 1
  fi
}

assert_contains "preflight uses PostgreSQL client" "psql"
assert_contains "preflight verifies the exact test database" "SELECT current_database()"
assert_contains "tunnel startup has a retry window" "tunnel_ready=0"
assert_contains "auth failure offers a private password retry" "PostgreSQL test password"
assert_contains "startup failures trigger cleanup" "trap cleanup_on_failure EXIT"
assert_contains "cleanup stops child processes" "stop_pid_tree"

down_script="$root/scripts/dev-down.zsh"
down_source="$(< "$down_script")"
if [[ "$down_source" != *"lsof -nP -t -iTCP:\$port -sTCP:LISTEN"* ]]; then
  print -u2 "FAIL: dev-down falls back to tracked service listeners"
  exit 1
fi
if [[ "$down_source" != *"ArginTaxPayer.Api"* || "$down_source" != *"next-server"* ||
      "$down_source" != *"autossh"* ]]; then
  print -u2 "FAIL: dev-down validates service process identity"
  exit 1
fi

print "dev-up script contract tests passed"
