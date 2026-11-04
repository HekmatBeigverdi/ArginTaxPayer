# Phase 18 — Step 13: persisted authority preflight and legacy inquiry guards

## Changes in this step
- Existing fiscal-memory environment resolver reads server-owned `fiscal_memories.authority_environment`, verifies customer/taxpayer/invoice ownership, active customer membership, and now requires the fiscal memory to be Active.
- Legacy Sandbox outbox worker (prior step) refuses non-Sandbox payloads and cannot retrieve Sandbox credentials from the dedicated Production DB.
- Both existing Sandbox UID and reference-number inquiry services now refuse any database other than `argintaxpayer_test` **before fetching a key, token or creating an HTTP request**.
- Existing inquiry SQL now additionally checks the immutable outbox envelope's `schema`, `environment`, operation/customer/taxpayer/invoice/memory IDs against the dispatch operation. This avoids cross-environment operation confusion.
- Source regression check: `apps/web/tests/phase18-step13-inquiry-isolation.test.mjs`.

## Acceptance commands (macOS and Ubuntu)
```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
node --test apps/web/tests/phase18-step09-dual-authority-audit.test.mjs apps/web/tests/phase18-step10-fiscal-environment-onboarding.test.mjs apps/web/tests/phase18-step13-inquiry-isolation.test.mjs
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --configuration Release --filter 'FullyQualifiedName~Phase18|FullyQualifiedName~Phase10Sandbox'
```

## Production blockers (not waived by a passing test)

These changes are **not** a dual-environment deployment. Existing Sandbox invoice creation, outbox admission, legacy token signing and legal child operations remain tied to the isolated historical `argintaxpayer_test` database. Moving them blindly onto `argintaxpayer_prod` would bypass owner/key/transport boundaries; do not do so.

Production invoices still lack a reviewed, immutable operation-environment binding + transport credentials, and the full Production authority endpoint flows (send, receipt, reference inquiry, correction, cancellation and sales return) are not implemented. Existing Sandbox operations must be reconciled with their separately stored memory metadata before any consolidation/migration. Indeterminate operations must **never** be retransmitted.

Do not enable operational endpoints or update the active release on tax.argin.ir solely for this step. A validated per-operation provider transport, a reviewed incremental data migration and real dual-environment integration acceptance are required.
