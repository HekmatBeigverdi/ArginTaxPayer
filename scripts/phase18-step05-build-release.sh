#!/usr/bin/env bash
# Phase 18 Step 05 — local-only Release packaging. No deploy/DB/provider actions.
set -Eeuo pipefail
umask 077
die() { echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || die "Wrong branch"
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || die "Dirty tracked files"
for x in dotnet node npm tar sha256sum git; do command -v "$x" >/dev/null || die "Missing $x"; done
[[ -f apps/web/package-lock.json ]] || die "Missing lockfile"
sha="$(git rev-parse HEAD)"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
out="$PWD/artifacts/phase18-step05/$sha/$stamp"
mkdir -p "$out/logs" "$out/stage/api" "$out/stage/web"
chmod 700 "$out" "$out/logs"
printf 'check\tstatus\n' > "$out/manifest.tsv"
run() {
 local step="$1"; shift
 echo "[START] $step"
 if "$@" > "$out/logs/$step.log" 2>&1; then
   printf '%s\tPASS\n' "$step" >> "$out/manifest.tsv"
   echo "[PASS] $step"
 else
   local rc=$?
   printf '%s\tFAIL(%s)\n' "$step" "$rc" >> "$out/manifest.tsv"
   echo "[FAIL] $step; review local private log" >&2
   exit "$rc"
 fi
}
run dotnet-restore dotnet restore ArginTaxPayer.sln
run dotnet-release-build dotnet build ArginTaxPayer.sln -c Release --no-restore
run dotnet-release-tests dotnet test ArginTaxPayer.sln -c Release --no-restore
run dotnet-publish dotnet publish src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj -c Release --no-restore -o "$out/stage/api"
run web-install bash -c 'cd apps/web && npm ci'
run web-tests bash -c 'cd apps/web && npm test'
run web-typecheck bash -c 'cd apps/web && npm run typecheck'
run web-production-build bash -c 'cd apps/web && NODE_ENV=production npm run build'
[[ -s "$out/stage/api/ArginTaxPayer.Api.dll" ]] || die "API assembly missing"
[[ -f apps/web/.next/BUILD_ID ]] || die "Next.js BUILD_ID missing"
cp apps/web/package.json apps/web/package-lock.json "$out/stage/web/"
cp -a apps/web/.next "$out/stage/web/"
[[ ! -d apps/web/public ]] || cp -a apps/web/public "$out/stage/web/"
# Stage deliberately excludes node_modules; install dependencies from lockfile on target.
if find "$out/stage" -type f \( -name '.env' -o -name '.env.*' -o -name '*.pem' -o -name '*.pfx' -o -name '*.p12' -o -name '*.key' \) | grep -q .; then
 die "Sensitive filename detected in staging; inspect before packaging"
fi
printf 'sha=%s\nutc=%s\ndotnet=%s\nnode=%s\nnext_build_id=%s\n' "$sha" "$stamp" "$(dotnet --version)" "$(node --version)" "$(cat apps/web/.next/BUILD_ID)" > "$out/build-info.txt"
(cd "$out/stage" && tar -czf "$out/api.tar.gz" api && tar -czf "$out/web.tar.gz" web)
(cd "$out" && sha256sum api.tar.gz web.tar.gz > SHA256SUMS)
printf 'packaging\tBUILT_NOT_DEPLOYED\n' >> "$out/manifest.tsv"
# Source checks are conservative blockers until full secured Production refactor.
if grep -q 'if (identityDevelopmentEnabled)' src/ArginTaxPayer.Api/Program.cs; then
 printf 'api-prod-routes\tBLOCKED_DEV_GATE\n' >> "$out/manifest.tsv"
fi
if grep -q 'process.env.NODE_ENV === "development"' apps/web/lib/identity.ts; then
 printf 'web-prod-auth\tBLOCKED_DEV_GATE\n' >> "$out/manifest.tsv"
fi
echo "[DONE] SHA $sha"
echo "[DONE] Local artifacts: $out"
echo "[WARNING] NOT APPROVED FOR PRODUCTION DEPLOYMENT. Verify packages for embedded secrets and resolve gates."
