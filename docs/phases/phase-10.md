# ArginTaxPayer — Phase 10: Tax Authority Invoice Submission (Two-Pattern Delivery)

**Status:** Planned and documented; implementation not started; owner step-by-step approval required.  
**Plan date:** 2026-10-23.  
**Repository:** HekmatBeigverdi/ArginTaxPayer  
**Base branch / inspected SHA:** `main` / `7112f03b7fe30ceea1aff7f04f587a1b0e939c92` (verify again at Step 01; Phase09 merge/release anchor `fde20594e733f96e032396f8e22e8781b14cac18`).  
**Phase branch:** `phase/10-invoice-submission`  
**Prior release:** `phase-09-v1.0.0`  
**Fixed step count:** **24**. Do not renumber/extend without separate owner-approved scope amendment.  
**Primary objective:** Secure, auditable electronic invoice dispatch to the Tax Authority for exactly two patterns using Phase 07/08 invoice and Phase 09 key custody infrastructure, with one owner-authorized live invoice submission evidenced before phase acceptance.

## 1. Approved scope — deliberately narrow but extensible

### Enabled invoice patterns
1. **General sale of goods and services** (official pattern identifier `inp=1`); eligible invoice types must be explicitly verified against the current official edition (type 1 and type 2 only where allowed).
2. **Contracting / پیمانکاری** (official type-1 contracting pattern identifier `inp=4`); must capture, validate and serialize the Tax Authority's unique seller contract registration identifier (`crn` where confirmed by the current rule edition). Never confuse a local contract number with the official registered ID.

For **both** enabled patterns, implement original invoice and each officially permitted correction, cancellation and return-from-sale operation; an unsupported pattern/type/operation combination must fail closed. Referenced corrections/cancellations/returns are new linked immutable invoice records, not in-place modifications of previously transmitted records. Tax Authority deletion of already transmitted invoices is **not** supported: use legally applicable cancellation. Internal draft deletion/withdrawal stays governed by existing document/audit policy.

### Excluded from Phase 10
Foreign exchange, gold/jewelry/platinum, utility bills, air tickets, export and other special patterns; no UI, payload adapter or live submission for these. Type 3 POS receipts are not a Phase10 standalone send workflow unless a separate documented authority requirement establishes one; do not imply generalized coverage of all invoice types. Comprehensive asynchronous authority inquiry is Phase11; subscription/partner revenue collection is Phase12. Certificate-backed JWS, HSM and private-key export are not silently introduced if the Phase09 approved key-based profile does not support them.

### Non-negotiable extensibility contract
- Canonical invoice aggregate and immutable snapshot are **pattern-neutral**, reusing Phases07–08. No duplicated invoice engine or per-pattern tables unless technically justified.
- Versioned `InvoicePatternDefinition` (or existing equivalent) stores `invoiceType`, `patternCode`, `ruleSetVersion`, `required/conditional/forbidden fields`, supported operations and field/UI metadata. Actual official field identifiers must be pinned to sources; don't infer or hard-code unsupported combinations.
- Per-pattern mapper/validator plug-ins (or strategies) build official header/body/payment/extension DTOs. Keep transport, signing, idempotency, audit and error/recovery engine shared.
- Frontend field visibility/labels/validation derive from backend source-of-truth metadata; server revalidates and rejects untrusted input.
- Version immutable snapshots with `patternCode`, `invoiceType`, `ruleSetVersion`, `mapperVersion` and signing profile. Historical invoices keep original semantics after rule updates.
- Registering a future pattern requires source citation, schema/rule mapping, explicit enabled flag, UI metadata, golden JSON fixtures, lineage/operation tests, security review and owner-approved release. Unsupported patterns are denied, not silently treated as general sales.
- Contracting must expose and persist any officially required contract identifier at the appropriate header or contract extension location, not as free-form notes. Exact required/optional/conditional field matrix is a Step02 deliverable and must cover common header, all relevant items, settlement and reference fields for both patterns.
- VAT rates and time limits are rule-set inputs, never unconditional constants copied from SDK demo code.

## 2. Evidence, primary sources and deferred obligations

**Primary SDK evidence (priority order):** checked-in `references/tax-sdk/legacy-0.0.34/` archives (`.zip`, `.nupkg`) and `references/tax-sdk/modern-2.0.32/` archive/extracted C# source. Do **not** use online NuGet as a source or a fallback in this plan. Validate archive hashes and license/provenance; no assumption that legacy 0.0.30 source is byte-equivalent to legacy 0.0.34 package or modern 2.0.32 source.

**Normative evidence:** applicable official Tax Authority circulars, invoice issuance rules, official connection instructions, exact edition/date/section/page and SDK algorithm details. Official rules govern conflicts with examples or archived SDK code. The exact current edition, including `crn` field shape, is a mandatory Step02 verification gate, not a claim made solely from historic or mirrored PDFs.

**Phase09 accepted inheritance:** production-like protected private-key custody, local signatures, fiscal-memory/token onboarding, scoped access; **do not regenerate keys or rebuild identity infrastructure**.

**Phase09 qualified exceptions:** original Phase09 release records live Tax Authority authentication evidence as unverified, PostgreSQL operational audit evidence incomplete, protected backup/restore unverified and selected manual security tests pending. Owner later reports an actual successful taxpayer connection/token test; document both statements and reconcile with verifiable evidence at Step01/08, not claim either result was independently proven. Phase08 bulk-import QA and cross-platform same-SHA parity remain independent regression debt.

**Source references:** `docs/roadmap/18-phases.md`, `docs/phases/phase-07-invoice-domain-rule-engine.md`, `docs/phases/phase-08.md`, `docs/phases/phase-09.md`, `references/tax-sdk/`, and official Tax Authority specifications only. README/archive sample contents may be stale; treat all imported code as untrusted.

## 3. Delivery/operational safeguards

- Stack stays .NET 9 Web API, Next.js, EF Core/PostgreSQL. Preserve existing MFA, authorization, commercial ownership and taxpayer/memory isolation.
- Customer-owned authorized signing only; Level1/Level2 reading metadata never implies permission to use private keys. Define explicit background-signing authorization in Step03/09.
- No private key, password, DEK/KEK, bearer token, sensitive raw error body or taxpayer-specific secret in browser storage, public DTO, repository or logs.
- TLS verification mandatory. Ignore insecure SDK transport settings, never allow arbitrary outbound endpoint configuration from user input.
- Persist intent and immutable signed payload identity before network calls; retries only when safe. Unknown external outcome must reconcile before any resend. Reference receipt is not evidence of final authority acceptance.
- Production database migration/deployment, live invoice transmission, merge, tag and release require **separate explicit owner permission**. Review SQL and rollback first; verify `current_database() = 'argintaxpayer_test'` before any local database test.
- macOS primary, Ubuntu 24.04 VPS same commit SHA at acceptance gates. Use existing project scripts and AUTOSSH keepalive with PostgreSQL local tunnel port `15432`. No generated `.next-dev`, local env or machine-specific caches in Git.
- UI: Persian RTL, unified green theme, compact tables, sectional modals, searchable dropdowns, Jalali date picker, section-level loading/skeleton and precise Persian errors.
- One step at a time, per-step doc update with commits/build/tests/deferrals, next step blocked until owner explicitly approves. Do not characterize GitHub file inspection as local build/runtime verification.

## 4. Milestones and checkpoints

| Milestone | Steps | Acceptance gate |
| --- | --- | --- |
| Rules and architecture | 01–04 | Owner-reviewed two-pattern field/operation/extension matrix |
| Transport foundation | 05–09 | Migration reviewed and signing/auth contract tested |
| Dispatch engine | 10–15 | Submission, no duplicate, response/recovery and legal lineage tested |
| Operational UI | 16–19 | Two-pattern compact UI and scoped submission lifecycle |
| Quality + real authority | 20–23 | macOS/Ubuntu same SHA, guarded live submission, regression/security and future-pattern dry run |
| Closeout | 24 | Explicit owner publication authorization |

## 5. Fixed 24-step implementation matrix

| Step | Title | Dependencies | Core acceptance | Status |
| --- | --- | --- | --- | --- |
| 01 | Baseline and scope freeze | Phase09 release and latest confirmed main | Read-only repository and safety audit; no code or migration. | Not started |
| 02 | Official rules and SDK source matrix | 01 | Hash and source-path inspection, official-version comparison and golden rule fixtures; no assumed NuGet parity. | Not started |
| 03 | Connector architecture and pattern extensibility | 02 | Architecture review; prove unsupported patterns fail closed and common code is pattern-neutral. | Not started |
| 04 | State machines and policy contracts | 03 | Pure state-transition matrix tests covering denial and duplicate commands. | Not started |
| 05 | Persistence/outbox and migration proposal | 04 | Schema/model tests and reviewed SQL/rollback; migration application separately approved on argintaxpayer_test only. | Not started |
| 06 | Two-pattern payload projection | 02–05 | Golden payload fixtures, null/decimal/rounding/timezone tests; reject missing or forbidden fields. | Not started |
| 07 | Preflight and immutable invoice snapshot | 06 | Negative field matrix, audit and concurrency tests; no send when invalid or changed. | Not started |
| 08 | Live connection and scoped authentication | 02–07; owner-controlled live test | TLS/timeout/expired token tests; optional live read-only authenticated smoke with explicit owner authorization. | Not started |
| 09 | Wire signing and prepared packet | 07–08 | Signature fixtures, public key verification, key rotation/mismatch/tenant denial; secrets absent from responses/logs. | Not started |
| 10 | Submission transport and initial receipts | 05,09 | Mock HTTP 2xx/non-2xx/malformed body/partial responses; no acceptance claim without authority evidence. | Not started |
| 11 | Idempotency and concurrency admission | 05,10 | Concurrent PostgreSQL unique constraint/replay/CAS tests with owner-approved test migration. | Not started |
| 12 | Controlled retry and network fault policy | 10–11 | 429/5xx/timeout/cancellation/crash fault injection, no duplicate sends. | Not started |
| 13 | Response and uncertain-result mapping | 10–12 | Reordered/duplicate/missing/malformed response tests; secure error serialization. | Not started |
| 14 | Reconciliation of interrupted sends | 13 | Restart, provider outage, incomplete transaction and no-blind-retry tests. | Not started |
| 15 | Original, correction, cancellation and return | 02,06–14 | Per-pattern/per-type/per-operation fixture matrix and invalid lineage cases; no unsupported combination. | Not started |
| 16 | Scoped application/API/BFF contracts | 11–15 | HTTP 400/401/403/404/409; cross-tenant/current-vs-former partner and CSRF/no-store tests. | Not started |
| 17 | Pattern-aware compact invoice UI | 06–07,16 | UI field/visibility/required/disabled tests, type-specific layout, no long one-page form. | Not started |
| 18 | Submit, receipt and timeline UI | 16–17 | Role tests, loader/skeleton and pending/error/result render tests. | Not started |
| 19 | Error recovery and permitted follow-up UI | 14–18 | Double-click, stale view, uncertain outcome and denial E2E tests. | Not started |
| 20 | Security, regression and reference verification | 01–19 | Unit/integration/web/security tests; explicit negative evidence and unresolved exceptions. | Not started |
| 21 | macOS operator acceptance | 20; migration approval if needed | Record exact SHA, command/result, current_database and secrets safe-handling checks. | Not started |
| 22 | Ubuntu same-SHA and controlled live invoice acceptance | 21; live-send approval; legitimate invoice data | Cross-platform build/test, live API response and immutable receipt proof; report inconclusive outcomes honestly. | Not started |
| 23 | Final scope/quality and extensibility audit | 20–22 | Extension dry-run with synthetic unsupported pattern (must fail closed), no production side effects; manual security checklist. | Not started |
| 24 | Owner-gated closeout and publication | 23 + explicit owner approval | Verify GitHub SHA/tag/release, branch cleanliness, evidence and exceptions; no automatic deployment. | Not started |

## 6. Full per-step delivery contracts

### Step 01 — Baseline and scope freeze

- **Goal / change scope:** Inspect Phase 07–09 interfaces, roadmap, release/tag, database history and reference inventory; establish the two-pattern scope and inherited exceptions.
- **Dependencies / prerequisites:** Phase09 release and latest confirmed main.
- **Acceptance criteria:** Read-only repository and safety audit; no code or migration.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Design/read-only and focused unit tests when appropriate; no live transmission.
- **Deliverable:** Baseline SHA, dependency map, evidence register and precise deferred work.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 02 — Official rules and SDK source matrix

- **Goal / change scope:** Compare official editions with bundled legacy 0.0.34 and modern 2.0.32 source/package; pin per-type/pattern required, optional, conditional, forbidden fields and signature wire profiles.
- **Dependencies / prerequisites:** 01.
- **Acceptance criteria:** Hash and source-path inspection, official-version comparison and golden rule fixtures; no assumed NuGet parity.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Design/read-only and focused unit tests when appropriate; no live transmission.
- **Deliverable:** Source traceability matrix with document ID/edition/page, SDK file/line/hash, test vectors and unresolved blockers.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 03 — Connector architecture and pattern extensibility

- **Goal / change scope:** Design bounded protocol adapters, PatternDefinition registry, field schema/rules and rendering metadata; separate Tenant/auth, signature, transport and result handling.
- **Dependencies / prerequisites:** 02.
- **Acceptance criteria:** Architecture review; prove unsupported patterns fail closed and common code is pattern-neutral.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Design/read-only and focused unit tests when appropriate; no live transmission.
- **Deliverable:** ADR/interface sketch, ownership matrix and extension checklist.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 04 — State machines and policy contracts

- **Goal / change scope:** Define independent invoice workflow, dispatch operation and authority result states, authorized commands, reference rules and retry semantics.
- **Dependencies / prerequisites:** 03.
- **Acceptance criteria:** Pure state-transition matrix tests covering denial and duplicate commands.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Design/read-only and focused unit tests when appropriate; no live transmission.
- **Deliverable:** Versioned state/authorization/error contracts.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 05 — Persistence/outbox and migration proposal

- **Goal / change scope:** Review existing EF schema and design append-only attempt/receipt records, unique scoped idempotency and reference chains; prepare additive SQL without applying it.
- **Dependencies / prerequisites:** 04.
- **Acceptance criteria:** Schema/model tests and reviewed SQL/rollback; migration application separately approved on argintaxpayer_test only.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Design/read-only and focused unit tests when appropriate; no live transmission.
- **Deliverable:** Migration proposal, risk analysis and owner decision gate.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 06 — Two-pattern payload projection

- **Goal / change scope:** Map canonical Phase07 invoice snapshots to type/pattern-specific official header, item, payment and extension DTOs for general sales and contracting; no frontend tax calculations.
- **Dependencies / prerequisites:** 02–05.
- **Acceptance criteria:** Golden payload fixtures, null/decimal/rounding/timezone tests; reject missing or forbidden fields.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Deterministic mapper and versioned field matrix for each approved type/pattern.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 07 — Preflight and immutable invoice snapshot

- **Goal / change scope:** Validate seller/buyer, fiscal memory, reference links, VAT, totals, legal field rules and contract registration ID before sealing an immutable approved snapshot.
- **Dependencies / prerequisites:** 06.
- **Acceptance criteria:** Negative field matrix, audit and concurrency tests; no send when invalid or changed.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Preflight findings with Persian codes and signed-snapshot-ready contract.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 08 — Live connection and scoped authentication

- **Goal / change scope:** Reuse Phase09 key custody, token/nonce and memory association; verify actual protocol-specific authority authentication without crossing user boundaries.
- **Dependencies / prerequisites:** 02–07; owner-controlled live test.
- **Acceptance criteria:** TLS/timeout/expired token tests; optional live read-only authenticated smoke with explicit owner authorization.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Secure versioned auth adapter; record legacy connection evidence gap.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 09 — Wire signing and prepared packet

- **Goal / change scope:** Connect Phase09 authorized signature to exact serialized protocol bytes with profile-safe separation; protect signing intent and result.
- **Dependencies / prerequisites:** 07–08.
- **Acceptance criteria:** Signature fixtures, public key verification, key rotation/mismatch/tenant denial; secrets absent from responses/logs.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Ready-to-dispatch signed packet and internal audit.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 10 — Submission transport and initial receipts

- **Goal / change scope:** Implement submission of signed packets to allowed endpoint, validate response and persist initial reference/UID if returned; offline mock first.
- **Dependencies / prerequisites:** 05,09.
- **Acceptance criteria:** Mock HTTP 2xx/non-2xx/malformed body/partial responses; no acceptance claim without authority evidence.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Submission connector plus initial durable receipt contracts.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 11 — Idempotency and concurrency admission

- **Goal / change scope:** Bind tenant, memory, immutable invoice revision and operation to durable idempotency; prevent double click/races and conflicting sends.
- **Dependencies / prerequisites:** 05,10.
- **Acceptance criteria:** Concurrent PostgreSQL unique constraint/replay/CAS tests with owner-approved test migration.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Exactly one logical dispatch admission and deterministic conflicts.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 12 — Controlled retry and network fault policy

- **Goal / change scope:** Use bounded retry, backoff and circuit protection only where safe; never blindly resend uncertain externally committed invoices.
- **Dependencies / prerequisites:** 10–11.
- **Acceptance criteria:** 429/5xx/timeout/cancellation/crash fault injection, no duplicate sends.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Retry classification and configurable policy with safe stop.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 13 — Response and uncertain-result mapping

- **Goal / change scope:** Persist transport result and authority result independently; normalize reference numbers, errors and unknown responses with redaction.
- **Dependencies / prerequisites:** 10–12.
- **Acceptance criteria:** Reordered/duplicate/missing/malformed response tests; secure error serialization.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Typed outcome store and localized error catalog.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 14 — Reconciliation of interrupted sends

- **Goal / change scope:** Introduce bounded evidence-driven lookup/reconciliation to resolve uncertain submissions using supported official identifiers; defer broad periodic inquiry to Phase11.
- **Dependencies / prerequisites:** 13.
- **Acceptance criteria:** Restart, provider outage, incomplete transaction and no-blind-retry tests.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Recovery workflow with auditable operator action.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 15 — Original, correction, cancellation and return

- **Goal / change scope:** Implement legal successor/reference validation for two approved patterns only; preserve original immutable invoice and allowed actions.
- **Dependencies / prerequisites:** 02,06–14.
- **Acceptance criteria:** Per-pattern/per-type/per-operation fixture matrix and invalid lineage cases; no unsupported combination.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Two-pattern operation matrix and fully tested reference-chain handling.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 16 — Scoped application/API/BFF contracts

- **Goal / change scope:** Expose command/query endpoints for prepare, submit, attempts, receipt and history; integrate current MFA/RBAC and ownership.
- **Dependencies / prerequisites:** 11–15.
- **Acceptance criteria:** HTTP 400/401/403/404/409; cross-tenant/current-vs-former partner and CSRF/no-store tests.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Stable typed API and scoped BFF endpoints.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 17 — Pattern-aware compact invoice UI

- **Goal / change scope:** Build Persian RTL segmented modals for general sales and contracting; show only fields/conditions defined by backend pattern metadata; searchable dropdowns, Jalali calendar.
- **Dependencies / prerequisites:** 06–07,16.
- **Acceptance criteria:** UI field/visibility/required/disabled tests, type-specific layout, no long one-page form.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Two-pattern create/edit/preview experience.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 18 — Submit, receipt and timeline UI

- **Goal / change scope:** Add intentional submit confirmation, read-only signed preview, status/receipts and immutable attempt timeline.
- **Dependencies / prerequisites:** 16–17.
- **Acceptance criteria:** Role tests, loader/skeleton and pending/error/result render tests.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Production-facing submission and status workspace.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 19 — Error recovery and permitted follow-up UI

- **Goal / change scope:** Show clear Persian errors, constrained retry/reconcile actions and valid correction/cancellation/return choices; prevent misleading deletion and duplicate submission.
- **Dependencies / prerequisites:** 14–18.
- **Acceptance criteria:** Double-click, stale view, uncertain outcome and denial E2E tests.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Safe operational UI for both patterns.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 20 — Security, regression and reference verification

- **Goal / change scope:** Regression across Phases01–09; audit leakage/TLS/source provenance, backup-restore and database evidence debt.
- **Dependencies / prerequisites:** 01–19.
- **Acceptance criteria:** Unit/integration/web/security tests; explicit negative evidence and unresolved exceptions.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Complete verified test matrix without marking unexecuted tests as passed.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 21 — macOS operator acceptance

- **Goal / change scope:** Run builds, tests, local PostgreSQL integration with validated argintaxpayer_test, UI flows and evidence collection.
- **Dependencies / prerequisites:** 20; migration approval if needed.
- **Acceptance criteria:** Record exact SHA, command/result, current_database and secrets safe-handling checks.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Independent operator output and environment/SHA parity required.
- **Deliverable:** Signed macOS acceptance record and reproducible logs.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 22 — Ubuntu same-SHA and controlled live invoice acceptance

- **Goal / change scope:** Verify same SHA on Ubuntu; with owner permission and properly authorized real taxpayer (e.g., Negin) submit one valid invoice, capture reference and independently distinguish acceptance from submission.
- **Dependencies / prerequisites:** 21; live-send approval; legitimate invoice data.
- **Acceptance criteria:** Cross-platform build/test, live API response and immutable receipt proof; report inconclusive outcomes honestly.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Independent operator output and environment/SHA parity required.
- **Deliverable:** Ubuntu and live authority acceptance evidence or explicitly documented blocker.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 23 — Final scope/quality and extensibility audit

- **Goal / change scope:** Review field/type/operation matrix, documentation, security debt and proof that adding next pattern only adds rules/mapper/UI metadata plus fixtures.
- **Dependencies / prerequisites:** 20–22.
- **Acceptance criteria:** Extension dry-run with synthetic unsupported pattern (must fail closed), no production side effects; manual security checklist.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Closeout readiness and future-pattern implementation guide.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 24 — Owner-gated closeout and publication

- **Goal / change scope:** Complete authoritative phase document with owner acceptance; merge/tag/release only after explicit separate authorization.
- **Dependencies / prerequisites:** 23 + explicit owner approval.
- **Acceptance criteria:** Verify GitHub SHA/tag/release, branch cleanliness, evidence and exceptions; no automatic deployment.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Phase-10-v1.0.0 release metadata if authorized, otherwise pending closeout.
- **Execution status:** Not started. Await owner approval and previous step signoff.


## 7. Pattern-field traceability template (mandatory Step02 completion)

Maintain the following rows in **this document**, filling exact official references before code is implemented. Pattern-specific fields must exist in the domain snapshot and UI, not merely in a generic unvalidated JSON payload.

| Pattern / type | Field category | Requiredness source | Rule/serialization test | Status |
| --- | --- | --- | --- | --- |
| General sale / type1 | Common header + seller, buyer, memory, dates, invoice identifiers | Pending edition/page | Golden header fixture + negative validations | Pending Step02 |
| General sale / type1 | Goods/service items, units, prices, discount, VAT, totals | Pending edition/page | Decimal and item matrix | Pending Step02 |
| General sale / eligible type2 | Type2-specific buyer/settlement omissions and rules | Pending edition/page | Valid/invalid type2 fixture | Pending Step02 |
| Contracting / type1 | Common header + officially registered seller contract identifier | Pending edition/page and exact `crn` rule | Missing/invalid/valid contract ID | Pending Step02 |
| Contracting / type1 | Services/items, payment/settlement conditions and contract-related fields | Pending edition/page | Golden contracting fixture + conditional fields | Pending Step02 |
| Both patterns / allowed operations | Original, correction, cancellation, return, reference tax ID and lineage | Pending edition/page for each combination | Transition/reference fixtures | Pending Step02/15 |

**No missing official-field matrix may be filled by guessing or by copying example JSON.** If source rules are incompatible with present invoice data, extend the existing canonical model and field-aware UI in approved steps, preserving historic revisions.

## 8. Acceptance and release definition

Completion requires real, authorized dispatch of at least one **legitimate** valid invoice for an onboarded taxpayer (e.g. Negin), exact response identifiers persisted and safely displayed, and objective separation of acknowledgment from final acceptance. Do not transmit invented invoices to production solely for testing; use an approved test environment when possible and owner-authorized real business data otherwise. Both supported patterns must have complete offline end-to-end contract coverage; where live tests are available and authorized, test both patterns and legal successor operations. Report unavailable authority test cases as explicit exceptions requiring owner decision, never claim blanket live success.

At Step23 demonstrate a **synthetic future-pattern extension dry-run** with no new engine, table, signing or transport rewrites; no activation of any third pattern in Phase10.

Owner-approved Step24 is necessary before merge into `main`, tag `phase-10-v1.0.0` and GitHub release.

## 9. Evidence and owner approvals (append-only)

| Date | Event | Evidence / SHA | Owner approval |
| --- | --- | --- | --- |
| 2026-10-23 | Two-pattern scope directed by owner; fixed 24-step plan written | Planning-only branch; no runtime changes | Scope approved; implementation steps not yet approved |

Every step completion must add: modified paths, migration SQL status, macOS test commands/outcomes, Ubuntu parity where applicable, official endpoint activity, missing evidence, exact commit SHA and owner acceptance before next step.

## 10. Step 01 — Baseline & scope freeze: source inspection record (2026-10-23)

**Status:** Documentation/source-inspection complete; macOS operator acceptance **pending**. No claim of local build, database execution or external Tax Authority transaction. Step02 remains blocked until explicit owner approval of Step01.

### Exact read-only evidence

- Verified GitHub `main` at `7112f03b7fe30ceea1aff7f04f587a1b0e939c92` on 2026-10-23. Prior Phase09 release merge anchor was `fde20594e733f96e032396f8e22e8781b14cac18`; the later main tip includes checked-in SDK archives. Verify the precise Git merge-base and ancestry again at operator checkout rather than assuming that the phase branch is at an obsolete SHA.
- The Phase10 branch before this evidence update was `5673da3861e318810feb93167ae57ffb5857af9e`, created from the inspected main tip. Fixed 24-step plan and the restricted two-pattern delivery scope are documented above.
- Phase09 GitHub Release `phase-09-v1.0.0` exists, published 2026-10-23. Its release text qualifies live authority authentication, complete PostgreSQL audit evidence, protected backup/restore and selected manual security scenarios. User reported successful authenticated taxpayer connection in Phase09; retain as an operator assertion until reproducible evidence is attached. No live submission has been performed in this planning step.
- Read `docs/roadmap/18-phases.md`, `docs/phases/phase-07-invoice-domain-rule-engine.md`, `docs/phases/phase-08.md`, and `docs/phases/phase-09.md`. **Correction to the plan's generic reference:** Phase07 authoritative document is `phase-07-invoice-domain-rule-engine.md`, not `phase-07.md`. Scope boundary retained: Phase10 dispatch/references; Phase11 comprehensive asynchronous inquiry; Phase12 entitlements/financial settlement.
- Existing domain rules at `src/ArginTaxPayer.Domain/Invoices/InvoicePatternMatrix.cs` already classify source/edition/time-bound type-pattern combinations and reject unverified submission. `InvoiceRuleSetRegistry.cs` already pins validity and has `IsSubmissionAuthorized => false` on the inspected lookup contract: **do not treat a read-only matrix match as dispatch authority**.
- Existing correction, cancellation and return rule files reside in `src/ArginTaxPayer.Domain/Invoices/`. Reuse them, with official per-pattern operation verification, instead of duplicating the legal reference chain.
- Phase08 code includes `apps/web/app/invoices/workspace.tsx`, invoice list/review features and Development-only endpoints `src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs` and `InvoiceReviewDevelopmentEndpoints.cs`. Phase10 production submission endpoints must be separate, explicitly authorized contracts; do not repurpose Development routes as public commands.
- Phase09 code includes `src/ArginTaxPayer.Application/Signing/` custody and signing contracts, associated persistence migrations, and `scripts/phase09-verify.sh`. The latter explicitly states it runs no migration, deployment or authority request.
- Latest inventoried existing migration filenames include `20261020181500_AddPhase09SigningMetadata.cs`, `20261022140000_AddEncryptedDatabaseSigningCustody.cs` and `20261022150000_AllowDatabaseSigningCustodyReference.cs`; Phase08 invoice migrations include `20261019080000_AddInvoiceReviewWorkflowPersistence.cs` and `20261019090000_AddInvoiceReviewIdempotency.cs`. **Applied migrations on the current test PostgreSQL database remain unverified**. No schema operation authorized or executed.
- `global.json`: .NET SDK `9.0.202` with latestPatch. `apps/web/package.json`: Next `15.5.27`, React `19.1.7`, scripts `test`, `typecheck`, `build`.
- Primary archived SDKs exist at `references/tax-sdk/legacy-0.0.34/` (`.nupkg` + `.zip`) and `references/tax-sdk/modern-2.0.32/` (ZIP + extracted C# source). Repository tree inspection confirmed the extracted modern code exposes invoice packet, submission, inquiry and JWS components. **Archive checksum verification was not executed in GitHub-only inspection**; do that safely on macOS at Step02. No online NuGet reference or dependency required.
- `references/tax-sdk/README.md` was separately authored and revised on branch `docs/tax-sdk-reference-readme`, commit `af81bb30d7df275bb6c1f88616c963ad7c6ef119`. It is **not present** on the current Phase10 branch. Do not silently merge that branch; request a separate decision or cherry-pick only after review.
- `.gitignore` already excludes `.next/`, `.next-dev/`, `next-env.d.ts`, common secrets and local Phase09 acceptance JSON. Operator must confirm `git status --short` is clean and distinguish ignored from historically tracked generated files.

### Scoped workstream freeze

- **Enabled:** general sales of goods/services (verified `inp=1` eligible types), contracting (verified `inp=4` type1), with each pattern's officially required/conditional fields, original and legally permitted correction/cancellation/sales return.
- **Excluded:** foreign exchange, gold, bills, air travel, export, and other special patterns; complete Phase11 inquiry and Phase12 commercial finance.
- **Future-proofing:** versioned schema/field matrix, strategy mapper/validator, shared transport/signing/idempotency/audit, UI metadata, no fallback of unsupported type/pattern to general sales.
- **Authority approval:** no real invoice send without separate owner authorization; an HTTP acknowledgment is not final fiscal acceptance. No production schema modifications, deployment, merge, tag or release.

### Step01 operator acceptance (not yet independently executed)

1. On macOS, fetch and switch to `phase/10-invoice-submission` with a clean worktree (do not discard local changes). Verify `git rev-parse HEAD`, `git merge-base main HEAD`, GitHub tag and working tree status.
2. Run `dotnet --version`, `dotnet restore ArginTaxPayer.sln`, `dotnet build ArginTaxPayer.sln --no-restore`, and `dotnet test ArginTaxPayer.sln --no-build`; record errors and exact summary. For web: `cd apps/web && npm ci && npm test && npm run typecheck && npm run build`. These are build/verification only, not live authority operations.
3. Check `zsh scripts/dev-status.zsh` only if a status view is desired; no service startup or DB migration is required for Step01.
4. At a subsequent Ubuntu acceptance checkpoint, fetch **the same Step01 commit SHA**, verify exact match and perform independent build/tests; never mix macOS and Ubuntu evidence from different SHAs.
5. Audit `git status --short` after tests; generated/local files must remain untracked/ignored. Record any failures rather than assuming success.

### Implementation/evidence checklist

| Item | Evidence | Status |
| --- | --- | --- |
| GitHub main, phase branch and Phase09 release | GitHub read-only inspection | Verified |
| Roadmap and Phase07/08/09 boundaries | Repository Markdown inspection | Verified |
| Existing invoice pattern and reference rules | Repository C# source inspection | Verified |
| SDK sources and archives | Repository tree inventory | Verified presence, binary integrity pending |
| Migration filenames / model inventory | Repository tree | Inventoried, database application unverified |
| Design and 24-step freeze | This document | Documented |
| macOS .NET/Node build and tests | Operator commands above | Pending |
| Ubuntu same-SHA verification | Future acceptance gate | Not performed |
| Live Tax Authority request | Explicitly excluded in Step01 | Not performed |
| Owner Step01 acceptance | Await operator report | Pending |

**Step01 signoff gate:** owner reviews macOS results and this evidence register. Only after explicit signoff begin Step02 official document/SDK traceability. Add the operator's exact tested SHA, outcomes, and any new exceptions to this same document.
