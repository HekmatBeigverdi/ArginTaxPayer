# Phase 18 — consolidated dual-authority implementation status

## Owner-confirmed workflow update (2026-11-06)

- The owner reports the existing Production original-invoice send was tested live and works. Do not replace or refactor that sender.
- The owner reports all Sandbox invoice operations (original, correction, cancellation and sales return) are tested and work. Keep their legacy tables, keys and service flow isolated.
- This branch adds a version-pinned protocol profile and Production legal-successor persistence/dispatch. Migration `20261106010000_AddProductionLegalSuccessorRelations` is applied to local `argintaxpayer_test` and verified in EF history; it has not been applied to Production. Production legal successors have not been live-tested.
- The authority inquiry must return `SUCCESS` for the predecessor before a legal successor can be prepared. A packet acknowledgement is not fiscal acceptance.
- The modern SDK 2.0.32 remains inactive. See the owner test steps in `docs/phase18-production-invoice-operations-operator-checklist.md`.

## What was implemented in one branch

1. `fiscal_memories.authority_environment`: nullable, explicit `Production=1/Sandbox=2`; legacy memories remain NULL and cannot authorize modern dispatch. API onboarding and Persian UI require explicit selection.
2. `Phase18PersistedAuthorityEnvironmentResolver`: read-only owner/taxpayer/invoice/memory/membership match and active memory requirement.
3. `invoice_dispatch_operations.authority_environment`: new **separate additive migration** `20261104230000_BindDispatchAuthorityEnvironment`. Existing operations remain NULL. PostgreSQL trigger prohibits changing an operation's owner, taxpayer, fiscal memory, invoice, revision or environment once inserted. No legacy backfill.
4. `Phase18PersistedAuthorityOperationGate`: server-owned comparison of operation and active fiscal-memory environments, tenant, invoice and current membership; null/mismatch/unknown/provider mismatch rejected.
5. Existing legacy Sandbox worker and both UID/reference inquiry methods reject Production-classified operations, unknown outbox schema and wrong environment. The legacy guards use `to_jsonb(row)->>'authority_environment'` so old test DB schemas are not broken before Step14 migration.
6. Production signing UI excludes Sandbox and legacy unclassified fiscal memories and displays a warning instead.
7. Branch CI: offline cross-layer tests, full .NET build/regressions, web test/typecheck/build and offline EF SQL generation. One-command acceptance: `bash scripts/phase18-step14-one-shot-source-acceptance.sh`.

## State and required migration

The owner's earlier confirmation covers `20261104220000_AddFiscalMemoryAuthorityEnvironment` on the dedicated Production database. **The newer `20261104230000_BindDispatchAuthorityEnvironment` migration has not been applied by the assistant.** Never run source builds against a database lacking required EF-mapped columns. Before an incremental migration on a populated DB, use encrypted backup and isolated restore evidence, controlled privileged migrator identity, reviewed SQL hash and postmigration verification.

No Release, main merge, systemd restart, Production public UI activation, financial transfer, actual invoice submission or authority inquiry was performed by these commits.

## Mandatory blockers before final Phase18 acceptance

- The original `sandbox_fiscal_memories` and Phase10 workflow still depend on `argintaxpayer_test`, and production operation-memory credentials have not been reconciled with historical Sandbox evidence. Do not simply remove that DB guard or repoint signing keys.
- A **new operation admission path** must atomically bind the trusted persisted memory environment, tenant and invoice to the dispatch operation and durable outbox; existing Phase10 admission remains deliberately test-only. An operation being resolvable is **not** permission to send it.
- Production-specific officially verified API implementation, credentials, encryption/signing, receipt validation, reference inquiry and replay-safe pending/Indeterminate handling must be wired and tested independently. The bundled modern SDK and legacy SELF_TSP adapter have distinct requirements; replacing the host string is insufficient.
- Cancellation, correction and sales return must use the same immutable predecessor environment and scoped memory while retaining the existing single-row/lineage UI semantics.
- Coexistence policy for multiple active per-environment memories under a subscription must be reviewed: the current fiscal_memories active uniqueness index and first-memory registration rule permit only one active memory per taxpayer. Do not silently weaken subscription binding without audited Level1 approval.
- Real acceptance needs an isolated Sandbox scenario and a separately consented actual Production scenario (where legally authorized), cross-environment negative tests, credential isolation, rollback rehearsal and Nginx/HTTPS smoke on `tax.argin.ir`.

## One-shot offline acceptance on macOS or Ubuntu

```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
bash scripts/phase18-step14-one-shot-source-acceptance.sh
```

Offline success is not a go-live certificate. Retain `ArginTaxPayer__EnableOperationalEndpoints=false`, `SandboxInvoiceLegacy__Enabled=false` and `ARGIN_PRODUCTION_UI_ENABLED=false` on the Ubuntu Production deployment until every listed blocker is explicitly resolved and live acceptance is independently confirmed.
