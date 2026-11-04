#!/usr/bin/env bash
# Phase18 Step05: create a NEW disposable, TLS-enabled PostgreSQL on Ubuntu.
# Does not touch existing postgres-postgres-1, Adminer, Nginx, or system settings.
set -Eeuo pipefail
umask 077
fail() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(uname -s)" == Linux ]] || fail 'Run on Ubuntu VPS only'
[[ "$(id -u)" != 0 ]] || fail 'Run as argindev, not root'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Tracked worktree must be clean'
for x in openssl sudo git ss; do command -v "$x" >/dev/null || fail "Missing $x"; done
sudo -v || fail 'sudo permission is required for Docker and secure key ownership'
sudo docker compose version >/dev/null || fail 'Docker Compose v2 is required'
[[ -z "$(ss -ltnH | awk '{print $4}' | grep -E '(^|[.:])15433$' || true)" ]] || fail 'TCP port 15433 already used'
root="$PWD/artifacts/phase18-step05-db/isolated"
[[ ! -e "$root" ]] || fail "Isolation directory already exists: $root. Inspect it; do not overwrite."
# Verify the new Compose project is not already running. Never use down -v automatically.
if sudo docker ps -a --format '{{.Names}}' | grep -Fxq 'argintaxpayer-phase18-postgres'; then
  fail 'Dedicated QA container already exists; inspect manually before proceeding'
fi
mkdir -p "$root/ca" "$root/server"
chmod 700 "$root" "$root/ca"
# TLS mount directory must be traversable by the postgres container UID.
chmod 755 "$root/server"
printf '[START] Generate new private CA and 127.0.0.1 TLS server certificate\n'
openssl req -x509 -newkey rsa:3072 -sha256 -nodes -days 30 \
  -subj '/CN=ArginTaxPayer Phase18 Ephemeral Test CA' \
  -keyout "$root/ca/ca.key" -out "$root/ca/ca.pem" > /dev/null 2>&1
openssl req -new -newkey rsa:3072 -sha256 -nodes \
  -subj '/CN=localhost' \
  -keyout "$root/server/server.key" -out "$root/server/server.csr" > /dev/null 2>&1
printf 'subjectAltName=DNS:localhost,IP:127.0.0.1\nextendedKeyUsage=serverAuth\n' > "$root/server/server.ext"
openssl x509 -req -sha256 -days 14 \
  -in "$root/server/server.csr" -CA "$root/ca/ca.pem" -CAkey "$root/ca/ca.key" \
  -CAcreateserial -extfile "$root/server/server.ext" \
  -out "$root/server/server.crt" > /dev/null 2>&1
openssl verify -CAfile "$root/ca/ca.pem" "$root/server/server.crt" >/dev/null ||
  fail 'Certificate verification failed'
openssl x509 -in "$root/server/server.crt" -noout -ext subjectAltName |
  grep -q '127.0.0.1' || fail 'Certificate lacks loopback SAN'
rm -f "$root/server/server.csr" "$root/server/server.ext"
chmod 600 "$root/server/server.key"
chmod 644 "$root/server/server.crt"
# postgres:17 currently runs its postgres account as UID/GID 999. This chown
# changes only files in this newly generated disposable directory.
sudo chown 999:999 "$root/server/server.key" "$root/server/server.crt"
bootstrap=$(openssl rand -hex 32)
dbpass=$(openssl rand -hex 32)
cat > "$root/compose.env" <<EOF
PHASE18_BOOTSTRAP_PASSWORD=$bootstrap
PHASE18_DB_PASSWORD=$dbpass
PHASE18_TLS_DIR=$root/server
EOF
chmod 600 "$root/compose.env"
unset bootstrap dbpass
printf '[START] Isolated PostgreSQL 17 on 127.0.0.1:15433\n'
sudo docker compose \
  --env-file "$root/compose.env" \
  -f ops/phase18-disposable-db/compose.yaml \
  up -d
echo '[INFO] Container started; use dedicated status/migration script after healthy.'
echo "[INFO] Private env: $root/compose.env (never paste its contents)"
echo "[INFO] CA certificate: $root/ca/ca.pem"
echo '[SAFE] No existing DB containers or system services were modified.'
