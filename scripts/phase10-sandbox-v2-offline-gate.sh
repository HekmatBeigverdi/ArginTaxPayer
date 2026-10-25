#!/usr/bin/env bash
# Sandbox invoice readiness code gate: intentionally NO invoice POST,
# NO migration, NO database mutation, NO external HTTP and NO live send.
set -euo pipefail
cd "$(dirname "$0")/.."
[[ "$(uname -s)" == "Darwin" ]] || { echo 'Only macOS is permitted before Phase10 Step23'; exit 2; }
[[ "$(git branch --show-current)" == "phase/10-invoice-submission" ]] || {
 echo 'Unexpected branch';exit 2;
}
[[ -z "$(git status --porcelain)" ]] || {
 echo 'Worktree must be clean';git status --short;exit 2;
}
echo "Phase10 Sandbox SDK-v2 offline gate SHA: $(git rev-parse HEAD)"
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter 'FullyQualifiedName~Phase10FiscalInvoiceIdentifierTests|FullyQualifiedName~Phase10SandboxInvoicePacketCryptographyTests|FullyQualifiedName~Phase10SandboxV2InvoiceTransportTests|FullyQualifiedName~Phase10SandboxSingleInvoiceConsentTests'
(
 cd apps/web
 node --test tests/phase10-sandbox-v2-offline-crypto.test.mjs \
  tests/phase10-sandbox-serial-durability.test.mjs \
  tests/phase10-step22-readiness.test.mjs
 npm run typecheck
 npm run build
)
echo "SOURCE/CRYPTO GATE PASS; NOT a real Sandbox invoice test or send authorization."
