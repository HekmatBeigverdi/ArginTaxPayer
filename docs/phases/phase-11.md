# ArginTaxPayer — Phase 11: Queue, Inquiry & Reconciliation

**Status:** Step 01 baseline audit recorded; owner approval pending for Step 01 and every subsequent step.
**Base:** Phase 10 merge commit `3d297c50451971f8b7ff6c292b7d1df431699fc9` on `main`; reported Phase 10 release `phase-10-v1.0.0`.
**Branch:** `phase/11-inquiry-reconciliation`
**Target release:** `phase-11-v1.0.0`.
**Reference:** `docs/roadmap/18-phases.md` (Phase 11: durable queue, asynchronous inquiry/reconciliation, safe retries and terminal results).

## Scope freeze and non-regression contract

Owner reports Sandbox acceptance for original submission, correction, cancellation and sales return in Phase 10. Preserve these behaviors, the invoice chain, immutable snapshots, fiscal identifiers, signing, existing Outbox and the one-request send/inquire entry points. Do **not** reimplement invoice dispatch or alter legal workflows without a proved defect and separate owner approval. Do not infer successful live Sandbox execution from offline tests alone.

Phase 11 should extend only demonstrably missing **asynchronous inquiry, restart-safe recovery, bounded retry of inquiry, trusted terminal authority mapping, and scoped operational visibility**. No automatic resend of an indeterminate invoice. No change to Production deployment and no subscription or cash-ledger implementation (Phase 12). Keep Sandbox/Production credentials, records and endpoints isolated.

## Fixed owner-proposed eight-step acceptance plan

| Step | Goal | Acceptance gate |
|---|---|---|
| 01 | Audit Phase 10 implementation and evidence | Inventory existing Outbox, inquiry, Worker, transitions, security and tests; identify unknowns without touching runtime or database |
| 02 | Audit/complement worker and durable queue | Background inquiry without browser; restart and concurrent-worker behavior demonstrated, no duplicate sends |
| 03 | Audit/complement automated authority inquiry | Verified official reference/UID rules and source-version mapping; do not weaken existing manual inquiry |
| 04 | Recover timeouts and transient failures | Bounded backoff for inquiry; crash-safe resumption; indeterminate remains protected |
| 05 | Reconciliation and final-result projection | Only trusted and correlated official evidence may update terminal outcomes; append-only history and legal chain intact |
| 06 | Existing UI/API status presentation | Small compatible changes only, RTL and scoped endpoints, no raw JSON; preserve current invoice operations |
| 07 | Security, regression and authorized Sandbox acceptance | Re-test original, correction, cancellation and sales return, isolation, duplicate prevention; record actual provider results separately from mocks |
| 08 | macOS/Ubuntu same-SHA acceptance and closeout | Build/test, DB audit, migration review if applicable, clean Git evidence, owner-gated PR/merge/tag/release; no implicit deployment |

Each step: inspect actual code first; identify dependencies and impacted files, Backend/Frontend and migration needs; security/legal constraints; tests and real results (PASS/FAIL/NOT_RUN), risks and rollback; stop for explicit owner acceptance. macOS is primary development/migration host; Ubuntu is independent test host. Only `argintaxpayer_test` may receive approved test migrations, after actual-database verification.

## Step 01 — Read-only baseline audit (2026-10-31)

**Inspected GitHub sources at Phase 10 base:**
- `docs/roadmap/18-phases.md` and `docs/phases/phase-10.md`. Caution: Phase 10 master plan still contains early 24-step planning notes; current merged commit explicitly refers to accepted Step 29.
- `scripts/phase10-step29-acceptance.sh`: release build, .NET tests, sales return tests, web tests/typecheck/build. The script marks PostgreSQL audit, live Sandbox send/inquiry, browser acceptance and production isolation `MANUAL_PENDING`; passing the offline script is not live proof.
- `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxOutboxService.cs`: durable encrypted descriptor, deduplication key and `automaticRetry=false`; no authority HTTP call in preparation.
- `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10DispatchEvidenceRepository.cs`: append-only inquiry/initial receipt, scoped row lock/CAS state revision; inquiry evidence alone does not advance to fiscal acceptance. Current code guards `current_database() = argintaxpayer_test`; assess later, **do not relax in Step 01**.
- `src/ArginTaxPayer.Application/Invoices/Phase10InquiryReconciliation.cs`: conservative result correlation, `MayAutomaticallyResend=false`, never treats status text as legal acceptance. Contains a default disabled inquiry boundary; check current DI wiring and later adapters in next read-only inventory.
- `apps/web/app/invoices/invoice-send-readiness-panel.tsx` and current BFF routes including `inquiry-by-reference`, `inquiry-by-uid`, `send-and-inquire`, `cancel-and-inquire`, `correction-send-and-inquire`, `sales-return-send-and-inquire` were confirmed from Phase 10 merge file inventory and prior implementation history; behavioral tests were not run here.

**What exists:** preparation Outbox, idempotency fields, provider receipt/evidence recording, conservative reconciliation contract, manual inquiry endpoints and Phase 10 legal flow UI/API are already implemented. They are inherited, not Step 11 features to rebuild.

**What is not yet independently verified for Phase 11:** fully registered asynchronous inquiry worker, job scheduling and leasing, actual database schema/invariants, crash recovery, terminal-result authority signature/semantics, reference-vs-UID fallback rules, current runtime DI, and live Sandbox end-to-end evidence from Step 29. These are *verification gaps*, not proven absent features.

**Evidence level:** remote GitHub source inspection and prior owner acceptance report only. No macOS/Ubuntu shell access, build, tests, runtime operation, database connection, Sandbox POST, migration, or local Git cleanliness verification was performed in this step.

**Safety/rollback:** documentation-only commit; no runtime behavior, DB change, secrets, Production access or external invoice request. Revert this documentation commit if needed. Phase 10 base remains unchanged.

**Owner decision:** Step 01 awaits review. Do not begin Step 02 before approval. Later step design must be revised if read-only code audit proves its target already complete.
