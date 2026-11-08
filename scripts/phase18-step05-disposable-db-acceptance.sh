#!/usr/bin/env bash
# Explicitly opt-in only for dedicated disposable PostgreSQL with VerifyFull TLS.
# Never creates, migrates, drops, or configures database resources.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
fail() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail "Incorrect branch"
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail "Tracked changes present"
[[ "${RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE:-}" == 1 ]] || fail "Explicit opt-in RUN_PHASE18_DISPOSABLE_DB_ACCEPTANCE=1 required"
[[ -n "${PHASE18_DISPOSABLE_DB_CONNECTION:-}" ]] || fail "PHASE18_DISPOSABLE_DB_CONNECTION missing"
[[ "${RUN_ARGINTAXPAYER_PG_INTEGRATION:-}" != 1 ]] || fail "Disable shared test database integrations"
# Connection details are validated in the xUnit test BEFORE any DB writes.
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step05-db/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'check\tresult\n' > "$out/manifest.tsv"
echo "[INFO] SHA=$sha; private logs=$out"
if env -u ConnectionStrings__ArginTaxPayerDb \
    -u RUN_ARGINTAXPAYER_PG_INTEGRATION \
    dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj \
      -c Release \
      --filter FullyQualifiedName~Phase18DisposableDatabaseAcceptanceTests \
      --logger 'console;verbosity=normal' > "$out/mfa-roles.log" 2>&1; then
    printf 'production_mfa_roles\tPASS\n' >> "$out/manifest.tsv"
    echo '[PASS] disposable DB MFA, sessions, roles'
else
    printf 'production_mfa_roles\tFAIL\n' >> "$out/manifest.tsv"
    echo "[FAIL] inspect private log $out/mfa-roles.log (do not disclose credentials)"
    exit 1
fi
printf 'production_tenant_data_isolation\tPENDING_SCOPED_FIXTURE_QA\n' >> "$out/manifest.tsv"
printf 'production_bff_browser_cookies\tPENDING_BROWSER_QA\n' >> "$out/manifest.tsv"
printf 'production_go_live\tBLOCKED\n' >> "$out/manifest.tsv"
echo "[DONE] $out/manifest.tsv"
echo '[NOTE] Dedicated DB contains only test fixtures; operator must drop DB and role after acceptance.'
