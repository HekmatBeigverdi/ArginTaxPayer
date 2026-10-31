# ArginTaxPayer — Phase 11: Queue, Inquiry & Reconciliation

**Status:** Step 01 owner-approved. Steps 01–02 owner-approved; Step 03 owner-approved. Step 04 inquiry-only bounded retry policy committed; execution tests pending.
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

### Step 02 continuation: opt-in read-only queue observer (2026-10-31)

- Added `src/ArginTaxPayer.Api/Invoices/Phase11InquiryQueueObserver.cs` as a scoped, one-minute, read-only development background observer of existing dispatch operations in states Acknowledged (5) and Indeterminate (6). It explicitly checks the actual database name, logs only a count, and makes **no authority HTTP call or invoice mutation**.
- Added explicit opt-in `Phase11:EnableReadOnlyQueueObserver` in `src/ArginTaxPayer.Api/Program.cs`, behind `identityDevelopmentEnabled`; default disabled, no effect on Phase 10 release flows.
- Added `apps/web/tests/phase11-step02-queue-observer.test.mjs` for a static safety contract, in addition to the existing `phase11-step02-dispatch-regression.test.mjs`.
- **No change** to the Phase 10 sender, legal invoice routes, keys, payloads, Outbox persistence, database schema or migration. No automatic sending.
- This does **not yet deliver real automatic inquiry**: it observes pending work but does not claim/lease or query the Tax Authority. Authorized, durable inquiry execution and cross-worker coordination remain a documented functional gap; do not label operational completion until proven. The later dedicated inquiry step must address it, preserving all Phase 10 behavior.
- Test execution: GitHub source changes only; macOS build, web tests, browser acceptance, database observation, restart test and Ubuntu test are **NOT_RUN**. Operator can run `node --test apps/web/tests/phase11-step02-*.test.mjs` and `dotnet build ArginTaxPayer.sln -c Release` locally. Ubuntu remains the final-phase gate unless infrastructure changes require earlier verification.
- Rollback: disable the opt-in flag (or revert the two implementation commits); no DB changes to reverse.

## Step 03 — Phase 10 Reference/UID inquiry compatibility (2026-10-31)

**Owner acceptance inherited:** The owner states that existing Sandbox reference/UID inquiries operate correctly. Existing original/correction/cancellation/return and their inquiry workflows remain frozen; do not replace their APIs, HTTP packets, signing, database writes or UI.

**Source audit:**
- `Phase10LegacyReferenceInquiryService.InquireAsync` selects only a scoped Acknowledged (state 5) dispatch with a prior receipt/reference and sends `INQUIRY_BY_REFERENCE_NUMBER`. It preserves report/evidence history.
- `Phase10LegacyUidInquiryService.InquireAsync` selects a scoped Indeterminate (state 6) operation and sends `INQUIRY_BY_UID`. It does not initiate an invoice send.
- `Phase10InquiryReconciliation` checks identity correlation and does not infer fiscal acceptance, non-delivery or authorization to resend from incomplete results.
- The two existing Next.js BFF routes enforce session cookie, origin/payload checks, no-store, scoped upstream resource identity and safe response shapes. Their behavior is left unchanged.
- In this step neither route was changed; reference and UID are independent supported paths, not an implicit fallback that could silently change fiscal semantics.

**Code change:** Added only `apps/web/tests/phase11-step03-inquiry-regression.test.mjs` with four static contract tests to guard the accepted source contracts. No runtime, DB, migration, production or external Sandbox change. No additional automatic inquiry transport is needed in this step: actual background execution, durable scheduling and backoff remain in later approved tasks. Step 02's read-only observer must never be interpreted as an automatic authority inquiry.

**Test evidence:** GitHub source inspected, test file committed. macOS Node/TypeScript/.NET tests and Sandbox operator runs were NOT_RUN by assistant. Owner's prior live acceptance is reported separately, not represented as a new run.

**macOS acceptance:** `node --test apps/web/tests/phase11-step03-inquiry-regression.test.mjs apps/web/tests/phase11-step02-dispatch-regression.test.mjs apps/web/tests/phase11-step02-queue-observer.test.mjs`, `dotnet build ArginTaxPayer.sln -c Release`, `dotnet test ArginTaxPayer.sln -c Release`, frontend `npm test && npm run typecheck && npm run build`. No Migration. Ubuntu acceptance deferred to final phase gate per owner.

**Rollback:** Revert the Step03 test-only commit; no runtime rollback necessary.

**Step 03:** Implementation complete as a no-runtime-change compatibility audit; automated tests and owner acceptance pending.

## Step 04 — Bounded inquiry-only retry safety policy (2026-10-31)

**Owner approval:** Step 03 accepted. Live Sandbox original/correction/cancellation/return and their current inquiry flows remain frozen and untouched.

**Added:** `src/ArginTaxPayer.Application/Invoices/Phase11InquiryRetryPolicy.cs` pure decision policy, and `tests/ArginTaxPayer.Tests/Phase11InquiryRetryPolicyTests.cs` covering transient errors, non-transient failures, capped exponential backoff, input validation and the invariant `MayResendInvoice == false`. A decision grants only permission to initiate a later **read-only inquiry**, never an invoice POST.

**Classification:** retryable: `inquiry_timeout`, `inquiry_network_unavailable`, HTTP 429/502/503/504. No automatic retry for 401/403/404, conflicting evidence, malformed results or provider rejections. At most six completed attempts with deterministic 1/2/4/8/16/30-minute delays. A future durable queue must persist counters and acquire a lease before scheduling; no concurrency claim is made today.

**Non-changes:** No modifications to Phase10 production code, signed packets, dispatch state, SDK, HTTP inquiry services, browser UI or DB. No migration, Sandbox request, Ubuntu test or Production activation.

**Evidence:** GitHub commit verified. macOS `dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --filter FullyQualifiedName~Phase11InquiryRetryPolicyTests` NOT_RUN; full build/regression NOT_RUN. The added policy is intentionally not wired to the existing accepted live inquiry path.

**Remaining operational boundary:** No durable retry dispatcher or crash recovery has been activated. This requires a separate safe background inquiry execution path with leases and service authentication; do not claim automated recovery is running. The approved eight-step phase separates source-compatible policy (Step04) and reconciliation/processing integration (Step05+), but the end-to-end Step04 objective is only partially satisfied until connected and tested.

**Rollback:** revert policy/test commits, or simply keep policy unused. No database rollback necessary.
