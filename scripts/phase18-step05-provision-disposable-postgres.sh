#!/usr/bin/env bash
# Phase18 Step05 — disposable PostgreSQL 17 with private, short-lived TLS CA.
# Creates only a NEW container/volume; never modifies current Postgres or Adminer.
set -Eeuo pipefail
umask 077
fail() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(uname -s)" == Linux ]] || fail 'Ubuntu/Linux required'
[[ "$(id -u)" != 0 ]] || fail 'Run as argindev, not root'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Tracked changes present'
[[ "${PHASE18_CREATE_DISPOSABLE_DB:-}" == YES ]] || fail 'Explicit PHASE18_CREATE_DISPOSABLE_DB=YES required'
for tool in sudo docker openssl psql git ss; do
  command -v "$tool" >/dev/null || fail "Missing: $tool"
done
container=argintaxpayer-phase18-postgres
volume=argintaxpayer_phase18_pgdata
port=15433
out="$PWD/artifacts/phase18-step05-db-infra"
[[ ! -e "$out" ]] || fail "Private directory exists; no overwrite: $out"
sudo docker container inspect "$container" >/dev/null 2>&1 && fail "Container already exists"
sudo docker volume inspect "$volume" >/dev/null 2>&1 && fail "Volume already exists"
ss -lntH | grep -Eq '(^|[.:])15433[[:space:]]' && fail "Port 15433 busy"
mkdir -p "$out/certs"
chmod 700 "$out"; chmod 755 "$out/certs"
printf '[INFO] Generating local CA and hostname-checked TLS certificate\n'
openssl req -x509 -newkey rsa:3072 -nodes -sha256 -days 7 \
  -keyout "$out/ca.key" -out "$out/certs/ca.crt" \
  -subj '/CN=ArginTaxPayer Phase18 Disposable CA' \
  -addext 'basicConstraints=critical,CA:TRUE' \
  -addext 'keyUsage=critical,keyCertSign,cRLSign' > "$out/openssl.log" 2>&1
openssl req -new -newkey rsa:3072 -nodes \
  -keyout "$out/certs/server.key" -out "$out/server.csr" \
  -subj '/CN=localhost' >> "$out/openssl.log" 2>&1
cat > "$out/server-extensions.cnf" <<'EOF'
basicConstraints = critical,CA:FALSE
keyUsage = critical,digitalSignature,keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = DNS:localhost,IP:127.0.0.1
EOF
openssl x509 -req -sha256 -days 7 -in "$out/server.csr" \
  -CA "$out/certs/ca.crt" -CAkey "$out/ca.key" -CAcreateserial \
  -out "$out/certs/server.crt" -extfile "$out/server-extensions.cnf" \
  >> "$out/openssl.log" 2>&1
openssl verify -CAfile "$out/certs/ca.crt" "$out/certs/server.crt" \
  >> "$out/openssl.log" 2>&1 || fail 'TLS certificate verification failed'
chmod 600 "$out/ca.key" "$out/certs/server.key"
chmod 644 "$out/certs/ca.crt" "$out/certs/server.crt"
# Official postgres:17 server UID is 999; bind-mounted private key is readable only there.
sudo chown 999:999 "$out/certs/server.key"
openssl rand -hex 32 > "$out/bootstrap_password"
role_password=$(openssl rand -hex 32)
chmod 600 "$out/bootstrap_password"
printf '[INFO] Creating isolated PostgreSQL 17 on 127.0.0.1:15433\n'
sudo docker volume create "$volume" >/dev/null
sudo docker run -d --name "$container" --restart=no \
  --publish "127.0.0.1:$port:5432" \
  --volume "$volume:/var/lib/postgresql/data" \
  --mount "type=bind,src=$out/certs,dst=/tls,readonly" \
  --mount "type=bind,src=$out/bootstrap_password,dst=/run/secrets/phase18_bootstrap_password,readonly" \
  --env POSTGRES_USER=postgres --env POSTGRES_DB=postgres \
  --env POSTGRES_PASSWORD_FILE=/run/secrets/phase18_bootstrap_password \
  --env POSTGRES_HOST_AUTH_METHOD=scram-sha-256 \
  postgres:17 \
  -c ssl=on -c ssl_cert_file=/tls/server.crt \
  -c ssl_key_file=/tls/server.key \
  -c ssl_ca_file=/tls/ca.crt -c password_encryption=scram-sha-256 \
  > "$out/container-id.txt"
healthy=0
for attempt in {1..60}; do
  if sudo docker exec -u postgres "$container" pg_isready -U postgres -d postgres >/dev/null 2>&1; then
    healthy=1; break
  fi
  sleep 2
done
[[ "$healthy" == 1 ]] || fail 'Container not ready; review isolated container log'
{
  printf "CREATE ROLE phase18_acceptance LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '%s';\n" "$role_password"
  printf 'CREATE DATABASE argin_phase18_acceptance OWNER phase18_acceptance;\n'
  printf 'REVOKE CONNECT ON DATABASE postgres FROM PUBLIC;\n'
} | sudo docker exec -i -u postgres "$container" \
    psql -X -q -v ON_ERROR_STOP=1 -U postgres -d postgres \
    > "$out/init.log" 2>&1 || fail 'Disposable role/DB initialization failed'
connection="Host=localhost;Port=$port;Database=argin_phase18_acceptance;Username=phase18_acceptance;Password=$role_password;SSL Mode=VerifyFull;Root Certificate=$out/certs/ca.crt;Timeout=5;Command Timeout=30"
{
  printf 'export PHASE18_DISPOSABLE_DB_CONNECTION=%q\n' "$connection"
  printf 'export RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1\n'
  printf 'export PHASE18_PG_HOST=localhost\n'
  printf 'export PHASE18_PG_PORT=15433\n'
  printf 'export PHASE18_PG_DATABASE=argin_phase18_acceptance\n'
  printf 'export PHASE18_PG_USER=phase18_acceptance\n'
  printf 'export PHASE18_PG_SSL_ROOT=%q\n' "$out/certs/ca.crt"
  printf 'export PHASE18_PG_PASSWORD=%q\n' "$role_password"
} > "$out/connection.env"
chmod 600 "$out/connection.env"
PGHOST=localhost PGPORT="$port" PGUSER=phase18_acceptance \
  PGDATABASE=argin_phase18_acceptance PGPASSWORD="$role_password" \
  PGSSLMODE=verify-full PGSSLROOTCERT="$out/certs/ca.crt" \
  psql -X -Atq -v ON_ERROR_STOP=1 \
    -c "SELECT current_database() || '|' || current_user || '|' || ssl::text FROM pg_stat_ssl WHERE pid = pg_backend_pid()" \
    > "$out/tls-check.txt" || fail 'Verified TLS login failed'
[[ "$(cat "$out/tls-check.txt")" == 'argin_phase18_acceptance|phase18_acceptance|true' ]] \
  || fail 'Database, identity or TLS verification mismatch'
printf '[PASS] New limited-role PostgreSQL database; TLS VerifyFull passed\n'
printf '[INFO] Private config: %s/connection.env — do not paste its contents\n' "$out"
printf '[INFO] No migrations or data have been applied yet\n'
