#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT/ops/adminer/compose.yaml"
NETWORK="argintaxpayer-adminer-test-db"
DB_CONTAINER="argintaxpayer-devtest-postgres"
DB_ALIAS="argintaxpayer-test-db"

sudo docker inspect "$DB_CONTAINER" >/dev/null || {
  echo "Required test database container is missing: $DB_CONTAINER" >&2
  exit 1
}

if ! sudo docker network inspect "$NETWORK" >/dev/null 2>&1; then
  sudo docker network create "$NETWORK" >/dev/null
fi

if ! sudo docker network inspect "$NETWORK" \
  --format '{{range .Containers}}{{println .Name}}{{end}}' | grep -Fxq "$DB_CONTAINER"; then
  sudo docker network connect --alias "$DB_ALIAS" "$NETWORK" "$DB_CONTAINER"
fi

sudo docker compose --file "$COMPOSE_FILE" up --detach

for attempt in {1..20}; do
  if curl --noproxy '*' --fail --silent --show-error \
    --max-time 2 http://127.0.0.1:18082/ >/dev/null 2>&1; then
    echo 'Adminer ready at http://127.0.0.1:18082 (VPS loopback only).'
    exit 0
  fi
  sleep 1
done

echo 'Adminer did not become ready; inspect its Docker logs.' >&2
sudo docker logs --tail 60 argintaxpayer-adminer >&2 || true
exit 1
