#!/usr/bin/env bash
# Step08: one-time OFFLINE root-only configuration staging. No service starts.
# Does NOT generate/rotate fiscal RSA keys or activate tax, SMS or payments.
set -Eeuo pipefail
umask 077
fail(){ printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "${1:-}" == --stage-disabled-private-config && "$#" == 1 ]] ||
  fail 'Explicit --stage-disabled-private-config required'
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot operator only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Dirty tracked source'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[a-f0-9]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'Exact independently verified macOS SHA required'
[[ "${PHASE18_OWNER_APPROVED_CONFIG_STAGING:-}" == YES ]] ||
  fail 'Owner approval for writing NEW private config required'
for unit in argintaxpayer-api.service argintaxpayer-web.service; do
  if systemctl is-active --quiet "$unit"; then fail "Service active: $unit"; fi
done
[[ ! -e /etc/argintaxpayer/phase18-activation-approved ]] || fail 'Go-live marker present'
[[ -f "$PWD/artifacts/phase18-step07/production-private/compose.env" &&
    -f "$PWD/artifacts/phase18-step07/production-private/ca/ca.pem" ]] ||
  fail 'Step07 dedicated DB secrets or CA missing'
for t in sudo openssl install mktemp stat sed getent; do command -v "$t" >/dev/null || fail "Missing $t"; done
sudo -v
getent group argintaxsvc >/dev/null || fail 'Dedicated argintaxsvc group missing; do not stage yet'
# Never overwrite an existing config or replace an existing signing/encryption secret.
for f in /etc/argintaxpayer/api.env /etc/argintaxpayer/web.env /etc/argintaxpayer/prod-db-ca.pem; do
  if sudo test -e "$f"; then fail "Existing private config; refuse overwrite: $f"; fi
done
# Only two source-verified 32-byte AES keys are generated for this NEW DB.
# Never replace them after ciphertext has been created. Real authority, payment
# and SMS provider secrets are deliberately absent.
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
dbpass=$(sed -n 's/^PHASE18_PROD_APP_PASSWORD=//p' "$PWD/artifacts/phase18-step07/production-private/compose.env")
[[ "$dbpass" =~ ^[a-f0-9]{64}$ ]] || fail 'Missing dedicated least-privileged DB app credential'
# Generate once, in memory only, and write to root-owned private environment file.
# BOTH keys are AES-256 32-byte base64, as required by the current source.
mfa_key=$(openssl rand -base64 32 | tr -d '\n')
wrapping_key=$(openssl rand -base64 32 | tr -d '\n')
[[ -n "$mfa_key" && -n "$wrapping_key" && "$mfa_key" != "$wrapping_key" ]] || fail 'Failed to generate independent keys'
cat > "$tmp/api.env" <<EOF
# Phase18 Step08: private stable keys for NEW isolated Production DB, go-live still blocked.
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:15090
ArginTaxPayer__PublicBaseUrl=https://tax.argin.ir
ArginTaxPayer__EnableOperationalEndpoints=false
Ghasedak__EnableLiveSending=false
SandboxInvoiceLegacy__Enabled=false
SandboxInvoiceLegacy__OperatorApprovedEndpoint=false
Phase11__EnableReadOnlyQueueObserver=false
Identity__AllowDevelopmentRegistration=false
Identity__MfaEncryptionKey=$mfa_key
SigningKeyProtection__Enabled=true
SigningKeyProtection__Provider=Database
SigningKeyProtection__DatabaseMasterKey=$wrapping_key
ConnectionStrings__ArginTaxPayerDb=Host=127.0.0.1;Port=15434;Database=argintaxpayer_prod;Username=argin_prod_app;Password=$dbpass;SSL Mode=VerifyFull;Root Certificate=/etc/argintaxpayer/prod-db-ca.pem;Timeout=5
# API routes disabled; no real provider secrets or dispatch approvals staged.
EOF
cat > "$tmp/web.env" <<'EOF'
NODE_ENV=production
PORT=15091
HOSTNAME=127.0.0.1
ARGIN_PUBLIC_BASE_URL=https://tax.argin.ir
ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15090
ARGIN_IDENTITY_UI_ENABLED=false
ARGIN_PRODUCTION_UI_ENABLED=false
EOF
sudo install -d -o root -g argintaxsvc -m 0750 /etc/argintaxpayer
# Service must read public CA despite its ProtectHome=true restriction.
# Only the CA is group-readable; root-only api.env and web.env remain 0600.
sudo install -o root -g argintaxsvc -m 0640 -T "$PWD/artifacts/phase18-step07/production-private/ca/ca.pem" /etc/argintaxpayer/prod-db-ca.pem
sudo install -o root -g root -m 0600 -T "$tmp/api.env" /etc/argintaxpayer/api.env
sudo install -o root -g root -m 0600 -T "$tmp/web.env" /etc/argintaxpayer/web.env
for f in /etc/argintaxpayer/api.env /etc/argintaxpayer/web.env; do
  [[ "$(sudo stat -c %U:%G:%a "$f")" == root:root:600 ]] ||
    fail 'Unexpected ownership or mode after config staging'
done
unset dbpass mfa_key wrapping_key
echo '[PASS] One-time root-only 0600 environment staged with dedicated DB app credential and two independent persistent 32-byte keys.'
echo '[NOTE] Preserve both encryption keys across every restart/deploy; Step12 requires independently escrowed encrypted recovery.'
echo '[BLOCKED] No systemd activation, tax authority, SMS, payments or Nginx changes.'
