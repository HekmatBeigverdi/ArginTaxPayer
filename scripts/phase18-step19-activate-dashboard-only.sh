#!/usr/bin/env bash
# Opt-in ONLY: enable login + read-only dashboard on an ALREADY deployed exact SHA.
# Does NOT deploy binaries, migrate DB, enable SMS, payments, tax authority,
# Sandbox or Production dispatch. Rolls back both root-only env files on failure.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "${1:-}" == "--activate-dashboard-only" && "$#" == 1 ]] ||
  fail 'Usage: --activate-dashboard-only'
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot operator required'
[[ "${PHASE18_OWNER_APPROVED_DASHBOARD_ONLY:-}" == YES ]] ||
  fail 'Explicit dashboard-only activation approval required'
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] ||
  fail 'Wrong source branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
  fail 'Dirty tracked source'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" == "$sha" ]] ||
  fail 'Exact matching approved SHA required'
for cmd in sudo python3 nginx curl systemctl; do
  command -v "$cmd" >/dev/null || fail "Missing $cmd"
done
sudo -v
sudo test -f /etc/argintaxpayer/phase18-activation-approved ||
  fail 'Activation marker not present'
current=$(sudo readlink -f /opt/argintaxpayer/current) ||
  fail 'No deployed current release'
[[ "$current" == "/opt/argintaxpayer/releases/$sha" ]] ||
  fail "Active deployed release is NOT source SHA; stage and promote reviewed version first"
sudo grep -Fxq "sha=$sha" "$current/build-info.txt" ||
  fail 'Deployed build-info SHA mismatch'
sudo nginx -t >/dev/null 2>&1 || fail 'Nginx validation must PASS'
systemctl is-active --quiet argintaxpayer-api.service ||
  fail 'API not running'
systemctl is-active --quiet argintaxpayer-web.service ||
  fail 'Web not running'
for url in http://127.0.0.1:15090/health/live http://127.0.0.1:15090/health/ready; do
  curl --noproxy '*' -fsS -o /dev/null --max-time 6 "$url" ||
    fail 'Initial API health not ready'
done
api=/etc/argintaxpayer/api.env
web=/etc/argintaxpayer/web.env
for f in "$api" "$web"; do
  sudo test -f "$f" && [[ "$(sudo stat -c %a "$f")" == 600 ]] ||
    fail "Missing/unsafe config mode: $f"
done
sudo grep -Fxq 'ArginTaxPayer__EnableOperationalEndpoints=false' "$api" ||
  fail 'Global operational API already enabled'
sudo grep -Fxq 'SandboxInvoiceLegacy__Enabled=false' "$api" ||
  fail 'Sandbox dispatch must remain off'
sudo grep -Fxq 'Ghasedak__EnableLiveSending=false' "$api" ||
  fail 'SMS must remain off for dashboard-only'
if sudo grep -Fxq 'ProductionInvoiceLegacy__Enabled=true' "$api"; then
  fail 'Production authority dispatch must remain off'
fi
sudo grep -Fxq 'Identity__AllowDevelopmentRegistration=false' "$api" ||
  fail 'Registration must be off'
if sudo grep -Fxq 'Identity__OtpDeliveryMode=LocalFile' "$api"; then
  fail 'LocalFile OTP is not permitted in Production'
fi
sudo grep -Fxq 'ARGIN_PRODUCTION_UI_ENABLED=false' "$web" ||
  fail 'Production UI already enabled; use normal rollout review'
sudo grep -Fxq 'ARGIN_IDENTITY_UI_ENABLED=false' "$web" ||
  fail 'Identity UI already enabled; use normal rollout review'

# Keep last-known-good private settings for immediate rollback.
stamp=$(date -u +%Y%m%dT%H%M%SZ)
backupdir="/etc/argintaxpayer/phase18-step19-backup-$stamp"
sudo install -d -o root -g root -m 0700 "$backupdir"
sudo cp -p -- "$api" "$backupdir/api.env"
sudo cp -p -- "$web" "$backupdir/web.env"
rollback(){
  echo '[FAIL] Dashboard-only smoke failed; restoring previous configurations.' >&2
  sudo cp -p -- "$backupdir/api.env" "$api" || true
  sudo cp -p -- "$backupdir/web.env" "$web" || true
  sudo systemctl restart argintaxpayer-api.service || true
  sudo systemctl restart argintaxpayer-web.service || true
}
trap rollback ERR
sudo python3 - "$api" "$web" <<'PY'
import os, pathlib, stat, sys, tempfile

def replace(path, replacements):
    f = pathlib.Path(path)
    content = f.read_text(encoding='utf-8').splitlines()
    for key, value in replacements.items():
        matched = [i for i, line in enumerate(content) if line.startswith(key + '=')]
        if len(matched) > 1:
            raise RuntimeError('duplicate setting: ' + key)
        if matched:
            content[matched[0]] = key + '=' + value
        else:
            content.append(key + '=' + value)
    fd, name = tempfile.mkstemp(dir=str(f.parent), prefix='.phase18-step19-')
    try:
        with os.fdopen(fd, 'w', encoding='utf-8') as dest:
            dest.write('\n'.join(content) + '\n')
            dest.flush()
            os.fsync(dest.fileno())
        os.chmod(name, stat.S_IRUSR | stat.S_IWUSR)
        os.chown(name, 0, 0)
        os.replace(name, f)
    finally:
        if os.path.exists(name):
            os.unlink(name)

replace(sys.argv[1], {
    'ArginTaxPayer__EnableIdentityDashboardOnly': 'true',
})
replace(sys.argv[2], {
    'ARGIN_PRODUCTION_UI_ENABLED': 'true',
    'ARGIN_IDENTITY_UI_ENABLED': 'true',
    'ARGIN_IDENTITY_DASHBOARD_ONLY': 'true',
})
PY
sudo systemctl restart argintaxpayer-api.service
sudo systemctl restart argintaxpayer-web.service
for _ in {1..20}; do
  api_live=$(curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 3 http://127.0.0.1:15090/health/live || true)
  api_ready=$(curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 3 http://127.0.0.1:15090/health/ready || true)
  identity=$(curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 3 http://127.0.0.1:15091/identity || true)
  auth=$(curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 3 http://127.0.0.1:15090/api/dev/identity/authorization/me || true)
  blocked=$(curl --noproxy '*' -s -o /dev/null -w '%{http_code}' --max-time 3 http://127.0.0.1:15090/api/dev/invoices || true)
  if [[ "$api_live" == 200 && "$api_ready" == 200 && "$identity" == 200 &&
        "$auth" == 401 && "$blocked" == 404 ]]; then
    trap - ERR
    echo "[PASS] Dashboard-only identity enabled at exact deployed SHA $sha"
    echo '[PASS] API live/ready, login view, anonymous identity rejection and operational endpoint isolation'
    echo "[INFO] Root-only rollback snapshot: $backupdir"
    echo '[BLOCKED] Fiscal dispatch and live SMS remain disabled.'
    exit 0
  fi
  sleep 2
done
false # activates ERR trap/rollback
