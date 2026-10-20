#!/usr/bin/env bash
set -Eeuo pipefail
# Phase08 Step21: Ubuntu-only acceptance wrapper; test DB guard lives in ubuntu-dev.sh.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
[[ "$(uname -s)" == "Linux" ]] || { echo "Ubuntu/Linux required" >&2; exit 2; }
[[ -f /etc/os-release ]] && source /etc/os-release
[[ "${ID:-}" == "ubuntu" ]] || { echo "Ubuntu required" >&2; exit 2; }
[[ "$(git branch --show-current)" == "phase/08-invoice-workflows-ui" ]] || {
 echo "Unexpected branch" >&2; exit 2;
}
git diff --quiet && git diff --cached --quiet || { echo "Working tree must be clean" >&2; exit 2; }
for cmd in git curl dotnet npm psql; do command -v "$cmd" >/dev/null || {
 echo "Missing command: $cmd" >&2; exit 2; }; done
STATE="$HOME/.argintaxpayer-test/phase08-step21"
mkdir -p "$STATE"; chmod 700 "$STATE"
STAMP="$(date -u +%Y%m%d-%H%M%S)"
REPORT="$STATE/ubuntu-acceptance-$STAMP.txt"
touch "$REPORT"; chmod 600 "$REPORT"
exec > >(tee -a "$REPORT") 2>&1
printf 'Phase08 Step21 Ubuntu acceptance\nUTC: %s\nCommit: %s\n' "$(date -u +%FT%TZ)" "$(git rev-parse HEAD)"
echo "Compare this SHA with the macOS Step20 accepted commit. Same source commit is required."
echo "[1/3] Ubuntu official test-only database preflight + build and tests"
if bash scripts/ubuntu-dev.sh verify; then
 echo 'RESULT ubuntu regression: PASS'
else
 echo 'RESULT ubuntu regression: FAIL'
 echo "Evidence: $REPORT"; exit 1
fi
echo "[2/3] Ubuntu service readiness (existing services only)"
bash scripts/ubuntu-dev.sh status
echo "[3/3] Read-only HTTP status"
for item in "api|http://127.0.0.1:15089/health/ready" "web|http://127.0.0.1:3000/invoices"; do
 name="${item%%|*}"; url="${item#*|}"
 status="$(curl --noproxy '*' --max-time 8 -s -o /dev/null -w '%{http_code}' "$url" || true)"
 printf '%s HTTP status: %s\n' "$name" "$status"
 if [[ "$status" == "000" || "$status" =~ ^5 ]]; then
  echo "RESULT $name probe: UNAVAILABLE"
 else
  echo "RESULT $name probe: REACHABLE (not session/login proof)"
 fi
done
cat <<'NEXT'
MANUAL UBUNTU CHECKS:
- Same commit SHA as accepted macOS source, record any difference.
- Local API health; browser login/session and role-scoped invoices.
- Saved draft, date/time Jalali, line totals and settlement.
- Revision history, optimistic concurrency and authorization boundaries.
- No live tax submission, external payment or migration.
- Bulk import integration: explicitly DEFERRED by owner, not PASS.
NEXT
echo "Evidence: $REPORT"
echo "Step21 awaits operator/manual acceptance; script output is not signoff."
