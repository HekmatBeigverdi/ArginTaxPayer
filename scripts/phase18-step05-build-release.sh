#!/usr/bin/env bash
# Phase 18 Step 05 — local-only Release packaging. No deploy/DB/provider actions.
set -Eeuo pipefail
umask 077
die() { echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || die "Wrong branch"
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || die "Dirty tracked files"
for x in dotnet node npm tar git; do command -v "$x" >/dev/null || die "Missing $x"; done
if command -v sha256sum >/dev/null; then digest_cmd=sha256sum; elif command -v shasum >/dev/null; then digest_cmd='shasum -a 256'; else die 'Missing SHA-256 utility'; fi
[[ -f apps/web/package-lock.json ]] || die "Missing lockfile"
sha="$(git rev-parse HEAD)"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
product_version="$(node -p "require('./apps/web/package.json').version")"
release_version="${product_version}+local.${stamp}.${sha:0:8}"
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
run web-production-build env NODE_ENV=production NEXT_PUBLIC_APP_VERSION="$release_version" npm --prefix apps/web run build
[[ -s "$out/stage/api/ArginTaxPayer.Api.dll" ]] || die "API assembly missing"
[[ -f apps/web/.next/BUILD_ID ]] || die "Next.js BUILD_ID missing"
cp apps/web/package.json apps/web/package-lock.json "$out/stage/web/"
cp -a apps/web/.next "$out/stage/web/"
[[ ! -d apps/web/public ]] || cp -a apps/web/public "$out/stage/web/"
# Stage deliberately excludes node_modules; install dependencies from lockfile on target.
if find "$out/stage" -type f \( -name '.env' -o -name '.env.*' -o -name '*.pem' -o -name '*.pfx' -o -name '*.p12' -o -name '*.key' \) | grep -q .; then
 die "Sensitive filename detected in staging; inspect before packaging"
fi
printf 'version=%s\nsha=%s\nutc=%s\ndotnet=%s\nnode=%s\nnext_build_id=%s\n' "$release_version" "$sha" "$stamp" "$(dotnet --version)" "$(node --version)" "$(cat apps/web/.next/BUILD_ID)" > "$out/build-info.txt"
(cd "$out/stage" && tar -czf "$out/api.tar.gz" api && tar -czf "$out/web.tar.gz" web)
(cd "$out" && $digest_cmd api.tar.gz web.tar.gz > SHA256SUMS)
printf 'packaging\tBUILT_NOT_DEPLOYED\n' >> "$out/manifest.tsv"
# Phase18 static preflight does not certify runtime security or functional Production login.
if grep -Fq 'EnableIdentityDashboardOnly' src/ArginTaxPayer.Api/Program.cs &&
   grep -Fq 'if (identityDashboardOnlyEnabled)' src/ArginTaxPayer.Api/Program.cs &&
   grep -Fq 'app.MapPhase15DashboardEndpoints();' src/ArginTaxPayer.Api/Program.cs &&
   grep -Fq 'Status404NotFound' src/ArginTaxPayer.Api/Program.cs; then
 printf 'api-prod-routes\tDASHBOARD_ONLY_SOURCE_GATE_PRESENT_LIVE_QA_PENDING\n' >> "$out/manifest.tsv"
else
 printf 'api-prod-routes\tBLOCKED_DASHBOARD_ONLY_GATE\n' >> "$out/manifest.tsv"
fi
if grep -q 'ARGIN_PRODUCTION_UI_ENABLED' apps/web/lib/identity.ts &&
   grep -q 'ARGIN_PUBLIC_BASE_URL' apps/web/lib/identity-origin.mjs &&
   grep -q 'secure: process.env.NODE_ENV === "production"' 'apps/web/app/api/identity/[...path]/route.ts'; then
 printf 'web-prod-auth\tSOURCE_GATE_IMPLEMENTED_RUNTIME_QA_PENDING\n' >> "$out/manifest.tsv"
else
 printf 'web-prod-auth\tBLOCKED_DEV_GATE\n' >> "$out/manifest.tsv"
fi
printf 'production-deployment\tBLOCKED_PENDING_LIVE_RUNTIME_SECURITY_ACCEPTANCE\n' >> "$out/manifest.tsv"
echo "[DONE] SHA $sha"
echo "[DONE] Local artifacts: $out"
echo "[WARNING] NOT APPROVED FOR PRODUCTION DEPLOYMENT. Run Production-mode auth, security and Ubuntu acceptance."
