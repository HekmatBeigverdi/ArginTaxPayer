#!/usr/bin/env bash
# Phase18 Step07: explicitly provision ONE new, isolated PostgreSQL 17 instance.
# Never touch the shared 5432 DB, Phase18 QA 15433, Nginx or systemd units.
set -Eeuo pipefail
umask 077
fail(){ printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "${1:-}" == "--provision-isolated-production-db" && "$#" == 1 ]] ||
  fail 'Explicit --provision-isolated-production-db required'
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Run as argindev on Ubuntu'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Tracked files modified'
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[0-9a-f]{40}$ &&
   "$(git rev-parse HEAD)" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'Full independently verified macOS SHA is required'
for t in sudo docker openssl psql ss git stat; do command -v "$t" >/dev/null || fail "Missing $t"; done
sudo -v || fail 'Privileged Docker permission required'
sudo docker compose version >/dev/null || fail 'Docker Compose v2 required'
# Pin the already-reviewed local PostgreSQL 17 image by immutable registry digest.
# Never use a floating tag for the Production DB container.
image_digest=$(sudo docker image inspect postgres:17 --format '{{index .RepoDigests 0}}' 2>/dev/null || true)
[[ "$image_digest" =~ ^postgres(@sha256:)[a-f0-9]{64}$ ]] ||
  fail 'Missing reviewed local postgres:17 image digest; pull and review PostgreSQL 17 image separately'

root="$PWD/artifacts/phase18-step07/production-private"
[[ ! -e "$root" ]] || fail 'Production secrets directory already exists; refuse overwrite'
if sudo docker container inspect argintaxpayer-phase18-production-postgres >/dev/null 2>&1; then
  fail 'Dedicated container already exists'
fi
if sudo docker volume inspect argintaxpayer-phase18-production_prod_pgdata >/dev/null 2>&1; then
  fail 'Dedicated production volume already exists'
fi
if ss -lntH | awk '{print $4}' | grep -Eq ':15434$'; then
  fail 'Private PostgreSQL port 15434 occupied'
fi
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  if systemctl is-active --quiet "$unit"; then fail "$unit active: refuse DB provisioning"; fi
done
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] ||
  fail 'Production service activation already permitted; review manually'
mkdir -p "$root/ca" "$root/server" "$root/reports"
chmod 700 "$root" "$root/ca" "$root/reports"
chmod 755 "$root/server"
# Separately owned CA/cert for this instance; never reuse QA certificate.
openssl req -x509 -newkey rsa:3072 -sha256 -nodes -days 365 \
  -subj '/CN=ArginTaxPayer Dedicated Production DB Local CA' \
  -addext 'basicConstraints=critical,CA:TRUE' \
  -addext 'keyUsage=critical,keyCertSign,cRLSign' \
  -keyout "$root/ca/ca.key" -out "$root/ca/ca.pem" > "$root/reports/tls-generation.log" 2>&1
openssl req -new -newkey rsa:3072 -sha256 -nodes \
  -subj '/CN=localhost' \
  -keyout "$root/server/server.key" -out "$root/server/server.csr" >> "$root/reports/tls-generation.log" 2>&1
cat > "$root/server/server.ext" <<'EOF'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:localhost,IP:127.0.0.1
EOF
openssl x509 -req -sha256 -days 180 \
  -in "$root/server/server.csr" -CA "$root/ca/ca.pem" -CAkey "$root/ca/ca.key" \
  -CAcreateserial -extfile "$root/server/server.ext" \
  -out "$root/server/server.crt" >> "$root/reports/tls-generation.log" 2>&1
openssl verify -CAfile "$root/ca/ca.pem" "$root/server/server.crt" > /dev/null ||
  fail 'TLS certificate verification failed'
openssl x509 -in "$root/server/server.crt" -noout -ext subjectAltName |
  grep -Fq '127.0.0.1' || fail 'Missing loopback certificate SAN'
rm -f "$root/server/server.csr" "$root/server/server.ext"
chmod 600 "$root/ca/ca.key" "$root/server/server.key"
chmod 644 "$root/server/server.crt"
# The official PostgreSQL 17 container account owns the TLS private key.
pg_uid=$(sudo docker run --rm --entrypoint id "$image_digest" -u postgres)
[[ "$pg_uid" =~ ^[0-9]+$ ]] || fail 'Unrecognized postgres container UID'
sudo chown "$pg_uid:$pg_uid" "$root/server/server.key"
bootstrap=$(openssl rand -hex 32)
migrator=$(openssl rand -hex 32)
app=$(openssl rand -hex 32)
cat > "$root/compose.env" <<EOF
PHASE18_PROD_BOOTSTRAP_PASSWORD=$bootstrap
PHASE18_PROD_MIGRATOR_PASSWORD=$migrator
PHASE18_PROD_APP_PASSWORD=$app
PHASE18_PROD_TLS_DIR=$root/server
PHASE18_PROD_POSTGRES_IMAGE=$image_digest
EOF
chmod 600 "$root/compose.env"
unset bootstrap migrator app image_digest
# Only the dedicated project/compose file is addressed; never run compose down.
sudo docker compose --env-file "$root/compose.env" \
  -f ops/phase18-production-db/compose.yaml up -d
ready=0
for i in {1..60}; do
  if sudo docker exec -u postgres argintaxpayer-phase18-production-postgres \
    pg_isready -U argin_prod_migrator -d argintaxpayer_prod >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 2
done
[[ "$ready" == 1 ]] || fail 'Isolated production database did not become ready'
migrator=$(sed -n 's/^PHASE18_PROD_MIGRATOR_PASSWORD=//p' "$root/compose.env")
[[ "$migrator" =~ ^[0-9a-f]{64}$ ]] || fail 'Missing migrator credential'
export PGPASSWORD="$migrator" PGSSLMODE=verify-full PGSSLROOTCERT="$root/ca/ca.pem"
proof=$(psql -h 127.0.0.1 -p 15434 -U argin_prod_migrator -d argintaxpayer_prod -X -At \
  -v ON_ERROR_STOP=1 -c "SELECT current_database(),current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()),(SELECT rolsuper FROM pg_roles WHERE rolname=current_user),(SELECT rolcreatedb FROM pg_roles WHERE rolname=current_user);" 2>/dev/null) ||
  fail 'TLS VerifyFull production database login failed'
[[ "$proof" == 'argintaxpayer_prod|argin_prod_migrator|t|f|f' ]] ||
  fail 'Production database identity, TLS or privilege mismatch'
unset migrator PGPASSWORD PGSSLMODE PGSSLROOTCERT
echo '[PASS] Dedicated, TLS VerifyFull PostgreSQL 17 production instance provisioned.'
echo '[PASS] Port 127.0.0.1:15434, separate container, volume, migrator and app roles.'
echo '[BLOCKED] No migrations, application services, DNS, Nginx or authority calls.'
echo '[NOTE] Private credentials/CA under artifacts/phase18-step07/production-private (never paste/upload).'
