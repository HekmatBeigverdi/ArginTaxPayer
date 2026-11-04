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
for t in sudo openssl install mktemp stat; do command -v "$t" >/dev/null || fail "Missing $t"; done
sudo -v
# Never overwrite an existing config or replace an existing signing/encryption secret.
for f in /etc/argintaxpayer/api.env /etc/argintaxpayer/web.env; do
  if sudo test -e "$f"; then fail "Existing private config; refuse overwrite: $f"; fi
done
# Secret values intentionally are NOT invented here. Authentication/signing
# material must be explicitly supplied and reviewed under Step08's offline
# acceptance before the operational opt-in gate is ever considered.
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
cat > "$tmp/api.env" <<'EOF'
# Phase18 Step08: disabled-by-default; only a SAFE skeleton, NOT go-live ready.
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:15090
ArginTaxPayer__EnableOperationalEndpoints=false
Ghasedak__EnableLiveSending=false
SandboxInvoiceLegacy__Enabled=false
SandboxInvoiceLegacy__OperatorApprovedEndpoint=false
Phase11__EnableReadOnlyQueueObserver=false
Identity__AllowDevelopmentRegistration=false
# No connection string, passwords, cryptographic keys or live credentials present.
# Populate separately under secure custody only after specific type/format review.
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
sudo install -d -o root -g root -m 0700 /etc/argintaxpayer
sudo install -o root -g root -m 0600 -T "$tmp/api.env" /etc/argintaxpayer/api.env
sudo install -o root -g root -m 0600 -T "$tmp/web.env" /etc/argintaxpayer/web.env
for f in /etc/argintaxpayer/api.env /etc/argintaxpayer/web.env; do
  [[ "$(sudo stat -c %U:%G:%a "$f")" == root:root:600 ]] ||
    fail 'Unexpected ownership or mode after config staging'
done
echo '[PASS] Root-only 0600 configuration skeleton staged without secrets.'
echo '[BLOCKED] Runtime database connection and crypto custody NOT configured; operational endpoints OFF.'
echo '[BLOCKED] No systemd activation, tax authority, SMS, payments or Nginx changes.'
