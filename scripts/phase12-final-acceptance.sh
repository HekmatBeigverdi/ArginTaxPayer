#!/usr/bin/env bash
# Phase 12 final cross-platform acceptance. NO migrations, writes, provider traffic,
# tax authority submission, or production credentials.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
umask 077
case "$(uname -s)" in
  Darwin) platform=macos ;;
  Linux) . /etc/os-release; [[ "${ID:-}" == ubuntu ]] || { echo "Ubuntu only" >&2; exit 2; }; platform=ubuntu ;;
  *) echo "Unsupported system" >&2; exit 2 ;;
esac
[[ "$(git branch --show-current)" == "phase/12-subscriptions-billing-settlements" ]] ||
  { echo "REFUSED: wrong branch" >&2; exit 2; }
[[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
  { echo "REFUSED: tracked files modified" >&2; exit 2; }
for exe in git dotnet npm node; do
  command -v "$exe" >/dev/null || { echo "Missing $exe" >&2; exit 2; }
done
sha="$(git rev-parse HEAD)"
if [[ -n "${PHASE12_EXPECT_SHA:-}" && "$sha" != "$PHASE12_EXPECT_SHA" ]]; then
  echo "REFUSED: SHA does not match PHASE12_EXPECT_SHA" >&2; exit 2
fi
evidence="artifacts/phase12-final/$sha/$platform-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$evidence"
printf 'check\tstatus\tlog\n' > "$evidence/manifest.tsv"
fail=0
run(){
  local name="$1"; shift
  echo "[START] $name"
  if "$@" >"$evidence/$name.log" 2>&1; then
    echo "[PASS] $name"
    printf '%s\tPASS\t%s\n' "$name" "$evidence/$name.log" >>"$evidence/manifest.tsv"
  else
    local code="$?"
    fail=$((fail+1))
    echo "[FAIL] $name exit=$code"
    tail -n 35 "$evidence/$name.log" || true
    printf '%s\tFAIL(%s)\t%s\n' "$name" "$code" "$evidence/$name.log" >>"$evidence/manifest.tsv"
  fi
}
run dotnet-restore dotnet restore ArginTaxPayer.sln
run dotnet-build dotnet build ArginTaxPayer.sln -c Release --no-restore
run dotnet-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
run phase12-approval-policy dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~Phase12CommercialActivationApprovalPolicyTests
if [[ -d apps/web/node_modules ]]; then
  run web-tests bash -c 'cd apps/web && npm test'
  run web-typecheck bash -c 'cd apps/web && npm run typecheck'
  run web-build bash -c 'cd apps/web && npm run build'
else
  echo "[FAIL] Web dependencies missing: run (cd apps/web && npm ci)"
  printf 'web-dependencies\tNOT_RUN\tapps/web/node_modules missing\n' >>"$evidence/manifest.tsv"
  fail=$((fail+1))
fi
printf 'postgres-audits\tMANUAL_PENDING\trun scripts/phase12-final-db-audit.sh with TEST-DB psql connection\n' >>"$evidence/manifest.tsv"
printf 'browser-qa\tMANUAL_PENDING\tLevel1/Level2/customer checkout, approvals, debt, security, plan, history\n' >>"$evidence/manifest.tsv"
printf 'psp-live\tNOT_CLAIMED\tNo verified live SEP/ZarinPal integration claimed by offline gate\n' >>"$evidence/manifest.tsv"
[[ "$(git rev-parse HEAD)" == "$sha" ]] || { echo "SHA changed during test"; exit 1; }
echo "SHA=$sha PLATFORM=$platform FAILURES=$fail EVIDENCE=$evidence"
cat "$evidence/manifest.tsv"
(( fail == 0 )) || exit 1
echo "[PASS] Offline acceptance only; DB, second-host parity, operator and release approval still required."
