# ArginTaxPayer — Phase 11: Queue, Inquiry & Reconciliation

**Status:** Step 01 owner-approved. Step 02 safety audit and static regression guards committed; runtime acceptance NOT_RUN / completion pending.
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

**Owner decision:** Step 01 explicitly approved; Step 02 authorized by owner with a strict no-regression constraint. Later step design must be revised if read-only code audit proves its target already complete.

## Step 02 — Worker/queue safety audit and regression guard (2026-10-31)

**Owner constraint:** Four Phase 10 legal flows (original, correction, cancellation, sales return) have been manually exercised in Sandbox and accepted. They must remain untouched. This step made NO changes to their runtime APIs, domain, persistence, UI, sending, or database schema.

**Source finding:** `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyOutboxWorker.cs` is an **explicitly invoked sender**, not a background-hosted inquiry loop. It starts a send only after checking `Reserved` and recording the attempt, then persists either initial receipt or indeterminate outcome. Automatically scheduling this worker would be unsafe because it can submit original packets. `Phase10SandboxOutboxService` already supplies deduplication keys and `automaticRetry=false`. `Phase10InquiryReconciliation` explicitly disallows automatic resending or equating a status string with legal acceptance. `Phase10DispatchEvidenceRepository` uses scoped revision checks and stores inquiry evidence append-only.

**Implementation in this step:** added `apps/web/tests/phase11-step02-dispatch-regression.test.mjs`, four read-only source-contract regression checks over the above Phase 10 files. These provide an early alarm against unintentional conversion to auto-sending, weakening indeterminate safety, or removing evidence guards. This is **test-only optimization**, not an operational inquiry scheduler.

**Outstanding Step 02 acceptance criteria:** demonstrate a separate, read-only background inquiry scheduler with authenticated scoped service identity, durable leasing/claim ownership, crash/restart and concurrent-worker behavior. No such runtime implementation was added, since applying automatic scheduling to the Phase 10 sender would threaten proven fiscal flows. Evaluate/add a separate inquiry-only worker in a follow-on owner-approved change, after deciding the service-identity and official Inquiry contract. Thus full Step 02 operational criterion is **NOT MET** yet.

**Tests:** GitHub-only source edit; no macOS or Ubuntu runtime available in this execution. Automated test result `NOT_RUN`; use `node --test apps/web/tests/phase11-step02-dispatch-regression.test.mjs`. No migration, DB query, worker execution, Sandbox contact, or invoice POST occurred.

**Risk/rollback:** Zero runtime surface changed. Remove the test-only commit if a false-positive contract becomes obsolete; never change the Phase 10 worker into an automatic sender. Next actions remain owner gated.
