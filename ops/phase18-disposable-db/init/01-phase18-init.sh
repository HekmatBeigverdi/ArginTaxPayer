#!/usr/bin/env bash
# Executed by postgres official entrypoint ONLY for a brand-new empty volume.
set -Eeuo pipefail
: "${PHASE18_DB_PASSWORD:?missing}"
export PGDATABASE=postgres
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" \
    --set=acceptance_password="$PHASE18_DB_PASSWORD" <<'SQL'
CREATE ROLE phase18_acceptance LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD :'acceptance_password';
CREATE DATABASE argin_phase18_acceptance OWNER phase18_acceptance;
SQL
