#!/usr/bin/env bash
# Official postgres entrypoint only. Runs once on a NEW empty named volume.
set -Eeuo pipefail
: "${PHASE18_PROD_MIGRATOR_PASSWORD:?missing}"
: "${PHASE18_PROD_APP_PASSWORD:?missing}"
export PGDATABASE=postgres
psql --username "$POSTGRES_USER" -X -v ON_ERROR_STOP=1 \
  --set=migrator_pass="$PHASE18_PROD_MIGRATOR_PASSWORD" \
  --set=app_pass="$PHASE18_PROD_APP_PASSWORD" <<'SQL'
CREATE ROLE argin_prod_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD :'migrator_pass';
CREATE ROLE argin_prod_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD :'app_pass';
CREATE DATABASE argintaxpayer_prod OWNER argin_prod_migrator;
REVOKE ALL ON DATABASE argintaxpayer_prod FROM PUBLIC;
GRANT CONNECT ON DATABASE argintaxpayer_prod TO argin_prod_app;
SQL
psql --username "$POSTGRES_USER" -X -v ON_ERROR_STOP=1 -d argintaxpayer_prod <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO argin_prod_app;
-- Migrator owns schema objects and grants access to application on creation.
ALTER DEFAULT PRIVILEGES FOR ROLE argin_prod_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO argin_prod_app;
ALTER DEFAULT PRIVILEGES FOR ROLE argin_prod_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO argin_prod_app;
SQL
# TCP requires TLS: official entrypoint restarts PostgreSQL after these scripts.
# Insert rejection rules BEFORE any permissive IPv4/IPv6 host auth.
sed -i '1i hostnossl all all ::/0 reject' "$PGDATA/pg_hba.conf"
sed -i '1i hostnossl all all 0.0.0.0/0 reject' "$PGDATA/pg_hba.conf"
