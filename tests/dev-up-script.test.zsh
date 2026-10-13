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

print "dev-up script contract tests passed"
