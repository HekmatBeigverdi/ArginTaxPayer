#!/usr/bin/env bash
# Step21: macOS acceptance evidence collector. No invoice send, migrations,
# database writes, credential printing or implicit authority request.
set -euo pipefail
cd "$(dirname "$0")/.."
umask 077
[[ "$(uname -s)" == "Darwin" ]] || { echo 'REFUSED: macOS only; Ubuntu is Step23'; exit 2; }
[[ "$(git branch --show-current)" == "phase/10-invoice-submission" ]] || {
  echo 'REFUSED: unexpected branch';exit 2;
}
[[ -z "$(git status --porcelain)" ]] || {
  echo 'REFUSED: uncommitted changes';git status --short;exit 2;
}
sha="$(git rev-parse HEAD)"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
folder="artifacts/phase10-step21-$stamp"
mkdir -p "$folder"
manifest="$folder/manifest.tsv"
printf 'check\tstatus\tevidence\n' > "$manifest"
printf '%s\n' "$sha" > "$folder/source-sha.txt"
failures=0

record(){
 local name="$1" result="$2" evidence="$3"
 printf '%s\t%s\t%s\n' "$name" "$result" "$evidence" >> "$manifest"
}
check(){
 local name="$1";shift
 local file="$folder/$name.log"
 if "$@" > "$file" 2>&1; then
   record "$name" PASS "$file";echo "PASS: $name"
 else
   local rc="$?"
   record "$name" "FAIL($rc)" "$file";echo "FAIL: $name ($rc): $file"
   failures=$((failures+1))
 fi
}
check macos-dev-status zsh scripts/dev-status.zsh
check dotnet-build dotnet build ArginTaxPayer.sln --no-restore
check dotnet-tests dotnet test ArginTaxPayer.sln --no-restore
if [[ -d apps/web/node_modules ]];then
 check web-tests bash -c 'cd apps/web && npm test'
 check web-typecheck bash -c 'cd apps/web && npm run typecheck'
 check web-build bash -c 'cd apps/web && npm run build'
else
 record web-dependencies NOT_RUN 'apps/web/node_modules missing'
 failures=$((failures+1))
fi

# No credentials are passed by argument or printed. Env is read from existing
# shell only; never source a file from the evidence collector.
if [[ -n "${ConnectionStrings__ArginTaxPayerDb:-}" ]];then
 check test-db-readonly-audit bash scripts/phase10-step20-db-audit.sh
else
 record test-db-readonly-audit NOT_RUN 'connection variables not exported'
 failures=$((failures+1))
fi

# Unauthenticated health readiness is distinct from successful user sign-in.
check api-health bash -c 'curl --noproxy "*" --max-time 5 -fsS -o /dev/null http://127.0.0.1:15089/health/ready'
check web-http bash -c 'test "$(curl --noproxy "*" --max-time 5 -sS -o /dev/null -w "%{http_code}" http://127.0.0.1:3000/)" = "307" || test "$(curl --noproxy "*" --max-time 5 -sS -o /dev/null -w "%{http_code}" http://127.0.0.1:3000/)" = "200"'

# Never claim these browser/operator assertions were automatically checked.
for item in 'auth-level1-level2-customer' 'cross-tenant-denial' \
  'sandbox-key-match-restart' 'draft-sstid-validation' \
  'snapshot-idempotent-two-tabs' 'no-invoice-submit-network' \
  'backup-restore-isolated-rehearsal' 'no-credentials-in-logs'; do
  record "$item" MANUAL_PENDING 'see docs/phases/phase-10.md Step21 matrix'
done

printf '%s\n' "$sha" > "$folder/head-after.txt"
if [[ "$(git rev-parse HEAD)" != "$sha" || -n "$(git status --porcelain)" ]];then
 record exact-sha-and-clean-worktree FAIL 'source changed during gate'
 failures=$((failures+1))
else
 record exact-sha-and-clean-worktree PASS "$folder/head-after.txt"
fi
printf 'SHA=%s\nUTC=%s\nplatform=macOS\nautomatic_failures=%s\noperator_acceptance=PENDING\n' \
 "$sha" "$stamp" "$failures" > "$folder/acceptance-status.txt"
echo "Evidence directory: $folder"
cat "$manifest"
if [[ "$failures" -gt 0 ]];then
 echo "Automatic gate FAIL/NOT_RUN; inspect evidence before approving Step21."
 exit 1
fi
echo "Automatic checks PASS. Manual/backup-restore acceptance remains PENDING."
