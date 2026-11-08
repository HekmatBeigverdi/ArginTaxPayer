#!/usr/bin/env bash
# Phase18 Step05 — run database fixture after explicit provisioning and migrations.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
fail() { echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux ]] || fail 'Run on Ubuntu only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked files'
config="$PWD/artifacts/phase18-step05-db-infra/connection.env"
[[ -f "$config" && ! -L "$config" ]] || fail 'Missing private isolated DB connection'
[[ "$(stat -c '%a' "$config")" == 600 ]] || fail 'Private connection file mode must be 600'
source "$config"
[[ "$PHASE18_PG_HOST" == localhost && "$PHASE18_PG_PORT" == 15433 &&
   "$PHASE18_PG_DATABASE" == argin_phase18_acceptance &&
   "$PHASE18_PG_USER" == phase18_acceptance ]] || fail 'Unexpected database endpoint'
[[ "${RUN_ARGINTAXPAYER_PG_INTEGRATION:-}" != 1 ]] || fail 'Turn off shared DB integration opt-in'
export RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1
bash scripts/phase18-step05-disposable-db-acceptance.sh
