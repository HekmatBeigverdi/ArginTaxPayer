#!/usr/bin/env bash
set -Eeuo pipefail
# Phase08 Step20: macOS operator gate. Does not execute migrations or import writes.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "ERROR: Step20 is macOS-only." >&2
  exit 2
fi
if [[ "$(git branch --show-current)" != "phase/08-invoice-workflows-ui" ]]; then
  echo "ERROR: wrong Git branch." >&2
  exit 2
fi
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "ERROR: uncommitted changes. Preserve them before operator acceptance." >&2
  exit 2
fi
for tool in git curl dotnet npm psql zsh; do
  command -v "$tool" >/dev/null || { echo "ERROR: missing $tool" >&2; exit 2; }
done
STATE="$HOME/.argintaxpayer-test/phase08-step20"
mkdir -p "$STATE"
chmod 700 "$STATE"
STAMP="$(date +%Y%m%d-%H%M%S)"
REPORT="$STATE/mac-acceptance-$STAMP.txt"
touch "$REPORT"
chmod 600 "$REPORT"
exec > >(tee -a "$REPORT") 2>&1

printf 'Phase08 Step20 macOS acceptance evidence\n'
printf 'Timestamp: %s\nBranch: %s\nCommit: %s\n' "$(date -u +%FT%TZ)" "$(git branch --show-current)" "$(git rev-parse HEAD)"
printf 'OS: %s\n' "$(sw_vers -productVersion)"
printf '\n[1/3] Source and regression suite\n'
if bash scripts/phase08-step19-verify.sh; then
  echo 'RESULT source regression: PASS'
else
  echo 'RESULT source regression: FAIL'
  echo "Evidence file: $REPORT"
  exit 1
fi

printf '\n[2/3] Development services (never starts or restarts automatically)\n'
zsh scripts/dev-status.zsh
printf '\n[3/3] Local read-only HTTP probes\n'
for item in "api|http://127.0.0.1:15089/health/ready" "web|http://127.0.0.1:3000/invoices"; do
  name="${item%%|*}"
  url="${item#*|}"
  code="$(curl --noproxy '*' --max-time 8 -s -o /dev/null -w '%{http_code}' "$url" || true)"
  printf '%s HTTP status: %s\n' "$name" "$code"
  if [[ "$code" == "000" || "$code" =~ ^5 ]]; then
    echo "RESULT $name probe: FAIL / service not ready"
  else
    echo "RESULT $name probe: REACHABLE (not proof of authenticated session)"
  fi
done

cat <<'NEXT'

MANUAL BROWSER ACCEPTANCE — operator must record results:
  A. Login/MFA, profile and tenant/user selection.
  B. Invoice list, filters/search, customer-level scoping.
  C. Start invoice, select taxpayer and buyer, Jalali date/time.
  D. Add 20 lines; compact table scroll, edit/delete, totals and settlement.
  E. Save draft, reopen, revise, stale revision conflict from second tab.
  F. Verify history newest-first, Jalali timestamps, actor display names.
  G. Validate required pattern fields and review final invoice summary.
  H. Confirm no invoice issuance/signing or compulsory admin approval.
  I. Step18 bulk CSV/XLSX import: DEFERRED by owner; not PASS.
  J. Record database identity separately before any future database mutation.
NEXT

printf '\nEvidence file: %s\n' "$REPORT"
printf 'Step20 is NOT approved until manual outcomes and platform results are confirmed.\n'
