# ArginTaxPayer — Phase 10: Tax Authority Invoice Submission (Two-Pattern Delivery)

**Status:** Steps 01–16 owner-approved. Step17 compact RTL invoice dispatch status panel, disabled send/inquiry controls and regression tests committed; macOS UI acceptance pending. No live invoice send, no DB migration. Ubuntu remains Step23.  
**Plan date:** 2026-10-23.  
**Repository:** HekmatBeigverdi/ArginTaxPayer  
**Base branch / inspected SHA:** `main` / `7112f03b7fe30ceea1aff7f04f587a1b0e939c92` (verify again at Step 01; Phase09 merge/release anchor `fde20594e733f96e032396f8e22e8781b14cac18`).  
**Phase branch:** `phase/10-invoice-submission`  
**Prior release:** `phase-09-v1.0.0`  
**Fixed step count:** **24**. Do not renumber/extend without separate owner-approved scope amendment.  
**Execution scheduling:** macOS tests during implementation; Ubuntu VPS review and same-SHA acceptance occur only in Step 23 (penultimate step), with closeout in Step 24. Step22 is macOS-controlled live authority acceptance, not Ubuntu.  
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
| Quality + real authority | 20–23 | macOS-controlled live submission, then Ubuntu same-SHA verification specifically in Step23, regression/security and future-pattern dry run |
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
| 22 | macOS controlled live invoice acceptance | 21; live-send approval; legitimate invoice data | macOS live API response and immutable receipt proof; report inconclusive outcomes honestly. | Not started |
| 23 | Ubuntu same-SHA acceptance and final extensibility audit | 20–22 | Ubuntu same-SHA build, tests, authenticated smoke only when safe, plus synthetic unsupported-pattern dry-run (must fail closed) and manual security checklist. | Not started |
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

### Step 22 — macOS controlled live invoice acceptance

- **Goal / change scope:** On macOS, with owner permission and properly authorized real taxpayer (e.g., Negin), submit one legitimate valid invoice, capture reference and distinguish acknowledgment from acceptance.
- **Dependencies / prerequisites:** 21; live-send approval; legitimate invoice data.
- **Acceptance criteria:** macOS live API response and immutable receipt proof; report inconclusive outcomes honestly.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Independent operator output and environment/SHA parity required.
- **Deliverable:** macOS controlled live-authority acceptance evidence or explicitly documented blocker.
- **Execution status:** Not started. Await owner approval and previous step signoff.

### Step 23 — Ubuntu same-SHA acceptance and final extensibility audit

- **Goal / change scope:** First perform independent Ubuntu VPS build, tests and security checks on the exact macOS-tested SHA; then review field/type/operation matrix, documentation, security debt and future-pattern extensibility.
- **Dependencies / prerequisites:** 20–22.
- **Acceptance criteria:** Ubuntu same-SHA build, tests, authenticated smoke only when safe, plus synthetic unsupported-pattern dry-run (must fail closed) and manual security checklist.
- **Tests / evidence:** Record exact command, environment, SHA, objective results, failures, skipped tests and security assertions. Focused domain, HTTP, persistence and/or UI fixtures covering authorized success, negative cases, concurrency and regression as applicable.
- **Deliverable:** Ubuntu acceptance evidence, closeout readiness and future-pattern implementation guide.
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
4. Defer **all Ubuntu execution** until Step23: there compare against the final macOS-tested source SHA; no Ubuntu server checks in Steps01–22.
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
| Ubuntu same-SHA verification | Step23 only | Not performed |
| Live Tax Authority request | Explicitly excluded in Step01 | Not performed |
| Owner Step01 acceptance | Await operator report | Pending |

**Step01 owner decision (2026-10-23):** Owner explicitly approved Step01 in chat. Actual macOS execution outputs were not supplied in the approval message and therefore remain unverified evidence, not asserted PASS. Step02 is authorized; its own signoff must be separate. Only after explicit signoff begin Step02 official document/SDK traceability. Add the operator's exact tested SHA, outcomes, and any new exceptions to this same document.

## 11. Step 02 — Official specification and archived SDK comparative matrix (2026-10-23)

**Status:** Source review and design/fixture contract completed on repository branch; **no app code, tests, live calls or database changes**. Await macOS operator read-only/hash/tests and owner acceptance. Step03 is blocked.

### 11.1 Authority/source precedence and inspection boundaries

1. **Normative:** Owner-provided Iranian Tax Authority issuance specification `RC_IITP.IS V7.9` (Tir 1405; 116-page PDF), as inventoried and table/page-cited in the authoritative Phase07 document `docs/phases/phase-07-invoice-domain-rule-engine.md`. This source PDF itself was **not accessible for page-by-page reinspection in this GitHub-only step**, so table/page claims below are transcribed from Phase07 evidence and remain independently verifiable. Exact applicability/effective dates and newer amendments are **unresolved submission gates**, not inferred from publication month.
2. **Primary implementation evidence:** checked-in `references/tax-sdk/legacy-0.0.34/TaxCollectData.Library.0.0.34.nupkg` and `.zip` and extracted `references/tax-sdk/modern-2.0.32/tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32/` plus accompanying ZIP. Their presence verified; binary ZIP/NUPKG contents and fresh SHA256 hashes **not independently extracted or calculated during this GitHub step**. NuGet online URL is intentionally excluded.
3. **Prior security evidence:** `docs/phases/phase-09.md` archives SHA256 evidence and compares legacy 0.0.30 source vs 0.0.34 binary, modern 2.0.32 source and package, legacy RSA PKCS#1 v1.5 signature vs certificate/JWS path. Hashes recorded in Phase09 are **historical evidence, not new verification**.
4. Secondary web-accessible copies of issuance instructions indicate ordinary-sale pattern 1 and contracting pattern 4 and registered contract identifier, but **are not substituted for the provided V7.9 authority original**. No unverified remote NuGet source or live endpoints are part of this step.

**Key scope correction:** Phase07 cites **Type1 Pattern1, Type2 Pattern1 and Type1 Pattern4** as the eligible type/pattern candidates within Phase10's two pattern families. V7.9's overall table contains many additional patterns; these are documented only for future addition and **must remain disabled**. Type3 has no regular Type1/Type2 pattern and is not a supported Phase10 transmission type. Cancellation's external type/pattern presence is a subject-specific exception; classification remains internal.

### 11.2 Official type/pattern/subject matrix (Phase07 V7.9 table evidence)

| Classification/subject | Source | Phase10 decision / fixture |
| --- | --- | --- |
| Type1, `inp=1` general goods/services | V7.9 Tables1 p14, 9 p31 | ENABLED after effective-rule review; `type1-general-original.json` golden baseline |
| Type2, `inp=1` general sale | V7.9 Tables1 p14, 9 p31 | IN SCOPE, conditionally enabled after type2-specific buyer/settlement validation; `type2-general-original.json` |
| Type1, `inp=4` contracting | V7.9 Tables1 p14, 9 p31, 11 pp33–35 | ENABLED only after verified seller contract registration ID (`crn`) and contracting-specific field checks; `type1-contract-original.json` |
| Type2, `inp=4` contracting | V7.9 Table9 p31 (not listed) | DENY: `unsupported-pattern-type` fixture |
| Subjects original 1, correction 2, cancellation 3, sales-return 4 | V7.9 section5 pp15–18, Table10 p32 | Explicit source-grounded allowed operation check for **each** enabled type/pattern |
| `irtaxid` references | V7.9 Table8 p30; Table10 p32 | REQUIRED for correction/cancellation/return, FORBIDDEN on original; verify referenced-invoice existence/status and authorized lineage before sending |
| Cancellation external `inty`/`inp` | V7.9 Table10 p32, Phase07 Step09 | Omit from outbound cancellation payload, preserve typed pattern classification internally; negative fixture forbids accidental inclusion |
| Seller registration contract ID `crn` | V7.9 Table11 pp33–35 | Conditional/required for contracting original; reject unrelated `crn` on ordinary original; verify business-valid registration ID with authority before live send |
| Non-enabled special patterns | V7.9 Tables1/9, Phase10 owner scope | DENY by explicit phase feature policy even if domain's historical legal matrix describes them |

No item in this table is a license to submit until the official document's **effective date and current amendments**, other conditional validations, and signing wire profile are verified.

### 11.3 Required/conditional field contract for the two patterns

This is a **source-traceability schema**, not an assertion that every named field is legally mandatory. Classify each individual field as Required, Conditional, Optional or Forbidden by specific V7.9 table row in the implementation-stage detailed mapping; an SDK C# nullable type is not legal requiredness.

| Segment | Candidate official JSON fields evidenced by archived 2.0.32 DTO | Grounded validation and source | Future golden fixture gate |
| --- | --- | --- | --- |
| Common identity/header | `taxid`, `indatim`, `indati2m`, `ins`, `inty`, `inp`, `inno` | V7.9 Table3 p24, Table4 pp25–26; correct unique tax ID, time and rules; outbound `inty/inp` depend on subject | Reject malformed/time-invalid ID, mismatched memory, forbidden cancellation fields |
| Parties and purchaser | `tins`, `tob`, `tinb`, `bid`, `bpc`, `sbc`, `bbc` | Phase07 V7.9 conditional buyer/person/branch tables (Table11 pp33–35); distinguish Type1 vs Type2; check full official table per field | Two types with appropriate conditional buyer fields; no fabricated buyer |
| Subject and contracting | `irtaxid`, `crn` | V7.9 Tables8,10,11; contract registration ID is not a local contract note; references link immutable prior invoices | Subject original/referring and contract ID presence/format/ownership |
| Totals and settlement | `setm`, `tprdis`, `tdis`, `tadis`, `tvam`, `todam`, `tbill` | V7.9 Tables15–20 pp39–42 and settlement-specific rules; aggregate exactly from immutable line snapshots | Whole-rial totals, appropriate type/subject settlement, invalid reconciliation blocked |
| Line catalog and prices | `sstid`, `sstt`, `mu`, `am`, `fee`, `prdis`, `dis`, `adis`, `vra`, `vam`, `tsstam` | V7.9 Tables29–32 pp50–52 and Tables40–44 pp58–63; `sstid` exactly 13 ASCII digits; `sstt` optional max400, `mu` fiscally optional but local catalog resolution required; `am` positive max18 integer/8 fractional digits | Unit/reference snapshot validity, numeric precision, tax classification, line/header reconciliation |
| Payments | `Payments[]` with `pdt`, `pmt`, `pv`, `iinn`, `acn`, `trmn`, `trn` etc. | SDK `Dto/PaymentItemDto.cs` shows field **shape only**; official cash/credit/terms and applicability need table-by-table confirmation | Missing permitted/required settlement elements, no invented payment transaction |
| Data container | `Header`, `Body[]`, `Payments[]`, `Extension[]` | SDK `Dto/InvoiceDto.cs` is serializer evidence; verify required/omitted/null behavior against official wire edition | Exact canonical serialized bytes and prohibited field omission |

**Critical prior regression rule:** Phase07 Step10 documents V7.9 general truncation requirements; never reuse draft-only `InvoiceRoundingPolicy` midpoint choices for fiscal submission without implementing verified source-grounded truncation. Unit/catalog and VAT classification may be unresolved, which **blocks send rather than defaulting VAT to zero**.

### 11.4 SDK wire/auth comparison and source paths

All below refer to extracted `references/tax-sdk/modern-2.0.32/tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32/TaxCollectData.Library/` unless labeled legacy.

| Topic | Inspected exact path | Finding / Phase10 design gate |
| --- | --- | --- |
| Invoice DTO | `Dto/HeaderDto.cs`, `Dto/BodyItemDto.cs`, `Dto/InvoiceDto.cs`, `Dto/PaymentItemDto.cs` | Names and structural types found, including `crn`, `irtaxid` and `inp`; does not prove field requiredness or current API acceptance |
| Submission / response | `Clients/TaxApi.cs`, `Clients/LowLevelTaxApi.cs` | `SendInvoicesAsync` exists; maps UID/reference/TaxId; validate partial/reordered/duplicate results and preserve unknown outcomes |
| Packet sequence | `Providers/PacketProvider.cs` | `CreateInvoicePacket` invokes signing **then** encryption middleware; exact signature canonicalization/padding/serialized bytes must be fixture-pinned |
| Nonce auth | `Clients/RestSharpHttpClient.cs` | Requests nonce then creates signed token/Authorization; avoid raw response-body error leakage and token logging |
| Modern signature | `Cryptography/JwsSignatory.cs` | JWT/JWS RS256 with `x5c`, `sigT`, `typ`, `crit`, `cty` headers; separate certificate-backed wire path, **not equivalent to legacy simple RSA/Base64** |
| Modern URL construction | `Properties/DefaultUrlProperties.cs` | Version marker `v2`; uses `Path.Combine` to form URL, **not safe to adopt blindly across macOS/Windows/Linux**. Use reviewed URI combination and fixed allowed destinations |
| Old SDK evidence | `docs/phases/phase-09.md` source profile `legacy-key-source-0.0.30-rsa-sha256-pkcs1-base64` | Offline documented: RSA-SHA256 PKCS#1 v1.5, UTF8 canonicalization and ordinary Base64; **0.0.34 NuGet binary parity unverified** |
| Signing ownership | Phase09 `Application/Signing` and custody contracts | Reuse owner-scoped server custody and authorize signing on immutable invoice hash. Never migrate to JWS/certificates implicitly |
| Dependencies | Modern `TaxCollectData.Library.csproj` | `netstandard2.0`, SDK `2.0.32` and third-party package dependencies observed; not added to the app or executed |

**Version decision:** No live protocol selected by Step02. Carry two explicitly separated candidate adapters (`legacy-key` and `modern-certificate-JWS`) in comparative design only. The Phase09 existing owner-approved key-based profile is favored for reuse if live authority support is verified. If V7.9/current authority wire rules require a certificate not available to the taxpayer, stop and request owner decision, do not auto-enable insecure bypasses.

### 11.5 Golden fixture and negative-test catalogue (specification only)

Proposed deterministic synthetic fixtures, to be created in implementation steps only (no new test files in Step02):

- `type1-general-original.json`, `type2-general-original.json`, `type1-contract-original.json`.
- `type1-general-correction.json`, `type1-general-cancellation.json`, `type1-general-sales-return.json`.
- `type2-general-correction.json`, `type2-general-cancellation.json`, `type2-general-sales-return.json` **only where confirmed legal**.
- `type1-contract-correction.json`, `type1-contract-cancellation.json`, `type1-contract-sales-return.json` **only where confirmed legal**.
- `deny-type2-contract.json`, `deny-other-pattern.json`, `deny-no-crn.json`, `deny-no-irtaxid.json`, `deny-original-with-irtaxid.json`, `deny-cancellation-with-inty-inp.json`, `deny-invalid-taxid-or-line-id.json`, `deny-unresolved-vat.json`, `deny-unverified-effective-date.json`, `deny-unknown-wire-signature.json`.
- Canonical signature fixtures must be compared byte-for-byte with approved versioned official evidence and fixed clock/nonce. Synthetic keys only; no real tokens, private keys, actual taxpayers, API calls or identifiers.

### 11.6 Implementation blockers and explicit handoffs

| Blocker / unknown | Current evidence | Resolution owner |
| --- | --- | --- |
| Current applicable V7.9 amendments and effective dates | Phase07 archived document/table references; original PDF not re-opened during Step02 | Step03 admission design + Step06/07 final rule gate; official verification mandatory before live enable |
| Missing per-field official table row/condition for all header/payment/contract specifics | SDK DTO inspection plus Phase07 subset | Step06 mapper and Step07 preflight must complete exhaustive two-pattern table row matrix |
| New archive SHA256, old 0.0.34 ZIP vs NUPKG parity | Presence verified, bytes not independently unpacked | Step02 macOS operator inspection and Step03 version architecture |
| Legacy key-based endpoint compatibility | Phase09 offline key profile and reported connection; no independently verified live dispatch | Step08 authenticated read-only live evidence and Step09 signing profile |
| Migration applicability / live DB proof | Files inventoried, no real DB history inspected | Step05 review, owner-approved test migration; macOS only until Step23 |
| Reference/subject-specific requiredness and permissible operations | V7.9 tables as transcribed by Phase07 | Step15 per-pattern/per-operation legal fixtures |
| Official packet encryption/canonicalization and error responses | Modern code signing/encrypt pipeline inspected; legacy parity outstanding | Steps08–10 transport contract fixtures |
| Phase08 bulk import QA and Phase09 backup/restore/audit evidence | Explicit prior release exception | Step20 regression/security and Step23 Ubuntu final review |

### 11.7 Revised platform schedule — owner decision (2026-10-23)

**Owner explicitly directed Ubuntu testing only at the penultimate step.** This supersedes every earlier Gate A–D mention of Ubuntu in Steps01–22. Do not run `bash scripts/ubuntu-dev.sh verify`, server tests, VPS deployments or Ubuntu service checks before **Step23** unless owner separately changes the plan. Step22 remains **macOS-controlled owner-authorized live authority submission**. Step23 performs the first Ubuntu full independent build, tests, validated test DB and same-SHA parity checks. Step24 handles owner-gated closeout and release. If Step23 discovers a platform bug, fix on the phase branch and **retest the identical corrected SHA on macOS and Ubuntu within Step23** before closure; never accept mismatched SHA.

### 11.8 Step01 acceptance and Step02 verification status

- Step01: **owner approved** in chat 2026-10-23; no macOS output attached to that approval, so do not claim that its commands succeeded.
- Step02: **owner-approved 2026-10-23**. GitHub source, DTO and official provenance review performed; documented matrix and test case design above. No application code, operational tests, database migrations, official send, deployment, merge or release performed. Exact macOS output was not included in the acceptance turn; do not fabricate it.
- Operator macOS checks before owner accepts Step02:
  1. `git rev-parse HEAD` matches the Step02 document commit; `git status --short` clean.
  2. `shasum -a 256` over local ZIP/NUPKG files; compare original Phase09 evidence hashes **only when filenames/artifacts match**.
  3. `unzip -l` (read-only) archives; flag binary-only vs source and suspicious samples; never execute archives.
  4. Targeted `dotnet test` on Phase07 patterns/rules and Phase09 signing test classes; `dotnet build` for regression.
  5. No Ubuntu, DB migration or live authority operation.

**Step02 owner decision:** Approved by owner in chat on 2026-10-23, authorizing Step03. No claim of passing tests without attached operator output.

## 12. Step 03 — Connector architecture, security boundaries and extensible pattern contracts (2026-10-23)

**Status:** Detailed architecture and extension contract documented. No runtime service, schema, migration, UI, package installation or live authority call in this architecture step. **Owner acceptance and macOS regression review pending.**

### 12.1 Decisions locked for Phase10

**Decision P10-ADR-001 — Reuse the existing invoice source of truth.** `InvoiceAggregate` (`src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs`) is the customer-owned invoice domain. `InvoicePatternMatrix`, `OfficialV79InvoicePatternMatrix`, `OfficialV79TypePatternRuleFactory` and `InvoiceRuleSetRegistry` already hold official classification/provenance/read-only validity rules. Do not duplicate these as separate mutable tax engine, recreate invoice tables by pattern, or allow an SDK DTO to own business state. The dispatch layer reads a server-authorized immutable invoice revision and builds a distinct wire snapshot.

**Decision P10-ADR-002 — Independent eligibility gates.** A documented legal combination is **not** a feature switch, and a feature switch is **not** proof of legal applicability. Authorize submission only when all independent gates pass: authenticated server-resolved ownership; taxpayer↔active fiscal-memory and Phase09 key version match; approved/immutable invoice revision; legal type+pattern+subject combination; Phase10 explicitly allowed pattern; effective-dated source and complete required-field evidence; eligible official seller/buyer/catalog/VAT; correct wire/signature profile and authorized endpoint; durable dispatch admission/idempotency. Any unknown results in a typed denial, not a fallback.

**Decision P10-ADR-003 — Narrow enabled set.** The initial Phase10 allowlist contains precisely `(First, 1)`, `(Second, 1)` and `(First, 4)` as **candidates** to become send-enabled after explicit regulatory applicability/field coverage gates. `(Second,4)`, Type3 and all other combinations are denied irrespective of general domain catalogue membership. Whether a particular referring operation is enabled depends on the separate official subject/operation policy. Only Pattern1 (ordinary goods/services) and Pattern4 (contracting) belong to this phase.

**Decision P10-ADR-004 — Three independent concerns: pattern policy, mapper, transport.** No pattern-specific logic enters the networking/signing/retry engine. A future pattern can supply a source-pinned definition, field and subject policy, deterministic DTO mapper, test fixtures and UI metadata without modifying dispatch state transitions or idempotency.

**Decision P10-ADR-005 — Shared submission context, tenant isolation and auditable records.** All operations are scoped by authenticated `CustomerUserId`, `TaxpayerId`, `FiscalMemoryId`, invoice ID and revision, with ownership resolved from the database at command execution, never from browser-provided IDs alone. Level1/Level2 role and partner assignment are checked against current policy; reading/report access is not signing permission. The server—not user JSON—selects key version, custody, signing profile and allowed endpoint.

**Decision P10-ADR-006 — Immutable wire preparation.** Mapping produces canonical UTF-8 JSON from an immutable, official-rule-verified invoice snapshot, pinned to `RuleSetEdition`, `MapperVersion`, pattern/type/subject and source documents. Signature and encrypted packets derive from exactly these bytes. Store safe hashes and identifiers before the external request. On any mismatch or stale revision, deny and require a new deliberate operation.

**Decision P10-ADR-007 — Audit and result separation.** The internal workflow status, transport dispatch status and official response/acceptance status are separate. An HTTP 200 or reference receipt never automatically marks a fiscal invoice accepted. Uncertain response forbids blind resend. Durable outbox/idempotency and narrow reconciliation are designed in Steps04–05/11–14; complete periodic inquiry remains Phase11.

**Decision P10-ADR-008 — No runtime SDK dependency by default.** The bundled SDKs under `references/tax-sdk/` are version-pinned comparative evidence, not a DI-registered shared singleton or a production NuGet dependency. Build native, audited scoped adapters only when protocol evidence is validated. A legacy RSA/base64 signature and a modern certificate/JWS+encryption packet must never be interchanged implicitly. No online package fallback.

### 12.2 Required bounded component contracts (design-only, names illustrative)

| Layer / responsibility | Proposed abstraction | Inputs / responsibilities | Hard constraints |
| --- | --- | --- | --- |
| Domain | Existing `InvoiceAggregate`, `InvoiceSubjectSelection`, `InvoicePatternMatrix`, `InvoiceRuleSetRegistry` | Immutable invoice snapshots, versioned legal rule evidence | No SDK or HTTP coupling; never grants dispatch permission alone |
| Application | `IInvoiceDispatchAuthorization` | Authenticated actor/session and current scoped invoice/memory/role/key status | Deny impersonation, former partner and read-only privilege escalation |
| Application | `IInvoicePatternDefinitionProvider` | Exact `Type, Pattern, RuleSetEdition`, issue UTC and subject | Immutable catalogue, no default-to-latest or wildcard fallback |
| Application | `IInvoiceSubmissionEligibility` | Tenant authorization + approved revision + verified field coverage + allowlist | All gates must pass, stable localized denial codes |
| Application | `IInvoicePayloadMapper` strategy | Immutable invoice, full Phase06 source snapshots, official version and pattern definition | Deterministic `Header/Body/Payments/Extension` wire DTO; no new tax computations from user input |
| Application | `IInvoicePacketPreparation` | Server-selected immutable payload bytes and signed intent | Snapshot/hash pinning, no raw private-key return |
| Application | `IInvoiceDispatchOrchestrator` | Authorized dispatch command with idempotency key | One operation per pinned revision and taxpayer/memory identity; audit-before-send |
| Infrastructure | `IInvoiceSigningProfileAdapter` | Authorized signing request through Phase09 `IDigitalSigningService` | Never expose Sign(anyBytes); no process-global signer or cross-tenant credential caching |
| Infrastructure | `ITaxAuthorityTransport` | Approved endpoint/profile and already prepared secured packet | TLS verification, timeouts, no unchecked redirection or arbitrary URL |
| Infrastructure | `IDispatchOperationStore` | Transaction-scoped attempt, result, reference and trace IDs | Durable uniqueness/CAS/outbox; Stage05 migration is separately owner-gated |
| API/BFF | `InvoiceSubmissionEndpoints` | Authenticated prepare/submit/receipt/history operations | CSRF, rate limits, no-store, no raw provider errors |
| Web UI | Pattern-aware view model | Field schema and backend decisions | RTL compact sectional modals; server-side validation authoritative |

**Dependency rule:** `Web/UI → API/BFF → Application → Domain`; Infrastructure implements application ports. No Domain reference to SDK/HTTP/EF/Next.js. No browser access to signing keys or Tax Authority credentials.

### 12.3 Immutable pattern definition contract

Use an internal **composite identity**:

`PatternKey = (ElectronicInvoiceType, PatternCode, RuleSetEdition)`

Submission eligibility additionally requires a **rule effective date**, source trust level, supported `InvoiceSubject`, approved requiredness completeness and explicit Phase10 allowlist. Include a stable `MapperVersion` in prepared packets. A future rule revision creates a new definition; past invoices remain pinned to their original version.

Each `InvoicePatternDefinition` must contain:

- **Official provenance:** document ID/version/table/page, effective UTC range and applicable issuer/business context; no automatic floating to newest rule.
- **Fields:** official JSON field name and path; relevant invoice type/subject; `Required | Conditional | Optional | Forbidden`; machine-readable predicate for conditional cases; type/scale/max length/allowed values; Persian title/help text; server validation key; omit/null serialization semantics. A C# nullable field **does not mean optional by law**.
- **Operations:** explicit per-pattern+type+subject validity for original, correction, cancellation, sales return and reference obligations; no deletion of transmitted tax invoices.
- **Presentation:** section name/order, control kind, searchable entity picker, date type and conditional visibility. Presentation is a projection of domain metadata, not its authority.
- **Evidence and tests:** official rule source, golden JSON samples and negative fixtures, effective-date regression, subject/reference tests and serialization byte stability.

For Phase10 the concrete mapper strategies are conceptually:
- `GeneralSaleType1Mapper` for `(First,1)`.
- `GeneralSaleType2Mapper` for `(Second,1)`, avoiding Type1 buyer/payment field assumptions.
- `ContractingType1Mapper` for `(First,4)`; requires official `crn` from a validated registered contract source (not free-form contract note).
- Shared `ReferenceSubjectProjection` for correction/cancellation/return; subject 3 cancellation may require **omitting outbound `inty` and `inp`** while preserving internal type/pattern classification.

**Never** create `mapDefault()` that serializes an unknown pattern as general sale.

### 12.4 Proposed submission pipeline and failure gates

```text
Authenticated customer action (or explicitly authorized delegated operation)
    |
    v
Current owner + taxpayer + fiscal memory + role + key scope resolution
    | (deny when ambiguous)
    v
Existing InvoiceAggregate/revision + Phase08 approved workflow snapshot
    |
    v
PatternKey + Phase10 enablement + official effective rules + subject eligibility
    | (unsupported, unverified, absent fields => deny)
    v
Requiredness and numeric/catalog/VAT validation [server-side]
    |
    v
Pattern-specific DTO mapper (no IO, deterministic) -> immutable UTF-8 snapshot/hash
    |
    v
Authorized Phase09 signing request + version-pinned wire profile
    | (signature mismatch, no trusted key => deny)
    v
Durable dispatch reservation/outbox + idempotency identity
    |
    v
Official authority transport (strict TLS and endpoint allowlist)
    |
    +-- explicit accepted receipt -> persist reference and attempt
    +-- explicit terminal error -> persist safe code and evidence
    +-- timeout/unknown -> persist indeterminate, reconcile before any retry
```

Ordering of the durable reservation relative to signing and transport must be finalized with transactional invariants in Steps04–05/09–11. **No external request occurs until an unambiguous durable operation identity exists**; an external request itself must never be inside a database transaction held open over network I/O.

### 12.5 Tenant, actor and secrecy matrix

| Actor/operation | Own authorized taxpayer | Other customer taxpayer | Level2 former/foreign owner | Level1/Level2 reporting |
| --- | --- | --- | --- | --- |
| Preview permitted invoice fields | According to verified active membership | Deny | Deny | Scoped read where policy allows |
| Initiate signing/submission | Explicit dedicated permission + active memory/key | Deny | Deny | No blanket privilege from administrative role |
| Read status/receipt/audit | Scoped | Deny | Deny | Only scoped, redacted support views |
| Reconcile uncertain attempt | Owner or separately authorized operator | Deny | Deny | Explicit reviewed support path, full audit |
| Read/export private keys or authority token | Never | Never | Never | Never |

Auth session, MFA freshness, current partner assignment and taxable party ownership are checked server-side for each command. A user's supplied taxpayer/memory GUID or a role claim alone proves nothing. External provider errors are normalized without raw bodies, sensitive headers, signatures or private material.

### 12.6 Later implementation plan and decision gates

| Step | Required realization of Step03 design |
| --- | --- |
| 04 | Dispatch+authority state machines, typed denials and transitions |
| 05 | Outbox/idempotency schema proposal and migration review; no unapproved application |
| 06 | Actual pattern DTO strategies and golden output JSON for 1/1, 2/1, 1/4 |
| 07 | Comprehensive source-verified field presence, fiscal sums, catalog and immutable approval gates |
| 08–09 | Authenticate existing fiscal memory; safely integrate existing Phase09 custody/signing profile |
| 10–15 | Transport, durable idempotency, receipts, uncertainty and allowed referencing subjects |
| 16–19 | Scoped API/BFF and metadata-derived Persian RTL forms/status/recovery |
| 20–22 | macOS integration/regression and owner-authorized controlled live invoice test |
| **23** | **Only Ubuntu VPS verification phase; compare exact macOS SHA and retest any platform fixes** |
| 24 | Explicit owner-controlled merge/tag/release |

### 12.7 Step03 architecture acceptance cases (static design review)

1. **Pattern catalogue vs submit eligibility:** `OfficialV79InvoicePatternMatrix.Create().Evaluate(...)` may report document compatibility, but its `VerifiedForSubmission=false` must prevent an issuance decision. The already existing `RuleSetLookup.IsSubmissionAuthorized=false` and `SigningResult.IsFiscalSubmissionAuthorized=false` remain intact.
2. **Unapproved pattern:** Type1 Pattern3, Type2 Pattern3, Type2 Pattern4, Type3 and every unsupported new pattern are denied at the phase allowlist, with no fallback.
3. **Incomplete contract:** Type1 Pattern4 absent `crn` denied; Type1 Pattern1 carrying unrelated contract data denied; invalid `irtaxid` and subject references denied.
4. **Legacy and modern wire modes:** no JWS/certificate auto-upgrade or legacy RSA signature reuse in the wrong protocol; mismatched signer and fiscal memory deny.
5. **Privilege:** copied invoice ID, customer ID, or old Level2 delegation cannot authorize another taxpayer's signing.
6. **Immutability:** same invoice revision and rule edition replay map to identical bytes; any revision, mapper change or frozen payload hash change requires new intentional preparation.
7. **Provider timeout:** indeterminate operation cannot be blindly resent; no claim of accepted invoice based only on HTTP 200.
8. **Future extension:** adding a test-only `(First, 5)` or a new official pattern version affects only policy, mapper, rule fixtures and field/UI metadata after explicit owner release; it does not require another invoice aggregate, HTTP client, signing engine or dispatch store.

**Design signoff remains distinct from executable compliance.** No runtime code/tests were created in this step; test commands below run preexisting regressions on macOS. Later steps must implement and automate all listed acceptance cases.

### 12.8 macOS operator review and verification

From a clean checkout of `phase/10-invoice-submission`:

```bash
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase07OfficialV79Tests|FullyQualifiedName~Phase07TypePatternRulesTests|FullyQualifiedName~Phase07InvoicePatternsTests|FullyQualifiedName~Phase09InvoiceSnapshotSigningTests|FullyQualifiedName~Phase09SigningOperationPolicyTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No need to start Next.js or PostgreSQL just to approve this architecture-only step. Do not execute Ubuntu commands or live Tax Authority requests; Ubuntu first becomes mandatory in Step23.

### 12.9 Step03 evidence

- **Reviewed paths:** `src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs`, `InvoicePatternMatrix.cs`, `InvoiceRuleSetRegistry.cs`, `InvoiceTypePatternRules.cs`; `src/ArginTaxPayer.Application/Signing/DigitalSigningContracts.cs`, `SigningOperationContracts.cs`, `SigningConnectionTestContracts.cs`; authoritative Phase07/08/09 and Phase10 documentation.
- **Source-grounded risks:** `IsSubmissionAuthorized=false` in existing registries intentionally denies accidental issuance; source matrix includes excluded patterns, so a separate restrictive allowlist is mandatory; the existing signing service does not confer submission authorization; the modern SDK's JWS/nonce stack is not inherently compatible with the legacy Phase09 key signature.
- **Files modified:** `docs/phases/phase-10.md` only.
- **Changes not made:** No app files, dependencies, package install, DB migration, PostgreSQL access, macOS/Ubuntu runtime, UI or API endpoint, tag, release or merge.
- **Owner approval:** Step02 and Step03 explicitly approved in chat (2026-10-23). Step03 operator test output was not attached to the approval message; do not claim independently verified PASS.

## 13. Step 04 — Dispatch state machine and sanitized error contracts (2026-10-23)

**Status:** Domain-only implementation and xUnit coverage committed; actual `dotnet` execution on macOS not run by this GitHub-only workflow. Owner acceptance pending. Step05 must not begin automatically.

### Existing-state compatibility

- `InvoiceLifecycleState` (`Draft`, `Validated`, `Finalized`) and `InvoiceReviewState` (`Draft`, `InReview`, `ChangesRequested`, `Approved`) remain unchanged. Neither local finalization nor workflow approval is Tax Authority acceptance.
- `TaxAuthorityProcessingState` (`NotSubmitted`, `Pending`, `Accepted`, `Rejected`, `Unknown`) remains a **separate authority-state domain**; this step does **not** set `Accepted` on a submission acknowledgement.
- Phase09 signing result `IsFiscalSubmissionAuthorized=false` remains unchanged.
- The new `InvoiceDispatchState` tracks one attempt's technical delivery lifecycle only, not final authority validation.

### Implemented immutable dispatch state transitions

| From | Event | To | Constraint |
| --- | --- | --- | --- |
| Prepared | Sign | Signed | A valid server-owned signature is a later application-layer prerequisite |
| Signed | Reserve | Reserved | A durable outbox claim is a later Step05/11 prerequisite |
| Reserved | BeginSend | Sending | No untracked provider call |
| Reserved | FailBeforeSend | RetryEligible | Proven no external send was made; still requires evidence before retry |
| Sending | ReceiptReceived | Acknowledged | Only provider receipt, **not** fiscal acceptance |
| Sending | OutcomeUnknown | Indeterminate | Timeout, ambiguous provider error or lost response; must reconcile |
| Sending | DefinitiveFailure | FailedTerminal | Terminal only with trusted provider evidence, not guessed HTTP semantics |
| Indeterminate | ReconcileReceipt | Acknowledged | Officially grounded matching receipt evidence required by later orchestrator |
| Indeterminate | ReconcileNotReceived | RetryEligible | Verified absence of external commit required; mere lookup failure is insufficient |
| RetryEligible | RetryFromEvidence | Reserved | Reclaim same operation with durable evidence checks |

All other transitions, including `Indeterminate→Sending`, `Indeterminate→Reserved` without evidence, and `Acknowledged→Sending`, are denied. This pure transition policy never performs I/O, persistence, private-key reads, tax transmission or actual provider reconciliation; **an allowed pure transition is not proof that event evidence exists**.

`InvoiceDispatchSnapshot` freezes `OperationId`, `CustomerUserId`, `TaxpayerId`, `FiscalMemoryId`, `InvoiceId`, `InvoiceRevision` and a 64-character SHA-256 payload fingerprint. Proposals return new immutable snapshots and validate exact expected dispatch revision. A future PostgreSQL CAS/unique constraint must enforce the same rule transactionally; in-memory revision comparison alone does not solve races.

### Sanitized typed error contract

`InvoiceDispatchFailureCode` covers access scope, unsupported pattern/signature, unverified rules, invalid invoice, stale revision, duplicate operation, signing/persistence outage, authority rejection/authentication, rate limit, timeout, malformed response and unknown result.

`InvoiceDispatchError.FromCode()` returns only an internal category, stable localization key and operator-review flag. No raw provider error body, credential, signature, customer payload or exception string is exposed. **A category alone is not retry authorization**; unknown outcome requires authoritative reconciliation.

### Test coverage added

`tests/ArginTaxPayer.Tests/Phase10DispatchContractsTests.cs`:
- Initial immutable identities, 64-hex validation and normalized hash.
- Sequential preparation/sign/reservation/send/receipt; no implication of final fiscal acceptance.
- Unknown outcome block, prohibited blind retry, reconciliation to receipt or evidence-based retry.
- Definitive failure terminal, stale revision and source immutability.
- Invalid identity, undefined events/states and safe typed errors.

### Files/side effects

- **New:** `src/ArginTaxPayer.Domain/Invoices/InvoiceDispatchContracts.cs`.
- **New:** `tests/ArginTaxPayer.Tests/Phase10DispatchContractsTests.cs`.
- **Updated:** `docs/phases/phase-10.md`.
- **None:** Migration/SQL, infrastructure adapter, API endpoint, signing invocation, UI, live provider request, Ubuntu command, merge or release.

### macOS acceptance commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10DispatchContractsTests|FullyQualifiedName~Phase07InvoiceLifecycleTests|FullyQualifiedName~Phase08InvoiceReviewContractsTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

Only macOS is required. Ubuntu acceptance remains **Step23 only**. No external authority request or DB access is required.

**Step04 owner decision:** explicitly approved in chat on 2026-10-23. Operator output was not attached to the approval turn, so runtime results are not independently claimed. Step05 authorized.

## 14. Step 05 — Durable dispatch outbox and PostgreSQL persistence proposal (2026-10-23)

**Status:** Design and proposed SQL recorded, pure domain admission/replay guards and xUnit cases committed. **No EF model registration, migration class, applied schema, DB connection, runtime worker, live send or Ubuntu check.** Explicit owner review is needed before writing/using a real migration in the following steps.

### 14.1 Existing tables audited and deliberate non-reuse

Existing repository sources reviewed:
- `Persistence/Invoices/InvoiceStorageEntities.cs` + `InvoicePersistenceModelConfiguration.cs`: `invoice_documents`, `invoice_document_revisions` (immutable historical snapshots), `invoice_operations` (internal subject/ref-slot reservations) and `invoice_reservation_cursors`.
- `Migrations/20261017010000_AddInvoicePersistenceAndOperationClaims.cs` creates those tables and an immutability trigger for invoice revision history. **Do not use `invoice_operations` as an authority transmission log**: it represents a different lifecycle/idempotency namespace.
- `Migrations/20261011120000_AddOutboxMessages.cs`: generic `outbox_messages` stores event `payload_json` as text and a processed timestamp. Do **not** place signed invoices, tokens or raw authority payloads in that generic outbox.
- `Migrations/20261011180000_AddIdempotencyReservations.cs`: generic taxpayer/operation/key uniqueness, but no frozen invoice revision, fiscal memory, canonical payload hash or secure authority receipt.
- `ArginTaxPayerDbContext.cs` references these existing sets. Current model snapshot and running DB migration history must be reviewed before introducing new mappings.

**Decision:** preserve pre-existing tables untouched and propose **dedicated Phase10 tables** `invoice_dispatch_operations`, `invoice_dispatch_attempts`, `invoice_dispatch_receipts`; the first table doubles as a narrow pending-outbox/dispatch lease source without storing sensitive wire payload. There must be **no generic outbox event containing raw tax JSON**.

### 14.2 Proposed relational boundaries and invariants

| Relation | Cardinality | Data classification / invariant |
| --- | --- | --- |
| `invoice_dispatch_operations` | 1 row per logical submit operation | Customer/taxpayer/memory+invoice/revision, idempotency key, official rule+mapper+signature profile versions, immutable payload SHA256, state/CAS revision; owner-scoped |
| `invoice_dispatch_attempts` | 0..N per operation | Immutable timestamped network attempt facts, sanitized result/error code, selected endpoint **identifier** (not arbitrary URL), provider correlation, attempt sequence; **never** bearer/raw packet/private key |
| `invoice_dispatch_receipts` | 0..N per operation | Append-only acknowledgement/reconciliation evidence, receipt identifier and provider reference, bounded redacted metadata, observation UTC; no claim of final Tax Authority acceptance |
| Existing `invoice_document_revisions` | referenced immutable source | Version-pinned local business snapshot; never updated by dispatch |
| Existing `signing_key_versions` / fiscal memory | selected server-side | Key/profile selection checked at execution; no secret key data copied into dispatch tables |

**Required uniqueness:**
- `UNIQUE(customer_user_id,taxpayer_id,idempotency_key)`: same scope/key with a different payload/memory/invoice/revision = **409 conflict**, same identity = replay existing state.
- `UNIQUE(customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision)` or an equally strong issuance reservation and explicit new-authorized-attempt policy. This prevents different idempotency keys from sending the same immutable invoice revision in parallel. Reference/operation lineage must be considered before permitting any new revision resend.
- FK `(customer_user_id,taxpayer_id,invoice_id)` → `invoice_documents` existing scoped alternate key; validate active customer ownership and matching fiscal memory in application *again* at dispatch.
- Unique `(operation_id,attempt_number)` and scoped immutable receipt/attempt identities; receipts with duplicated returned references must be reviewed rather than silently overwritten.
- `operation revision` `>0`, SHA-256 lowercase 64-hex; idempotency key 1..128 constrained at API/application, bounded provider identifier, UTC-only timestamps, no mutable historical business snapshot.

**Atomicity:** In a **short PostgreSQL transaction**, authorize the scope, compare expected invoice revision/frozen hash, insert-or-observe the unique operation and commit reservation. Only after commit may a worker claim and send. A lease-expired `Sending` row becomes `Indeterminate` for reconciliation, **never** directly resent. If DB commit fails, no external send. Provider/network calls never occur while holding a DB transaction. Durable attempts and transitions are serialized with CAS revision and append-only audit.

**Security/RLS:** every repository query and uniqueness domain is scoped to current authenticated owner/tenant and taxpayer; a DB-scoped FK alone does not establish live membership or role authorization. Consider PostgreSQL RLS as defense in depth, not as a replacement for application authentication. No payload, token, private key, raw response body, secrets or sensitive headers in outbox/attempts/receipts/logs. Use safe codes and cryptographic fingerprints only.

### 14.3 Proposed PostgreSQL DDL for operator **review only**

**Do not copy into production or run as an executable migration.** This SQL is an architectural proposal and requires an EF Core migration/model-snapshot alignment, a fully non-destructive rollback policy, FK verification against actual `fiscal_memories` columns, and operator approval before implementation. SQL shown with `CREATE TABLE` is *not applied*.

```sql
-- PROPOSAL ONLY. DO NOT EXECUTE.
CREATE TABLE invoice_dispatch_operations (
    id uuid PRIMARY KEY,
    customer_user_id uuid NOT NULL,
    taxpayer_id uuid NOT NULL,
    fiscal_memory_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    invoice_revision bigint NOT NULL CHECK (invoice_revision > 0),
    idempotency_key varchar(128) NOT NULL CHECK (length(idempotency_key) > 0),
    payload_sha256 char(64) NOT NULL CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
    rule_set_edition varchar(128) NOT NULL,
    mapper_version varchar(64) NOT NULL,
    signing_profile varchar(128) NOT NULL,
    invoice_type integer NOT NULL,
    pattern_code integer NOT NULL,
    subject integer NOT NULL CHECK (subject IN (1,2,3,4)),
    state integer NOT NULL CHECK (state BETWEEN 1 AND 8),
    state_revision bigint NOT NULL CHECK (state_revision > 0),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL,
    claimed_until_utc timestamptz NULL,
    CONSTRAINT fk_dispatch_invoice_scope
      FOREIGN KEY (customer_user_id,taxpayer_id,invoice_id)
      REFERENCES invoice_documents(customer_user_id,taxpayer_id,id) ON DELETE RESTRICT,
    CONSTRAINT fk_dispatch_customer FOREIGN KEY (customer_user_id)
      REFERENCES identity_users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_dispatch_taxpayer FOREIGN KEY (taxpayer_id)
      REFERENCES taxpayer_profiles(id) ON DELETE RESTRICT
    -- Add correct fiscal-memory FK only after inspecting actual model keys.
);
CREATE UNIQUE INDEX ux_dispatch_scope_idempotency
  ON invoice_dispatch_operations(customer_user_id,taxpayer_id,idempotency_key);
CREATE UNIQUE INDEX ux_dispatch_scope_invoice_revision
  ON invoice_dispatch_operations(customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision);
CREATE INDEX ix_dispatch_pending_claim
  ON invoice_dispatch_operations(state,claimed_until_utc,created_at_utc)
  WHERE state IN (3,4,6,7);

CREATE TABLE invoice_dispatch_attempts (
    id uuid PRIMARY KEY,
    operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
    attempt_number integer NOT NULL CHECK (attempt_number > 0),
    endpoint_code varchar(80) NOT NULL,
    started_at_utc timestamptz NOT NULL,
    completed_at_utc timestamptz NULL,
    result_code varchar(100) NULL,
    provider_trace_id varchar(128) NULL,
    UNIQUE(operation_id,attempt_number)
);
CREATE INDEX ix_dispatch_attempts_operation
  ON invoice_dispatch_attempts(operation_id,attempt_number);

CREATE TABLE invoice_dispatch_receipts (
    id uuid PRIMARY KEY,
    operation_id uuid NOT NULL REFERENCES invoice_dispatch_operations(id) ON DELETE RESTRICT,
    attempt_id uuid NULL REFERENCES invoice_dispatch_attempts(id) ON DELETE RESTRICT,
    receipt_kind integer NOT NULL CHECK (receipt_kind IN (1,2)),
    authority_reference varchar(256) NULL,
    authority_uid varchar(256) NULL,
    observed_at_utc timestamptz NOT NULL,
    evidence_sha256 char(64) NOT NULL CHECK (evidence_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_dispatch_receipts_operation
  ON invoice_dispatch_receipts(operation_id,observed_at_utc);
-- Later migration: DB-level UPDATE/DELETE guards on attempts/receipts;
-- if mutable request-state exists, use append-only state events or CAS transitions.
```

**SQL review limitations to resolve before an EF migration:** `invoice_type`/`pattern_code` cannot be non-null for an outbound cancellation's omitted fields unless explicitly designated as **internal classification** columns (they are, in this proposal). FKs on the fiscal memory and receipt attempt→operation cross-check need verification and strengthening; a receipt's optional attempt ID must belong to the **same** operation (later composite FK). The table set above is a DDL sketch, **not** a complete production migration or SQL acceptance proof. Append-only receipt/attempt trigger policy and backup restore/rollback safety require implementation and verification, not merely documentation.

### 14.4 Pure code and tests added (no schema mutation)

- `src/ArginTaxPayer.Domain/Invoices/InvoiceDispatchPersistenceContracts.cs` freezes the scoped `InvoiceDispatchAdmission` key and `CompareReplay` decision (`ReplaySameOperation`, `ConflictingPayload`, `DifferentKeyOrScope`). Requires valid UUIDs, positive invoice revision, 64-hex payload digest and safe 1..128 printable ASCII idempotency key. Hash is canonicalized to lowercase.
- `InvoiceDispatchStoragePolicy` is a **fail-closed planning rule**: only `Reserved` is directly claimable; `Sending` and `Indeterminate` require external reconciliation; attempts are not deletable. Real lease claims, CAS, ownership and provider evidence require PostgreSQL transactions later.
- `tests/ArginTaxPayer.Tests/Phase10DispatchPersistenceContractsTests.cs` checks exact same-operation replay, changed payload/memory conflict, cross-tenant separation, malformed idempotency keys and no blind worker redrive.

**Important distinction:** these tests are pure in-memory contract tests. They **do not** verify PostgreSQL constraints, concurrent transactions, WAL/outbox restart, immutability triggers, or live authority behavior. Those are mandatory in Steps11–14 and Step20, on an expressly authorized test DB.

### 14.5 Owner approval gates and future implementation

1. **This Step05:** approve schema *design and SQL review only*, plus pure contract tests. No migration execution is requested.
2. **Before EF migration implementation/application:** explicit owner approval of concrete migration SQL, data preservation and rollback plan; verify `current_database() = 'argintaxpayer_test'`, never production.
3. **Step11:** implement atomically persisted reservation and unique indexes, same tenant+key replays, conflicting payload 409, concurrent multi-worker admission, then authorized test DB validation.
4. **Step12–14:** crash/network fault, evidence-driven recovery and durable status history; append-only immutability DB triggers and safe logging.
5. **Step23:** first Ubuntu checks, same final SHA as macOS; no Ubuntu now.

### 14.6 macOS-only Step05 verification commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10DispatchPersistenceContractsTests|FullyQualifiedName~Phase10DispatchContractsTests|FullyQualifiedName~Phase07InvoiceLifecycleTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

**Step05 owner decision (2026-10-23):** explicitly approved in chat; separate macOS test logs were not supplied with the approval. No runtime/database evidence is fabricated. Step06 authorized.

## 15. Step 06 — Deterministic two-pattern invoice wire projection (2026-10-23)

**Status:** Restricted source code and xUnit test fixtures committed. **No successful local compile/test claim yet**, no production-authorized payload, no signing/transport/DB migration and no Ubuntu work. Step07 remains blocked until macOS tests and explicit owner acceptance.

### 15.1 Scope and explicit security limitations

**Supported candidate projections:** ordinary goods/services `(Type1, inp=1)` and `(Type2, inp=1)`, plus contracting `(Type1, inp=4)`. These are **candidate JSON projections only**; they are not permitted to send until Step07 proves complete official rules, effective dates, participants, reference lineage, settlement, VAT/classification and authorization. Out-of-scope patterns are rejected.

**Existing domain reuse:** `InvoiceTypePattern`, `ElectronicInvoiceType`, `InvoiceSubject`, `OfficialV79InvoiceSubjectPolicy`, `OfficialV79InvoicePatternMatrix.Edition`, `InvoiceValidationReceipt.IsSha256`, existing Phase06 product/catalog metadata and Phase07 invoice/financial aggregates. A server-side application adapter (future Step07/16) must read a trusted `InvoiceAggregate` revision and verified Phase06 immutable line snapshots before constructing `InvoiceWireInput`. **Never accept `InvoiceWireInput` directly as an arbitrary client JSON API command.**

**Source:** Phase07 record of owner-provided `RC_IITP.IS V7.9`, tables 3–4, 8–11, 15–20, 29–32, 40–44; local modern SDK `Dto/HeaderDto.cs`, `BodyItemDto.cs`, `InvoiceDto.cs`, `PaymentItemDto.cs` as **shape examples**, not regulatory proof. The original PDF was not re-opened for this step and current applicability is not independently confirmed.

### 15.2 Files and contracts

- Added `src/ArginTaxPayer.Domain/Invoices/Phase10InvoiceWireMapper.cs`.
- Added `tests/ArginTaxPayer.Tests/Phase10InvoiceWireMapperTests.cs`.
- Domain-only `InvoiceWireInput`, `InvoiceWireLine`, `InvoiceWirePayment`, `InvoiceWireProjection`, `Phase10InvoiceWireMapper.Project`; `JsonSerializer` deterministically emits a lowercase `header/body/payments/extension` envelope with official short names.
- `InvoiceWireProjection.IsSubmissionAuthorized=false` by construction; result includes frozen rule-set and mapper version. No direct SDK runtime/package dependency.
- Contract number `crn` is mandatory for contracting (1..12 ASCII digits as previous Phase07 implementation), forbidden on ordinary sale. No free-form substitute is accepted.
- `irtaxid` appears on correction/cancellation/return and is absent on original. `inty` and `inp` are **omitted** on cancellation, preserved in the internal `Classification`; cancellation line projection remains a source-specific rule requiring Step07 full review.
- Header and line totals checked for equality, VAT must be explicit; 13-digit `sstid`, decimal quantities and title/unit codes are projected from trusted source values. `tprdis`, `tdis`, `tadis`, `tvam`, `tbill` and `prdis`, `dis`, `adis`, `vra`, `vam`, `tsstam` map directly without recomputing hidden totals.
- **Fail-closed deliberate omissions:** transactional payment details and `setm` rejected until verified; type2 buyer field variants, active fiscal memory/seller identity, legal VAT effective-date rules and specialized subject body requirements are **not yet certified**. Empty `payments/extension` is a projection placeholder, not an assertion that empty arrays are legal under all combinations. This pure mapper is **not production usable** yet.
- Precise JSON serializer setting omits null values; no raw private keys, tokens, signatures or network endpoints inside projected DTO.

### 15.3 Source-based field-and-operation matrix

| Pattern | Core wire fields | Special fields | Subject/reference | Status |
| --- | --- | --- | --- | --- |
| Type1 / ordinary 1 | `taxid/indatim/ins/inty/inp/tins`, common monetary totals, `body` product/price/VAT | `crn` forbidden | Original, referring subjects `irtaxid` | JSON candidate only |
| Type2 / ordinary 1 | Common identity and amount projection | Buyer/settlement-specific attributes require Step07 verification | Same reference rules if officially valid | JSON candidate only, partial type2 field compliance |
| Type1 / contracting 4 | Common identity and amount projection | `crn` required, sourced from registered seller contract | Referring subjects require `irtaxid`; type/pattern omitted on cancellation | JSON candidate only, contractual condition verification pending |
| Out-of-scope combinations | None | None | No default fallback | Denied |

### 15.4 Focused tests added

`Phase10InvoiceWireMapperTests` covers both ordinary invoice types, contracting ID presence, deterministic output, official short JSON field names, header/body total mismatches, reference and cancellation omissions, rejected unapproved patterns, missing/forbidden contract number, settlement/payment not-yet-verified denials, and deliberately false submission authority.

**Important limitation:** these are *source-written* unit fixtures, not published independently certified golden JSON samples. A future Step07 acceptance gate must cross-check byte-for-byte with approved official examples and add precise negative field-requirement fixtures; do not call this source code officially compliant based on these fixtures.

### 15.5 macOS acceptance

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10InvoiceWireMapperTests|FullyQualifiedName~Phase10DispatchPersistenceContractsTests|FullyQualifiedName~Phase07OfficialV79Tests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No macOS local test execution has been performed in this GitHub-only change. If the operator reports an error, correct code on the same phase branch and regenerate a new SHA before approving. **Ubuntu checks remain exclusively Step23.**

### 15.6 Step07 handoff blockers

1. Verify official **full** field-requiredness matrix (Type1/Type2, original/referenced, contracting), correct dates of force, and official sample JSON encoding.
2. Implement authenticated `InvoiceAggregate`-to-`InvoiceWireInput` extraction (not browser-controlled); require persisted snapshot, role/MFA/tenant scope, memory/key association and approved workflow revision.
3. Verify buyer/seller official participant IDs and business validity, and source-valid goods/service/unit/VAT mapping. Reject unresolved tax classifications.
4. Implement conditional settlement and payment contracts and full legal references; eliminate placeholder unsupported operations or explicitly block those combinations. Do not guess seller or payment values.
5. Confirm header totals include all legally applicable fees/taxes; compare official truncation behavior and exact per-field numeric widths.
6. Verify whether empty `payments/extension` values should be **omitted** rather than emitted as arrays, and whether envelope casing is correct for the negotiated endpoint before any send. Keep compatibility separated for legacy vs modern SDK protocols.

**Step06 owner decision:** explicitly approved in chat, 2026-10-23. Test outputs were not attached to approval; no independent PASS claim. The owner authorized Step07, not production use of this mapper.

## 16. Step 07 — Source-gated preflight and immutable signing-candidate snapshot (2026-10-23)

**Status:** Source implementation and xUnit tests committed to Phase10 branch; **no operator/macOS execution evidence**, no EF change, database migration, web feature, signature request or authority HTTP call. Owner acceptance pending.

### 16.1 Exact delivered source

- **New** `src/ArginTaxPayer.Domain/Invoices/Phase10DispatchPreflight.cs`: `InvoiceDispatchPreflightContext`, typed `InvoicePreflightCode`, `InvoiceDispatchPreflightFinding`, `InvoiceDispatchPreparedSnapshot`, `InvoiceDispatchPreflightResult`, `Phase10DispatchPreflight.Evaluate`.
- **New** `tests/ArginTaxPayer.Tests/Phase10DispatchPreflightTests.cs`: focused unit tests for approval/state, rule provenance, type/date/subject/line-count mismatch, unsupported settlement and malformed financial totals, SHA256 and defensive copy.
- Reuses Phase07 `InvoiceLifecycleState.Finalized`, Phase08 `InvoiceReviewState.Approved`, Phase07 `InvoicePatternRegistry` and `InvoiceRuleSetRegistry`, Phase06/07 prepared JSON source from Step06. No unrelated phase tables changed.
- `InvoiceDispatchPreparedSnapshot` pins customer, taxpayer, fiscal memory, invoice ID/revision, business snapshot SHA256, official rule edition, mapper version, internal type/pattern and subject, frozen UTF-8 bytes plus computed SHA256 of exact wire bytes. A caller receives only a **defensive copy**, not an internally mutable byte array.
- `IsFiscalSubmissionAuthorized=false` on both result and prepared snapshot. Source verification/fixture success **cannot** grant signing, storage or authority dispatch permission.

### 16.2 Fail-closed checks before candidate preparation

| Gate | Logic | Failure code |
| --- | --- | --- |
| Scoped invoice identity and immutable revision/hash | Nonempty customer/taxpayer/memory/invoice GUID; invoice revision >0, SHA256 format, validated line IDs | `invalid_context`, `missing_fiscal_memory` |
| Existing workflow | `Finalized` + `Approved`; these are independent statuses and never fiscal acceptance | `invoice_not_finalized`, `review_not_approved` |
| Rule provenance and currency | Pinned V7.9 edition; both officially evidenced type/pattern effective windows and registry source date/coverage required; no "newest" fallback | `rule_edition_mismatch`, `unverified_legal_applicability`, `incomplete_rule_coverage` |
| Pattern/type/subject | Exact equality of local classification and Step06 DTO; exact subject and `irtaxid` relationship | `invoice_type_mismatch`, `invoice_subject_mismatch`, `invalid_reference` |
| Issue timestamp | Outbound Unix milliseconds equals immutable UTC issue date; invalid timestamp fails | `invoice_date_mismatch` |
| Item ownership shape | Count of official projected lines matches frozen local line IDs; duplicate/empty IDs rejected | `invalid_line_snapshot` |
| Preliminary fiscal amounts, contract, references | Existing Step06 restricted Mapper performs its structural `crn`, product code, total, VAT, type2 and referring-subject shape checks | `invalid_payload` |
| Safe errors | Structured `InvoicePreflightFinding` returns fixed message keys; never provider raw text | N/A |

**Important design distinction:** `InvoiceRuleSetRegistry.Resolve(... requireVerifiedDate:true)` and `CoverageReviewed` are necessary gates for *candidate preparation*, **not independently sufficient proof of complete legal compliance**. Current `OfficialV79InvoicePatternMatrix` has `VerifiedForSubmission=false`; it will normally cause fail-closed preflight until actual official effective dates/editions are reviewed and introduced. Tests may use synthetic *verified* fixtures but must never be treated as Tax Authority certification.

### 16.3 Limitations and mandatory downstream integration gates

1. **Not wired to trusted EF entities yet.** The current function accepts an immutable-looking `InvoiceDispatchPreflightContext` and `InvoiceWireInput` from a caller. Merely passing GUIDs/hash/approval states does not prove database ownership, issuer identity, physical fiscal memory registration, or actual persisted snapshot hash. Future trusted application service must fetch and lock/verify `StoredInvoice` + `StoredInvoiceRevision` + Phase08 approval + Phase09 memory/key before generating this input. No public API may accept arbitrary caller-supplied preflight context as authorization.
2. **Only preliminary line cross-check.** Item count/IDs, no duplicate IDs and the Step06 DTO math checks are implemented, but they do not yet bind each projected official `sstid`, source unit, VAT category, price and line amount to each exact immutable Phase06 `InvoiceLineSnapshot`. Required before signing and live dispatch.
3. **Official field completeness is not yet proven.** Phase10 two-pattern matrix still has unresolved Type2 buyer conditions, payment/settlement `setm` and payment elements, contracting validation against a registered contract, reference-chain authority state and special cancellation/return field presence. The Step06 mapper deliberately denies unknown settlement/payment inputs and omits or placeholders some fields. Resolve official mandatory fields prior to any production send, with source-cited golden/negative fixtures; never force through a permissive switch.
4. **No automatic signed state.** The digest and defensive-byte buffer are preparatory only. Phase09 `AuthorizedSigningRequest` requires independent authenticated session, owner-authorized key, same source snapshot hash and current revisions. Signed wire bytes must match this exact hash; Step09 owns this binding.
5. **No fiscal readiness from a green UI preview.** At most present candidate validation findings; never present an accepted state or a working 'send' action until all independent gates and transport persistence are implemented.
6. **No real enforcement of idempotency here.** Step05 proposed durable unique scope/transaction schema; Step11 delivers actual persisted claims/CAS/outbox. No duplicate network send until those mechanisms exist.
7. **Missing official citation verification** remains an explicit release blocker. Prior Phase07 document references V7.9 (Tir 1405) but this Step07 did not re-open original PDF; current amendments and exact effective dates require independent official verification.

**Consequence:** Step07 creates a **reviewable cryptographically pinned candidate**, *not* a regulator-ready invoice sender. Work remains in the next steps; "completed Step07" must not be reported as complete end-to-end authority field validation.

### 16.4 Focused automated unit test cases

`Phase10DispatchPreflightTests` records:
- Synthetic pinned registry+pattern with source-verified effective date can create a frozen candidate while retaining `IsFiscalSubmissionAuthorized=false`.
- Altering a copied wire buffer does not mutate the held candidate; SHA256 verifies the canonical UTF-8 bytes.
- Actual unverified source-date/coverage indicators reject preparation.
- Draft or unapproved invoice, mismatched subject/date and missing memory reject preparation.
- Missing line snapshot and edition mismatch reject preparation.
- Unsupported settlement/payment and changed financial totals reject preparation with safe typed errors.

Unit fixtures are **synthetic local evidence** and are not official compliance proof. No real-key, payer, Tax Authority or PostgreSQL integration.

### 16.5 macOS-only operator test

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10DispatchPreflightTests|FullyQualifiedName~Phase10InvoiceWireMapperTests|FullyQualifiedName~Phase10DispatchContractsTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No Next.js startup, PostgreSQL tunnel or Ubuntu VPS test required at Step07; Ubuntu remains exclusively Step23.

### 16.6 Step07 acceptance

| Acceptance criterion | Status |
| --- | --- |
| Fail-closed candidate preparation and typed safe reasons | Implemented in source; execution pending |
| Frozen scoped identity, revision, rule/mapper versions and exact wire SHA256 | Implemented; tests written |
| Source-date/edition/coverage gates | Implemented as necessary gates, actual official evidence incomplete |
| Source-authenticated EF invoice/line/VAT/registered contract binding | **Not implemented; mandatory before signing/send** |
| Complete legally conditional Type2/contracting/subject/payment field matrix | **Not implemented; tracked blocking integration** |
| DB migrations, live API, production enablement | None |
| macOS test execution | Pending operator |
| Ubuntu | Step23 only |

**Step07 owner decision (2026-10-23):** explicitly approved in chat; no macOS test output provided with that decision, so no independent PASS claim. Approved Step07 establishes only the fail-closed preparatory boundary, not complete fiscal compliance, signing permission or live dispatch. Step08 authorized.

## 17. Step 08 — Scoped Tax Authority authentication reuse (2026-10-23)

**Status:** Implementation and tests committed. Operator macOS build and unit tests **not executed** in GitHub-only workflow. Await explicit owner signoff. No database migration, Tax Authority request, invoice transmission, service deployment or Ubuntu VPS operation performed.

### Scope and source inventory

Phase09 already implements actual scoped certificate-free key-based SELF_TSP authentication in `src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyTaxAuthorityConnection.cs` and its `SigningConnectionRegistration.cs`. Its existing read-only adapter obtains a token using the selected protected key and calls `GET_FISCAL_INFORMATION`; the result is classified based on actual expected structured data, **not** on HTTP 200 alone. The key is sourced via `EfSigningOperationRepository` authorization, `ProtectedKeyEnvelopeStore`, `KeyEnvelopeProtector`, the taxpayer and fiscal memory identity and the selected key version. Legacy authority HTTPS base URL is pinned to `https://tp.tax.gov.ir/req/api/`; no insecure TLS bypass. The current endpoint `src/ArginTaxPayer.Api/Taxpayers/SigningConnectionEndpoints.cs` is deliberately Development/loopback only and is **not** promoted into a production invoice-send endpoint.

Phase09 source `SigningConnectionTestContracts.cs` explicitly distinguishes `Authenticated`, `AuthenticationRejected`, `NotAuthorized`, `KeyNotReady`, `VaultUnavailable`, `EndpointUnavailable`, `TimedOut`, `UnsupportedProfile` and `Indeterminate`, returning safe codes and bounded fiscal information. Authentication confirmation is neither activation/portal registration nor an authorization to transmit invoices.

### Delivered Phase10 integration boundary

- **Added** `src/ArginTaxPayer.Application/Invoices/Phase10AuthorityAuthentication.cs`: `IInvoiceAuthorityAuthentication`, `Phase09InvoiceAuthorityAuthentication`, bounded `InvoiceAuthorityAuthenticationStatus` and redacted `InvoiceAuthorityAuthenticationResult`. Validates nonempty auth and taxpayer/memory GUIDs locally, reuses `ISigningReadOnlyConnectionTest.TestAsync` with original caller token, and maps only safe status/date. Fails closed on unknown statuses. It never returns access token, packet, signature, private key, arbitrary provider response or error body.
- **Changed** `src/ArginTaxPayer.Infrastructure/Signing/Connection/SigningConnectionRegistration.cs`: register `IInvoiceAuthorityAuthentication` as a scoped bridge next to the existing Phase09 read-only service. Existing explicit Phase09 configuration remains the authoritative enablement gate: when disabled, the already registered `UnavailableSigningReadOnlyConnectionTest` returns `UnsupportedProfile`; registration does not turn on any provider operation.
- **Added** `tests/ArginTaxPayer.Tests/Phase10AuthorityAuthenticationTests.cs`: synthetic read-only provider, success separated from send permission, every classified failure, missing credentials/scope, unknown enum, pre-canceled token and no unintended provider calls.

**No new HTTP endpoint or UI control**, no `SendInvoices` call, no token persistence and no source changes to Phase09's protected key-handling/signing code. This integration intentionally avoids a duplicate token flow and preserves the prior certificate-free method.

### Required security and acceptance invariants

1. The application layer must resolve and authorize current owner/customer/partner permission and selected taxpayer+memory from authenticated session. The Phase09 provider independently repeats its own scoped authorization. A browser-selected GUID is never sufficient.
2. An `Authenticated` response proves the configured read-only Phase09 flow succeeded under the criteria of that provider; `IsFiscalSubmissionAuthorized=false` and `IsFiscalInvoiceAccepted=false` remain hard-coded. Final readiness additionally needs Step07 official rules, Step09 signing intent, Step11 durable dedupe and Step10 outbound packet/response contract.
3. No unsupported modern SDK certificate/JWS path is auto-enabled. Where a configured taxpayer's cryptographic profile is unsupported, the status is `UnsupportedProfile` rather than insecure fallback or fabricated success.
4. Runtime failures remain scoped and typed. No token, private-key material, full provider response, trace payload or sensitive header may enter DTO/logs. TLS pinning is to official host through existing Phase09 fixed URL and default trusted TLS validation.
5. Phase09's existing read-only TestAsync is already capable of contacting the real authority when it is explicitly enabled and a valid authenticated caller invokes it. **This Step08 did not invoke it** or gain proof of a current operational session. The user's previously reported connection success is not treated as a newly reproduced test result.
6. Live read-only authentication may be exercised by the operator only with explicit permission and safe environment checks; Phase10 Step22 remains the controlled live **invoice** send gate, not implied by Step08 approval.

### macOS-only verification, no database migration

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10AuthorityAuthenticationTests|FullyQualifiedName~Phase09LegacyAuthorityConnectionTests|FullyQualifiedName~Phase09ConnectionResultPolicyTests|FullyQualifiedName~Phase10DispatchPreflightTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No Next.js startup, PostgreSQL migration, real bearer/session, external call, macOS local process startup or Ubuntu VPS test is required for acceptance of this bounded integration step. **Ubuntu checks remain exclusively Step23**.

### Step08 acceptance and Step09 dependencies

| Acceptance item | Current evidence |
| --- | --- |
| Existing Phase09 identity/custody/read-only adapter reused | Source reviewed and DI registration added |
| Redacted, typed auth bridge with no send permission | Implementation committed |
| Unknown and non-success statuses fail closed | Unit tests committed; macOS execution pending |
| Real authority connection to named taxpayer | Not rerun; no current evidence attached |
| Official live submission wire compatibility | Not proven; Step09/10 gate |
| DB schema/migration, external invoice request | None |
| Ubuntu verification | Deferred to Step23 as owner directed |

**Step08 owner decision (2026-10-23):** owner expressly approved Step08 and authorized Step09. No macOS output accompanied the approval; runtime PASS is not independently claimed. Read-only authentication success is not live invoice transmission, verified official field compliance or authority acceptance.

## 18. Step 09 — Strict signing identity, packet-byte hash binding and fail-closed security guard (2026-10-23)

**Status:** Source code and unit fixtures committed on `phase/10-invoice-submission`; macOS operator build/tests pending, owner signoff pending. No SDK runtime dependency, authority request, schema migration, public endpoint, new signing DI service or Ubuntu test. **This step does not claim the Phase09 signing service is yet capable of cryptographically signing the exact Phase10 packet bytes.** See the mandatory blocking integration requirement below.

### 18.1 Critical discovery: the Phase09 signer signs different source bytes

Inspected `src/ArginTaxPayer.Infrastructure/Signing/InvoiceSnapshot/InvoiceSnapshotSigningService.cs`, `InvoiceSnapshotSigningContracts.cs`, `InvoiceSnapshotSigningRegistration.cs` and `src/ArginTaxPayer.Application/Signing/DigitalSigningContracts.cs`.

The existing `IDigitalSigningService.SignAsync` resolves **a trusted persisted `TrustedInvoiceSnapshot.SnapshotJson`** through `ITrustedInvoiceSnapshotSource`. It verifies SHA256 on that stored JSON, creates a durable signing intent, unwraps the selected protected key, and signs `Encoding.UTF8.GetBytes(snapshot.SnapshotJson)` with `LegacySigningMode.ObjectV030` / `LegacySigningPurpose.Content`. It returns signature metadata and `IsFiscalSubmissionAuthorized=false` without exposing private material.

The Phase10 Step06–07 `InvoiceDispatchPreparedSnapshot` computes a **different digest for the exact projected Tax Authority JSON bytes**. A signature over stored *business* SnapshotJson **cannot simply be attached** to the Phase10 official wire packet. Matching GUIDs, an invoice revision or a response `Signed` alone are not proof that the correct outbound bytes were signed. This is a blocking issue, not something to hide via a successful unit test.

### 18.2 Delivered Step09 code

- **Added:** `src/ArginTaxPayer.Application/Invoices/Phase10WireSignatureBinding.cs` with `InvoiceWireSigningIntent`, `InvoiceWireSignatureBindingStatus`, `InvoiceWireSignatureBindingResult` and `Phase10WireSignatureBinding.Check`.
- **Added:** `tests/ArginTaxPayer.Tests/Phase10WireSignatureBindingTests.cs`.
- This read-only binding checks: matching owner/customer/taxpayer/memory/invoice/revision; server-pinned expected key ID/revision; signing profile agreement; exact 64-hex wire hash recomputed from a **copy** of canonical UTF-8 bytes; equality with both `AuthorizedSigningRequest.SnapshotSha256` and `SigningResult.SnapshotSha256`; `DigitalSigningStatus.Signed`; and safe, bounded Base64 signature-envelope format.
- All failure codes are fixed safe keys: scope, key, profile, hash, malformed signature or signing failure. No raw provider response or signature contents are returned.
- **Crucial:** A fully matching metadata result is `MetadataMatchedButWireUnverified`. `IsWireSignatureVerified=false`, `IsFiscalSubmissionAuthorized=false` unconditionally. It **cannot be used** as the actual dispatcher admission/signed-state authorization.
- The test for positive-looking metadata deliberately uses a syntactically valid but **cryptographically meaningless** test signature and asserts it is **not** verified. Negative tests cover wrong business-snapshot hash, cross-tenant/revision, key/profile mismatch, invalid envelope and rejected signing status.

### 18.3 Mandatory signing integration before live invoices

Before `InvoiceDispatchState.Signed` may be entered or a signed packet may be sent, the implementation must establish an **authorized, trusted-source, byte-exact wire-signing path**, with all of:

1. Server-side load of the finalized approved invoice revision and official pattern payload; the server—not browser-supplied input—selects taxpayer, active fiscal memory, key version and signing profile.
2. Freeze the exact canonical bytes that the official SDK/protocol signs (including required envelope, packet headers, nonce, compression/encryption order, if relevant), and persist the unambiguous approved intent/packet identity before private-key access. Do not assume an arbitrary JSON body signature is equivalent to the SDK wire signature.
3. Reuse Phase09 protected custody and signer **only after** profile/algorithm reconciliation: the current `ObjectV030` signing mode may canonicalize input differently than a raw UTF-8 SHA256 signature. Compare algorithm and official authority fixtures, including PacketType and request header signatures. No unapproved modern certificate/JWS substitution.
4. Cryptographically verify the produced signature against the **exact documented signing input** using the pinned public SPKI and expected algorithm, compare key identity and payload hashes in constant time, and preserve typed signed-evidence state. An invalid signature, stale revision or failed audit must deny.
5. Integrate the signed-evidence boundary with a DB-backed dispatched operation, outbox CAS and safe provider response handling in later Steps10–11. **Never convert metadata-only agreement into signed/authorized status.**
6. No signer operation may reveal key material or offer generic arbitrary `Sign(bytes)` through public HTTP.

**Step09 acceptance is partial by design:** it delivers the safe matching/denial contract and identifies the existing signer byte-domain mismatch. The byte-exact crypto integration remains an explicit **blocking dependency** for Steps10 and 22; record it as unresolved and do not say live-signing is complete just because the metadata tests pass. Owner may approve the protective contract but that does not waive the requirement.

### 18.4 macOS-only operator acceptance

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore

dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10WireSignatureBindingTests|FullyQualifiedName~Phase09InvoiceSnapshotSigningTests|FullyQualifiedName~Phase09OfflineSigningCryptoTests|FullyQualifiedName~Phase10DispatchPreflightTests"

dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No migration, real private-key operation, database tunnel, Tax Authority connection or Ubuntu test is required. Ubuntu remains **Step23 only**. Record exact commit SHA, test outcomes and any regression failures; no unverified PASS claims.

### 18.5 Acceptance checklist

| Criterion | Result |
| --- | --- |
| Examine Phase09 actual signed-byte source, immutable key identity and audit | Reviewed |
| Phase10 exact prepared wire SHA256 and signer metadata matching | Added, tests written |
| Reject mismatched owner/taxpayer/memory/revision/key/profile/hash and malformed envelope | Added, tests written |
| Confirm crypto signature over exact outbound packet canonical bytes | **NOT IMPLEMENTED; hard send blocker** |
| Persist durable sign intent and enforce full scope in live DB | Existing Phase09 only; Phase10 integration still pending |
| Build/tests on macOS | Await operator |
| Migration or live authority send | None |
| Ubuntu | Step23 only |

**Step09 owner decision (2026-10-23):** explicitly approved in chat. The exact-byte cryptographic signing blocker remains OPEN and was not waived; no macOS operator output accompanied approval. Step10 authorized for simulator and fail-closed transport only.

## 19. Step 10 — Submission transport contracts and safe initial receipts (2026-10-23)

**Status:** Source code and unit test scenarios committed. macOS execution, owner acceptance and real network integration remain **pending**. No authority request, actual submit endpoint, schema migration, deployment, Ubuntu VPS check or production packet signature has been performed.

### Source inventory and strict phase boundary

- Modern archived SDK version 2.0.32 `Clients/LowLevelTaxApi.cs` exposes `SendInvoicesAsync(List<PacketDto>)` and archived `BatchResponseModel` includes timestamp and returned result packets; **SDK method presence is not proof of the current legally applicable wire protocol, payload correctness or authority acceptance**.
- Previous `Phase10WireSignatureBinding.Check` deliberately reports `MetadataMatchedButWireUnverified` and `IsWireSignatureVerified=false`. Its unresolved exact-wire-cryptographic-signature path is an explicit **hard gate** for live transport.
- Step05 stores are design/pure contracts; the real atomic EF/PostgreSQL outbox/attempt/receipt tables and CAS admission have **not** yet been implemented or migrated. Network calls may not be made until durable reservation exists and passing a signing proof is possible. No fake "sent" event can bypass that gate.
- Therefore Step10 implements transport/receipt **application contracts + simulated results**, without a live HTTP adapter. The actual production `SendInvoices` adapter must be completed only alongside the verified wire signature, safe persisted outbox and operator-authorized read-only/live protocol evidence. User-authorized first live invoice remains Step22.

### Files committed

- `src/ArginTaxPayer.Application/Invoices/Phase10SubmissionTransport.cs`
  - `InvoiceSubmissionDispatchDescriptor`: immutable scoped metadata (operation/customer/taxpayer/fiscal memory/invoice revision/wire hash/idempotency/known endpoint code), not raw unsigned bytes or an authorization token.
  - `IInvoiceSubmissionTransport` abstraction.
  - `DisabledInvoiceSubmissionTransport`: default fail-closed endpoint-free implementation, returning `Indeterminate` with `invoice.dispatch.transport_disabled`. It does **not** contact authority.
  - `SimulatedInvoiceSubmissionTransport`: explicitly constructed test-double with injected deterministic outcome; **not** registered as a production service.
  - `InvoiceSubmissionTransportOutcome`, `InvoiceSubmissionReceipt`, `InvoiceSubmissionReceiptStatus` with `Acknowledged`, `ExplicitlyRejected`, `Indeterminate`.
  - `InvoiceSubmissionReceiptPolicy.Parse`: validates scope, operation correlation, UTC timestamp, allowed endpoint code, strict safe message code, bounded reference. It never returns raw provider data or a fiscal acceptance assertion. `SuggestedEvent` maps a typed outcome to the Step04 dispatch state machine, **but applies no persisted transition** and authorizes no retry.
- `tests/ArginTaxPayer.Tests/Phase10SubmissionTransportTests.cs`:
  - Production-disabled path makes no external request and grants no receipt.
  - Simulated initial acknowledgement has reference but `IsFiscalAccepted=false`.
  - Ambiguous provider result maps to `OutcomeUnknown`, and blind retry from `Indeterminate` remains forbidden.
  - Explicit definitive rejection maps to `DefinitiveFailure`.
  - Wrong operation ID, omitted receipt reference, illegal reference on ambiguous outcome and unsafe provider values are rejected.
  - Simulated cancellation does not invoke callback.
- `docs/phases/phase-10.md`: this evidence and acceptance block. **No new dependency/package.**

### Step10 receipt classification and safe state handoff

| Transport classification | Evidence required later from official adapter | Suggested dispatch event | Fiscal acceptance |
| --- | --- | --- | --- |
| `Acknowledged` | Verified operation correlation, correct accepted provider packet type and bounded receipt/reference | `ReceiptReceived` → `Acknowledged` | **NOT established** |
| `ExplicitlyRejected` | Unambiguous authoritative terminal response for same operation; no network ambiguity | `DefinitiveFailure` → `FailedTerminal` | **NOT established** |
| `Indeterminate` | Timeout, disconnect, lost response, unsupported/malformed result or disabled adapter | `OutcomeUnknown` → `Indeterminate` | **Unknown; reconcile, no resend** |

**Production adapter obligations not yet implemented:** strictly pinned Tax Authority host and TLS, timeouts, approved endpoint and protocol version, exact signed/encrypted packet object, provider request UID/ref matching, response-size limits, official error packet mapping, replay-safe retry with evidence, authorization, persistence of the initial attempt and receipt, no sensitive logging. A generic HTTP 200 or the presence of a response ID does not prove fiscal acceptance.

**Limit of the initial parser:** `InvoiceSubmissionReceiptPolicy.Parse` consumes a **trusted classified outcome**; it does not parse arbitrary raw official JSON, verify provider cryptographic proof or independently validate a final tax receipt. The source path that constructs an `Acknowledged` outcome must be reviewed and tightly trusted in later steps. Never surface the simulation control in production.

### Integration gates after Step10

1. Step09 pending: implement authentic source-bound cryptographic signature verification over **exact officially required packet bytes** rather than the persisted business JSON; no simulated/dummy Base64 is permitted in production.
2. Step05/11 pending: apply owner-reviewed additive EF/PostgreSQL dispatch-operation/outbox/attempt/receipt migration to **test database only** after explicit approval, with durable CAS and uniqueness.
3. Step10/13 pending: protocol-specific response parser must validate batch correlation, reference/UID semantics and terminal-vs-indeterminate errors against the selected official endpoint.
4. Step07 pending: source-verified full field matrix, VAT and contracting requirements, current effective legal dates and owner/memory/line snapshot binding must pass before real signing.
5. Step20–22: run security/regression and conduct one **separately owner-authorized** legitimate live send on macOS; Step23 first Ubuntu full acceptance.

### macOS-only acceptance commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore

dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10SubmissionTransportTests|FullyQualifiedName~Phase10WireSignatureBindingTests|FullyQualifiedName~Phase10DispatchContractsTests"

dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No Next.js service, database tunnel, SQL migration, real authority bearer or Ubuntu VPS test is required. **Ubuntu remains Step23 only.**

**Acceptance claim:** Step10 delivers safe, testable mock transport and initial-receipt processing contracts; it does **not** deliver a functioning real Tax Authority network sender. Completion of this step shall not be presented as an end-to-end successful dispatch. **Step10 owner decision:** explicitly approved in chat 2026-10-23 and Step11 authorized. Runtime operator outputs were not attached; no independently verified PASS asserted.

## 20. Step 11 — Actual test-db outbox schema and atomic idempotency admission (2026-10-23)

**Status:** Code, additive migration source, protected transactional repository and static regression tests committed. **Migration has NOT been applied** to any database. PostgreSQL integration/concurrency and macOS runtime results must be supplied by operator. Owner signoff pending. No actual invoices, HTTP submit, production database, Ubuntu login/verification, merge or release.

### Source audit / preservation strategy

Previously installed PostgreSQL structures remain unchanged: `invoice_documents` and `invoice_document_revisions`, internal `invoice_operations`, `outbox_messages` and `idempotency_reservations`. The new Phase10 dispatch namespace is distinct. Existing tax invoice business revision records retain their historical immutability trigger. Migration is guarded by `current_database() = 'argintaxpayer_test'` and fails on other DB names; **no automatic migration** is run at API startup.

### Implemented migration (not applied)

**New:** `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261023190000_AddPhase10InvoiceDispatchOutbox.cs`.

- Adds `invoice_dispatch_operations`, `invoice_dispatch_attempts`, `invoice_dispatch_receipts`; does not drop/alter preexisting invoice/user tables.
- Enforces unique `(customer_user_id,taxpayer_id,idempotency_key)` and `(customer_user_id,taxpayer_id,fiscal_memory_id,invoice_id,invoice_revision)`. Distinct idempotency keys cannot reserve the same invoice revision/memory twice.
- FK to scoped `invoice_documents`, existing `identity_users`, `taxpayer_profiles` and `fiscal_memories`; the exact taxpayer→fiscal memory association must be reverified transactionally by the caller (the simple fiscal-memory FK alone cannot prove it).
- State and positive-revision constraints, SHA256 syntax checks and constrained reference fields. Append-only database triggers prevent `UPDATE/DELETE` on attempt and receipt evidence; `Down` throws to block accidental destructive rollback.
- Stores only safe metadata: hash, rule/mapper profile ID, operation/attempt/receipt and bounded reference. No raw private key, signed packet, bearer, authority response body, encryption key or customer financial JSON is persisted in the dispatch store.
- Schema intentionally introduced using raw SQL migration and accessed through explicit transactional commands, without tracking the new tables as EF entities or modifying the large EF ModelSnapshot. This is deliberate; **future switch to tracked entities requires correctly regenerated snapshot and verified model parity**. No migration was generated/applied automatically.

### Implemented admission repository (not connected to production submission)

**New:** `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceDispatchAdmissionRepository.cs`.

- Explicit database-name guard `argintaxpayer_test` at transaction start.
- Short `READ COMMITTED` transaction; `INSERT...SELECT` requires the existing scoped invoice, exact revision and `state=Finalized(3)`. `ON CONFLICT DO NOTHING RETURNING id` enforces database uniqueness and synchronizes competing writers.
- On conflict, `SELECT ... FOR UPDATE` of the same tenant+taxpayer+key: exact matching frozen memory/invoice/revision/hash/rule/mapper/sign profile → `Replayed`, otherwise `Conflict`. A second idempotency key claiming the same invoice revision is conflict, never a fresh send.
- Domain `InvoiceDispatchAdmission` from Step05 validates idempotency key format and normalized hash. The repository requires separately authenticated server-side authorization, current membership, active matching fiscal memory, signing key, approved review and full legal-preflight; **none** can be inferred from its metadata-only request. Do not register as a public endpoint or permit browser-issued raw arguments.
- No external I/O to Tax Authority; no signing, token, invoice transport, outbox worker or retry scheduler is wired. `Created` means reserved **only**, never sent.
- Outcomes: `Created`, `Replayed`, `Conflict`. DB exceptions propagate rather than being misrepresented as success.
- This is an admission capability, not yet the full provider attempt/CAS/reconciliation persistence workflow. Leases, restarts, attempt append, safe receipt ingestion and multi-worker recovery are Steps12–14.

**Known concurrent-scope gaps before actual send:** protect reference lineage, case-sensitive idempotency key namespace, correct fiscal-memory-to-taxpayer relationship, Phase08 review approval, rule evidence and lease owner with signed immutable packet. Concurrency behavior must be tested on a real isolated PostgreSQL database with two independent connections and same/different keys. Current unit tests do NOT prove this.

### Unit tests / source evidence

**New:** `tests/ArginTaxPayer.Tests/Phase10DispatchDatabaseContractsTests.cs` verifies the migration's test-DB guard, SQL additive tables, uniqueness indexes, append-only evidence triggers, non-destructive `Down` and pure replay/conflict domain behavior. This is **source-level verification**, not an applied PostgreSQL migration or transaction race test.

### Sandbox discovery (historical vs current)

An announcement attributed to Iran's Tax Authority and widely republished circa 1402 identifies `sandboxrc.tax.gov.ir` as an experimental tax workspace for registering a **separate sandbox fiscal memory ID** and sending test invoices; experimental data may be periodically deleted. References:
- `https://itanalyze.com/نسخه-جدید-سامانه-مودیان-بروزرسانی-شد/`
- `https://www.karandishpooya.ir/index.php/world-news/1205-اطلاعیه‌ی-سازمان-امور-مالیاتی-کشور؛-نسخه‌ی-جدید-سامانه‌ی-مودیان-بروزرسانی-شد`

**2026 status not verified:** an attempted external read of the sandbox host was unavailable via browser lookup; do not infer it was retired or operational, and do not invent a live API path. Add an explicit `Mock | Sandbox | Production` environment separation in later connector configuration. No sharing private keys, fiscal memory IDs, taxpayer credentials or authority-endpoint routing across environments without review. Only after an authorized sandbox account, authoritative current base URL/API edition and safe TLS/certificate behavior are verified should test sends be enabled. Sandbox use is a safer alternative to a first production test where access is proven, but is **not itself** production end-to-end acceptance.

### macOS source/build tests (no DB writes)

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~Phase10DispatchDatabaseContractsTests|FullyQualifiedName~Phase10DispatchPersistenceContractsTests|FullyQualifiedName~Phase10DispatchContractsTests|FullyQualifiedName~Phase10SubmissionTransportTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

### Operator-controlled PostgreSQL migration workflow (DO NOT RUN UNTIL APPROVED)

**No SSH login to Ubuntu needed.** Mac-side `autossh` tunnel from established project standard connects to VPS PostgreSQL on `127.0.0.1:15432`. The first three steps are **read-only**; do not paste passwords or connection strings into chat.

1. Start established AutoSSH tunnel in a separate Terminal (while VPN is not required if direct SSH works):
```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
 -o ServerAliveInterval=15 -o ServerAliveCountMax=3 \
 -o ConnectTimeout=10 -o ExitOnForwardFailure=yes \
 -L 127.0.0.1:15432:127.0.0.1:5432 \
 argindev@85.133.244.156
```
2. Another Terminal, set test-only connection environment **interactively and privately**. Prefer `read -s` or an existing private env file; example:
```bash
cd ~/projects/ArginTaxPayer
export ConnectionStrings__ArginTaxPayerDb='Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=<READ_FROM_YOUR_PRIVATE_ENV>'
```
Replace placeholder locally; never commit. Connection is accepted **only** when `Database=argintaxpayer_test`. No blank/default/production name.
3. Read-only DB identity/history check before *any* migration:
```bash
psql "host=127.0.0.1 port=15432 dbname=argintaxpayer_test user=argintaxpayer sslmode=prefer" -X -v ON_ERROR_STOP=1 -c "SELECT current_database(),current_user,inet_server_addr(),inet_server_port();"
psql "host=127.0.0.1 port=15432 dbname=argintaxpayer_test user=argintaxpayer sslmode=prefer" -X -v ON_ERROR_STOP=1 -c 'SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY migration_id DESC LIMIT 8;'
```
4. After **owner separate approval of generated SQL and safe backup/snapshot**, prepare an offline SQL script on macOS using the installed version of `dotnet-ef`:
```bash
dotnet ef migrations script 20261022150000_AllowDatabaseSigningCustodyReference 20261023190000_AddPhase10InvoiceDispatchOutbox \
 --project src/ArginTaxPayer.Infrastructure \
 --startup-project src/ArginTaxPayer.Api \
 --context ArginTaxPayerDbContext \
 --output /tmp/phase10-step11-review.sql
```
Do not run the SQL yet. Check `dotnet ef migrations list` and ensure prior Phase09 migration is actually in the target database first. A script starting at the prior migration is only valid if that migration is applied. Inspect the SQL for correct database name, table names, constraints, duplicate operations, and any unwanted `DROP/ALTER` on existing tables.
5. For **approved** isolated test DB only, apply the reviewed migration using `dotnet ef database update 20261023190000_AddPhase10InvoiceDispatchOutbox` with same project/startup/context and with verified environment above. Take DB backup first. Verify migration history and `to_regclass('public.invoice_dispatch_operations')`, `to_regclass('public.invoice_dispatch_attempts')`, `to_regclass('public.invoice_dispatch_receipts')`.
6. If unexpected schema mismatches, stop. **Never** use `database drop`, `migrations remove`, `database update 0`, blanket `DROP`, or a destructive rollback. `Down` deliberately prohibits dropping dispatch evidence.

**Important:** At present these are operator instructions for later approval, not a request to execute this migration now. Postgres build/integration tests and migration acceptance are pending. Additional macOS test-DB scripts will be given after operator confirms actual migration history. Ubuntu VPS code acceptance remains Step23; remote database access through the established local tunnel is separate from Ubuntu server OS tests.

### Step11 acceptance and next gate

| Gate | Status |
| --- | --- |
| Additive guarded migration source | Committed, not executed |
| SQL unique keys and scope/FK constraints | Written, not verified live |
| Transactional insert and replay/conflict | Implemented, no live DB execution |
| Immutable receipts/attempts | DB trigger definitions written, not applied |
| macOS .NET build and tests | Await operator |
| Postgres DB identity/backup/SQL review | Await owner approval and operator |
| Real two-client concurrency/idempotency race | Not executed; blocker for live sends |
| Live signer, actual transport, legal field readiness | Existing hard blockers |
| Ubuntu | Step23 only |

**Owner acceptance of Step11** confirms source and reviewed test-only migration plan; it does **not** mean a migration was applied or that live sending is safe. Step12 may proceed with these explicit DB/execution blockers carried forward, but no provider packet may be sent while they remain.

### Step11 follow-up — canonical repeatable macOS migration runbook (2026-10-23)

The owner provided read-only `psql` evidence:
- `SELECT current_database(),current_user` returned **`argintaxpayer_test`**, **`argintaxpayer`** through the macOS development tunnel.
- `"__EFMigrationsHistory"` is case-sensitive: **`"MigrationId"`**, not `migration_id`.
- Latest installed entry: `20261022150000_AllowDatabaseSigningCustodyReference`; Step11 `20261023190000_AddPhase10InvoiceDispatchOutbox` not yet installed.
- This is **not** evidence of any database schema change or migration execution.

**Persistent standard:** `docs/dev/database-migration-runbook.md`, reusable `scripts/phase10-step11-migration.zsh`. On Mac first run `zsh scripts/dev-status.zsh`; if stopped, existing `zsh scripts/dev-up.zsh` brings up AutoSSH/API/web and runs test-DB readiness. Do not create duplicate manual AutoSSH sessions. Because script-executed exports do not propagate to the parent shell, run **`set -a; source .env.dev.local; set +a`** in the operator's own terminal before subsequent `dotnet ef` commands. `.env.dev.local` is gitignored; never commit secrets. The script verifies port 15432, DB name and migration history, takes a backup, generates offline SQL and requires an explicit `APPLY` prompt after review. No irreversible migration action was executed by this source commit.

**Pending operator steps:** pull latest branch on macOS, build/targeted unit tests, inspect generated review SQL, get separate approval to apply, then validate live PostgreSQL constraints/idempotency with parallel test transactions. Ubuntu VPS operating-system acceptance remains Phase10 Step23 only.

## 21. Step 12 — Durable, fail-closed retry and network-fault safety (2026-10-23)

**Owner decision:** Step11 explicitly approved in chat after operator stated DB table setup was successful. Detailed migration history, PG concurrency race, build logs and count of source-level tests were not attached here; do **not** claim independently witnessed DB race PASS. Migration is believed applied to isolated `argintaxpayer_test` per operator but not verified in this session. No schema changes are required in Step12.

### Delivered source

- **Added** `src/ArginTaxPayer.Application/Invoices/Phase10RetrySafetyPolicy.cs`: explicit `InvoiceTransportFailureKind` for provably pre-network vs timeout/connection loss/unknown result; `InvoiceRetryDecision`, `InvoiceRetryDisposition` and `Phase10RetrySafetyPolicy.Classify`. The policy never schedules an automatic send (`MayAutomaticallyResend=false`), returns only safe message keys, and prevents incorrect lifecycle transitions.
- **Added** `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceDispatchFaultRepository.cs`: a PostgreSQL `UPDATE...WHERE operation_id AND customer_user_id AND taxpayer_id AND state AND state_revision RETURNING state_revision` CAS operation in a short `READ COMMITTED` transaction. Writes are limited to `argintaxpayer_test`; no signing, packet transport, receipt modification, retry scheduler, or HTTP. Both wrong-scope and stale worker revisions fail without advancing state.
- **Added** `tests/ArginTaxPayer.Tests/Phase10RetrySafetyTests.cs`: pre-network failure, timeout, connection reset, 429/503 uncertainty, malformed provider response, interrupted worker, inconsistent Reserved timeout, terminal/unknown states, invalid event/revision, no automatic resend.
- No EF migration, new table, API endpoint, worker activation or Ubuntu operation. The canonical development DB procedure remains `docs/dev/database-migration-runbook.md`. Use `zsh scripts/dev-status.zsh`, then existing `zsh scripts/dev-up.zsh` only if needed. For DB commands run `set -a; source .env.dev.local; set +a` in the same Mac terminal. Never instruct a parallel manual AutoSSH process.

### Fault matrix

| Durable current state | Proven failure evidence | Local policy proposal | Permitted follow-up |
| --- | --- | --- | --- |
| `Reserved` | Failure verifiably before any attempt to invoke network client | `FailBeforeSend` → `RetryEligible` | New network attempt **still requires** new authorized worker claim and signed packet proof; no automatic retry |
| `Sending` | Timeout, connection reset, 429, 5xx, malformed or missing response, process interruption | `OutcomeUnknown` → `Indeterminate` | **Reconciliation only**. No resend until official evidence proves no receipt, handled later |
| `Reserved` | Local state says no send yet, but worker reports timeout/possible network attempt | No transition; conflict/investigation | Investigate durable evidence; do not misclassify as definitely unsent |
| `Indeterminate` | Any local fault, timeout or expired lease | No new send or state change | Authority reconciliation Step14 |
| `Acknowledged`, `FailedTerminal` | Any | No retry | Terminal handling, not automatic re-delivery |
| `Prepared`, `Signed`, `RetryEligible` | General fault without independently proven cause | No transition | Follow scoped admission/workflow and official evidence |

**Security principle:** an HTTP status alone (including 408, 429 and 503), elapsed timeout, expired lease, application crash or a locally issued `CancellationToken` never proves non-delivery. The policy assumes the caller truthfully classified `BeforeNetworkAttempt`; this condition MUST be derived from a trusted worker execution boundary with durable evidence, not customer text. A reserved operation must be persisted as `Sending` before any authority-side network I/O. The Step12 repository is an internal method for authorized workers only. It does **not independently authenticate bearer/user/tenant permissions** and must never be exposed as an API accepting caller GUIDs.

### Known blockers and follow-up

1. **No live transport:** `DisabledInvoiceSubmissionTransport` remains; Step09 exact-byte signing/crypto verification still unimplemented. Never enable actual send based on these tests.
2. **No durable claim/attempt lifecycle yet:** Step11 DB has schema, but no fully implemented atomic `Signed→Reserved→Sending` claim with attempt insertion before network. The CAS fault repository handles only two transitions; full worker, attempt timestamps, append-only evidence and lease ownership must be integrated and race-tested before any send.
3. **No online reconciliation:** `Indeterminate` is sticky until Step14 obtains authoritative evidence. Do not implement retry using a grace period alone.
4. **No production database approval:** scoped guard intentionally restricts writes to `argintaxpayer_test`; production migration planning requires distinct security review.
5. **Current official sandbox status is not verified:** `sandboxrc.tax.gov.ir` historically advertised. Keep Mock/Sandbox/Production separation and avoid test invoices on Production.

### macOS acceptance commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore

dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~Phase10RetrySafetyTests|FullyQualifiedName~Phase10DispatchContractsTests|FullyQualifiedName~Phase10SubmissionTransportTests|FullyQualifiedName~Phase10DispatchDatabaseContractsTests"

dotnet test ArginTaxPayer.sln --no-build
git status --short
```

Optional **read-only** PostgreSQL inspection (no migration needed for Step12) after existing Mac helper is running:

```zsh
zsh scripts/dev-status.zsh
set -a
source .env.dev.local
set +a
# Use the existing test-only psql connection settings; never print the credential.
```

**Step12 acceptance scope:** pure fail-closed fault classification plus narrow scoped versioned state updates committed to source. Unit tests require operator execution; real PostgreSQL concurrent CAS race and true worker-before-network proof remain mandatory end-to-end gates. An accepted Step12 is NOT authorization for real invoice transmission. Step13 starts only after explicit owner acceptance.

## 22. Step 13 — Strict initial acknowledgement vs uncertain authority responses (2026-10-23)

**Owner acceptance:** Step12 explicitly approved in chat on 2026-10-23; no raw operator macOS test output accompanied approval. Step13 source is committed but not executed on macOS; independent build/test acceptance pending. No new DB migration, no HTTP request, no live Tax Authority invoice sender and no Ubuntu test.

### SDK response evidence and implementation

Inspected archived modern TaxCollectData SDK 2.0.32:
- `TaxCollectData.Library/Models/BatchResponseModel.cs` contains `Timestamp` and `Result: List<ResponsePacketModel>`.
- `ResponsePacketModel.cs` contains `Uid`, `PacketType`, `ReferenceNumber`, `Data`.
- Mirrored application DTOs `BatchResponseDto.cs` and `ResponsePacketDto.cs` expose the same fields.

Added:
- `src/ArginTaxPayer.Application/Invoices/Phase10AuthorityBatchResponseParser.cs`: bounded JSON document (262144 chars, max depth 16), exactly one response packet; case-insensitive duplicate property rejection; strict expected packet UID and packet type; limited safe reference characters and maximum length; reject root/packet provider error arrays, missing/null/extra batch entries and unrecognized output. A valid *initial* reference maps only to `InitialReceipt`; all unknown errors and malformed responses become `Indeterminate` with fixed `invoice.dispatch.response_indeterminate`. **No heuristic terminal rejection** from HTTP 4xx/5xx, arbitrary `status` strings or `SUCCESS` text.
- `src/ArginTaxPayer.Application/Invoices/Phase10AuthorityResponseHandoff.cs`: safe conversion to Step10 `InvoiceSubmissionTransportOutcome`. A correlated reference maps to `Acknowledged`; all others remain `Indeterminate`; no raw response, packet, bearer, private key, signature, provider body or unreviewed error message crosses the application boundary.
- `tests/ArginTaxPayer.Tests/Phase10AuthorityBatchResponseParserTests.cs`: valid matching receipt, wrong UID/type, malformed JSON, missing response, zero/duplicate/multiple packets, spoofed error, empty receipt, fake `SUCCESS` and unsafe input checks. Synthetic fixture only.

**Hard separation:** An initial `referenceNumber` is not the authority's final successful validation; final status must be queried with the official inquiry API, matched to the same taxpayer/memory/invoice and confirmed by sufficient official response evidence. `IsFiscalAccepted=false` and `MayAutomaticallyRetry=false` remain unconditional. No operation can transition to actual accepted invoice status based on the Step13 parser. Step14 will introduce conservative reconciliation contract, not automatic resend.

**Remaining production blockers:** (a) full source-verified current official response/error and inquiry schema, (b) packet/UID correlation when transport uses other packet types or multiple invoices, (c) verified canonical wire-byte signature from Step09, (d) durable claim/attempt write before HTTP and source-bound EF invoice from Step07, (e) production vs sandbox endpoint and fiscal memory isolation, (f) evidence-based final authority acceptance after inquiry, (g) live sandbox status. No code registered as live adapter or public endpoint.

### Sandbox/UI plan (within fixed 24 steps)

Historical Tax Authority announcement circa 1402 says `https://sandboxrc.tax.gov.ir` provided **test fiscal-memory IDs and a sandbox work folder** for sending experimental invoices, and states that test data is periodically cleared. This was corroborated by contemporary republished agency announcement:
`https://www.karandishpooya.ir/index.php/world-news/1205-اطلاعیه‌ی-سازمان-امور-مالیاتی-کشور؛-نسخه‌ی-جدید-سامانه‌ی-مودیان-بروزرسانی-شد`;
`https://itanalyze.com/نسخه-جدید-سامانه-مودیان-بروزرسانی-شد/`.
**Current availability and exact SDK/API host/route (2026) unverified.** Do not invent a modern sandbox endpoint URL or equate browser work folder with programmatic query support.

- Step14: safe query/reconciliation/status mapping, synthetic integration scenarios; no unconditional production sends.
- Steps16–19: scoped API/BFF and compact UI with **explicit environment label** `Mock / Sandbox / Production`, distinct fiscal memories and controls; sandbox-only invoice send button is gated on current official endpoint documentation, true signed-byte crypto, verified field completeness, durable outbox + explicit operator authorization.
- Steps20–22: macOS operator-controlled live sandbox acceptance if the official sandbox host, API route, test fiscal memory, test key, separate work folder and availability are actually verified. Check sending via UI and confirm status via authority inquiry plus **manual check of the sandbox test work folder**. An initial reference is not enough; do not claim success merely from HTTP200.
- Step23: Ubuntu same-SHA final integration verification as the separately scheduled step. Step24 final merge/tag/release after owner signoff.
- Never route sandbox invoices to `tp.tax.gov.ir` (production). Environment selection must be server-side allowlisted, not user-supplied URLs. If Sandbox is discontinued/unreachable, block real test send and present a clear non-fiscal Mock mode; do not silently fall back to production.

### macOS acceptance checks

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~Phase10AuthorityBatchResponseParserTests|FullyQualifiedName~Phase10SubmissionTransportTests|FullyQualifiedName~Phase10RetrySafetyTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

No Migration in Step13, no additional AutoSSH startup: use `zsh scripts/dev-status.zsh` then `zsh scripts/dev-up.zsh` only when live dev services are actually needed. PostgreSQL environment runbook is `docs/dev/database-migration-runbook.md`; no Ubuntu OS tests until Step23.

**Step13 acceptance is approval of fail-closed first-response interpretation only** and cannot waive pending verified signing, legal field validation, authority inquiry and safe sandbox/production separation.

## 23. Step 14 — Scoped inquiry reconciliation for uncertain dispatch (2026-10-23)

**Approval:** Step13 expressly accepted by owner in chat; no macOS build logs were supplied with acceptance. Step14 has committed code and synthetic tests, awaiting macOS owner acceptance. No actual authority inquiry, HTTP send, schema migration, Ubuntu deployment or end-to-end receipt test was performed.

### SDK examination

Source references from archived modern TaxCollectData SDK 2.0.32:
- `Dto/InquiryByUidDto.cs` accepts `UidList` and `FiscalId`, optionally start/end times.
- `Dto/InquiryByReferenceNumberDto.cs` accepts `ReferenceNumbers`, optionally a date window.
- `Models/InquiryResultModel.cs` and `src/Application/DTOs/InquiryResultDto.cs` include `ReferenceNumber`, `Uid`, `Status`, `PacketType`, `FiscalId`, `Data`, `Sign`.
- `src/Application/Services/InquiryService.cs` exposes `InquiryByUidAsync` and `InquiryByReferenceIdAsync`.
- Archived SDK RequestStatus enums do not even enumerate the same states: one source lists SUCCESS/FAILED/TIMEOUT and another SUCCESS/FAILED/PENDING. Therefore these string values cannot be given unverified live semantics or final-acceptance authority.

### Implemented Step14 read-only application contract

- **New:** `src/ArginTaxPayer.Application/Invoices/Phase10InquiryReconciliation.cs`.
  - `InvoiceReconciliationIdentity`: current server-resolved operation/owner/taxpayer/memory plus exact expected packet UID, packet type, fiscal identifier and optional known authority reference.
  - `Phase10InquiryReconciliation.Evaluate`: bounded JSON array read, exactly one response, required UID/type/fiscal ID/reference/status fields, strict identity correlation, duplicate-field rejection and fail-closed handling of malformed JSON. No arbitrary status text leaks to UI.
  - `InvoiceReconciliationOutcome`: `CorrelatedPendingReview`, `NoConclusiveResult`, `ConflictingEvidence`. All decisions have `IsFiscalAccepted=false`, `ProvesNotDelivered=false`, `MayAutomaticallyResend=false`, `IsFiscalSubmissionAuthorized=false`.
  - A missing/empty inquiry result remains **inconclusive**, never evidence of non-delivery; a mismatched UID/fiscal ID/reference is a conflict and demands investigation. Returned `SUCCESS`/`FAILED`/`PENDING` status strings are correlation data only pending verified official status semantics.
  - `IInvoiceAuthorityInquiry` is internal application boundary for an authorized, environment-pinned read-only inquiry adapter to be built later. `DisabledInvoiceAuthorityInquiry` always returns a safe unconfigured status and never contacts the network.
- **New:** `tests/ArginTaxPayer.Tests/Phase10InquiryReconciliationTests.cs`: exact synthetic matching, missing/invalid/null response, differing owner memory-derived fiscal ID, different UID/packet type/reference, duplicate JSON properties, multi-result ambiguity, status strings with non-authority meaning, default disabled adapter and cancellation.
- No DI registration for a real inquiry adapter; no API endpoint and no operational reconciliation worker. The Step11 `invoice_dispatch_receipts` table exists as evidence schema but this step does not insert rows or transition durable states based on provider JSON. **This is not a live operational reconciliation implementation.**

### Critical dependencies and closure requirements

1. Current, official specification of inquiry endpoint, signed request, environment-specific URL and response status codes must be verified before real use; the SDK archival model alone is insufficient to assume legal acceptance or non-delivery.
2. Reconciliation operations must be authenticated and scoped server-side to customer, taxpayer, active fiscal memory, exact packet UID and signing key profile; selection by browser GUID alone is forbidden.
3. Need append-only persistence of sanitized inquiry attempt/evidence, source time, correlation and verified final outcome (under Step11 constraints); do not fabricate final statuses.
4. Need durable atomic CAS state update after verified inquiry; `Indeterminate` cannot be made `RetryEligible` merely because a query returns an empty list.
5. Step09 exact-byte crypto signature and Step07 complete official payload rules must be remedied; production transport stays disabled. Do not claim Step14 establishes a working end-to-end provider integration.
6. Environment selection must remain separate: Mock vs verified current Sandbox vs Production, with distinct fiscal-memory/credential scopes. For UI Steps16–19, an explicitly labeled Sandbox control can be surfaced only after trusted endpoint/account verification; visual demo may use clearly labeled Mock.
7. For eventual acceptance, submit one separately authorized test invoice in sandbox (if available), save initial receipt, inquire by matching UID/reference and verify in sandbox work folder where available. None of these real calls occurred in Step14.

### No DB migration and no Ubuntu test in Step14

Use the existing canonical macOS runbook `docs/dev/database-migration-runbook.md`; use `zsh scripts/dev-status.zsh` and only if required `zsh scripts/dev-up.zsh`. No duplicate manual AutoSSH tunnel. If later running DB commands within same Mac terminal: `set -a; source .env.dev.local; set +a`. No schema changes required for this pure contract step.

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10InquiryReconciliationTests|FullyQualifiedName~Phase10AuthorityBatchResponseParserTests|FullyQualifiedName~Phase10RetrySafetyTests|FullyQualifiedName~Phase10DispatchContractsTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

**Step14 owner acceptance** approves only the fail-closed typed inquiry and correlation boundary, not completed online inquiry. Step15 follows only after owner acceptance; official sandbox status and end-to-end UI proof remain open.

## 24. Step 15 — Original, correction, cancellation and sales-return submission lineage (2026-10-24)

**Owner acceptance:** Step14 explicitly approved in chat. No independent macOS build or runtime proof provided with the approval; Step15 source and unit tests are committed, awaiting operator tests and owner signoff. No HTTP transport, authority send, schema migration, Ubuntu login or production configuration change performed.

### Reused Phase07/08 source and constraints

- `InvoiceSubject` / `InvoiceSubjectSelection` from `src/ArginTaxPayer.Domain/Invoices/InvoiceSubjects.cs`: original `ins=1`, correction `2`, cancellation `3`, sales return `4`; non-original requires fiscal `ReferenceTaxId` and may carry `LocalInvoiceId`.
- `OfficialV79InvoiceSubjectPolicy.Allows` implements the known referenced edition's subject/pattern exclusions; this is **not** live legal-validity proof.
- `InvoiceOperationRegistry` already defines per-scope reference-slot conflict policy: correction/return exclusive slot; cancellation separate exclusive slot. It is pure and not a distributed lock. Persisted unique-claim checks must be performed atomically later.
- `Phase10InvoiceWireMapper` projects `irtaxid` but does not itself resolve authority acceptance or legal predecessor history. Step07 and Step09 official wire/signing blockers remain open.

### Step15 delivered code

- **New** `src/ArginTaxPayer.Domain/Invoices/Phase10DispatchLineagePolicy.cs`: `InvoiceDispatchLineageContext`, `InvoiceDispatchPredecessorEvidence`, `InvoiceDispatchReferenceClaim`, `InvoiceDispatchLineageCode`, and `Phase10DispatchLineagePolicy.Check`.
- Original candidate is allowed only without a predecessor; it is still only an **eligible candidate** and `IsFiscalSubmissionAuthorized=false` invariably.
- Correction, cancellation and return require a server-resolved predecessor with exact fiscal reference tax ID, optional exact local predecessor ID, matching customer, taxpayer and fiscal memory; denies self reference, nonexistent predecessor, unverified authority acceptance, canceled/superseded predecessor and mismatched type/pattern.
- A competing **active** scoped claim is rejected for the same exclusive reference slot. Correction and return share one; cancellation uses a separate one. Only authoritative persisted evidence may mark a prior claim inactive, never a timeout, empty inquiry, browser flag or incomplete receipt.
- **New** `tests/ArginTaxPayer.Tests/Phase10DispatchLineagePolicyTests.cs`: original eligibility, three non-original subjects, missing/unaccepted predecessors, cross-tenant/memory/tax identifier, inactive/type mismatch, exclusive slots, self reference and V7.9 unsupported sale-return pattern.
- Does not create a new mutation endpoint, database FK/index, authorization bypass, stored reference update or worker. This is a protective policy to be integrated into authenticated application service in Steps16–20.

### Known bounds / mandatory later verification

1. `InvoiceDispatchPredecessorEvidence.IsAuthorityAcceptanceVerified` is provided by a **trusted server-side, verified and persisted external inquiry outcome**; the Step14 parser intentionally does not grant authority acceptance from arbitrary `SUCCESS` text. Until that proof exists, non-original live sends remain blocked.
2. Legitimate correction/return/cancellation chains can have edition-, subject-, state-, and amount-specific rules. The implementation conservatively denies inactive predecessors and requires matching type/pattern; any more permissive behavior requires cited official evidence and regression coverage before activation. It does not itself prove legally correct revision lineage or eligible returned quantities.
3. Scope/reference claim checking in an in-memory list is not a database concurrency barrier. Apply the existing Phase08 claim uniqueness + CAS under one transaction before any send. Different hosts must not race through this pure policy.
4. External identifiers must originate from the finalized immutable invoice and authority, never directly from browser requests. Only signed, source-verified approved snapshots may enter live wire pipeline once Step07/09 blockers are resolved.
5. No migration required for Step15; changing durable claim indexes would require a separately reviewed migration, backup and explicit approval under `docs/dev/database-migration-runbook.md`.
6. Sandbox UI/send remains deferred to Steps16–22 and subject to verified current endpoint, isolated sandbox keys, full wire signature and authority inquiry; do not silently route sandbox to Production.

### macOS-only acceptance commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~Phase10DispatchLineagePolicyTests|FullyQualifiedName~Phase07InvoiceSubjectsTests|FullyQualifiedName~Phase10DispatchPreflightTests|FullyQualifiedName~Phase10InvoiceWireMapperTests"
dotnet test ArginTaxPayer.sln --no-build
git status --short
```

**No DB migration or tunnel startup is required** by this pure domain Step15. When any future step requires DB, reuse the established `zsh scripts/dev-status.zsh`, `zsh scripts/dev-up.zsh` and shell-local `set -a; source .env.dev.local; set +a` per the canonical runbook. Ubuntu VPS OS acceptance stays Step23.

**Step15 acceptance:** approves the fail-closed predecessor/claim policy and test source, not live authorization, persisted race-proof claims, official subject-chain completeness or successful Sandbox transmission. Step16 begins after explicit owner approval.

## 25. Step 16 — Scoped Application/API/BFF dispatch gateway (2026-10-24)

**Owner decision:** Step15 approved in chat. This step creates a **read-only** invoice dispatch lookup and authenticated disabled-send API/BFF surfaces; it does not claim that live Tax Authority submission/inquiry or actual end-to-end dispatch is operational. Code committed; macOS build and operator acceptance are outstanding. No migration, network send, signing activation, Ubuntu work, release or merge.

### Reuse of project security architecture

- Existing `TaxpayerScopeService.ResolveAsync(bearer)` verifies current authenticated identity, active role and MFA where applicable, and `ScopedQuery(actorId, level1)` filters by CURRENT owner/partner relationships; the taxpayer ID in the path is never authorization evidence.
- Invoice ownership is separately enforced by `InvoiceDocuments.AsNoTracking().AnyAsync` matching invoice ID, taxpayer ID, and server-derived customer user ID. The dispatch store read additionally pins `customer_user_id`, `taxpayer_id`, `invoice_id`, so an unrelated tenant's operation is never returned.
- Same Development/loopback guard as Phase07 invoice endpoints; no new production endpoints or HTTP clients.
- BFF uses cookie-held session bearer, `identityApiOrigin()`, same-local-origin protection on POST, UUID path validation, request no-store and a bounded allowlisted JSON response. The browser never chooses a provider URL, fiscal memory or signed packet.

### Added and changed files

- **New** `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceDispatchReadService.cs`: `Phase10InvoiceDispatchReadService.GetAsync` authenticates server-side, verifies live scope, checks invoice ownership, then uses parameterized read-only SQL on `invoice_dispatch_operations`. Returns only safe state, operation ID, revision and UTC update time; never hashes, provider bodies, tokens or raw invoice JSON. Environment label is currently **Mock**, `SubmissionEnabled=false`, `InquiryEnabled=false` and `FiscalAccepted=false`. An invoice with no dispatch operation shows `NotPrepared`.
- **New** `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`: `GET /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/status` and `POST .../dispatch/send`. Both loopback-only and scoped. `POST` is a **hard disabled** operation which returns HTTP 503 with safe code `invoice_dispatch_not_enabled` only after authentication, taxpayer and invoice scope succeed; it cannot call a real transport.
- **Updated** `src/ArginTaxPayer.Api/Program.cs`: registers the scoped read service and maps endpoints **only** inside `identityDevelopmentEnabled`. No background worker or authority network registration.
- **New** `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/route.ts`: `GET` status and `POST` permanently disabled send proxy, with session validation, origin checks and allowlisted response. No arbitrary API proxy route and no user-supplied upstream URL.
- **New** `apps/web/tests/phase10-step16-dispatch-gate.test.mjs`: source-level regression guards covering route registration, scoped filters, disabled transport and BFF. These are structural tests; manual authenticated browser and real DB tests remain separately necessary.
- **Updated** this single canonical phase document.

### Browser-facing behavior

| Request | Scope | Current result |
| --- | --- | --- |
| `GET /api/invoice-dispatch/{taxpayerId}/{invoiceId}` | Auth cookie, authorized active taxpayer + owned invoice | Mock only, latest stored dispatch state or `NotPrepared`; `submissionEnabled:false`, `inquiryEnabled:false`, `fiscalAccepted:false` |
| `POST /api/invoice-dispatch/{taxpayerId}/{invoiceId}` | Same auth, origin and tenant/invoice checks | HTTP 503 `invoice_dispatch_not_enabled`, NO network send |
| `GET /api/dev/taxpayers/.../dispatch/status` | Development, loopback and bearer authorization | Same safe read-only operation view |
| `POST /api/dev/taxpayers/.../dispatch/send` | Development, loopback and bearer authorization | HTTP 503 only, no actual sending |
| Missing/wrong tenant/invoice | Authenticated scope prevents cross-tenant enumeration | 404 (or 401/403 for invalid access) |

**Known limitations:** only most recent operation view, not yet historical timeline or receipts. No real authority inquiry, receipt verification, packet signing, CAS claim-then-Sending attempt or provider transport. Sandbox credentials, official live endpoint and legitimate fiscal memory are unverified. Remaining prior Step07/09 and Step11–14 operational blockers must be cleared before enabling a sandbox test send. Step17–19 will build a compact status/operation UI with unambiguous Mock/Sandbox labels and a disabled send control unless the server has verified configuration. A mere UI label cannot authorize dispatch.

### Acceptance tests: macOS only

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~Phase10DispatchLineagePolicyTests|FullyQualifiedName~Phase10InquiryReconciliationTests|FullyQualifiedName~Phase10RetrySafetyTests"
dotnet test ArginTaxPayer.sln --no-build
cd apps/web
node --test tests/phase10-step16-dispatch-gate.test.mjs
npm run typecheck
npm run build
cd ../..
git status --short
```

If manually testing authenticated HTTP session, first `zsh scripts/dev-status.zsh`, then `zsh scripts/dev-up.zsh` only if services are stopped. Current shell DB env for any needed DB checks: `set -a; source .env.dev.local; set +a`. No new migration in Step16. Ubuntu OS acceptance remains Step23.

### Acceptance limitations and follow-on

Step16 is a **fail-closed API/BFF integration scaffold**, not complete invoice dispatch. Before claiming an end-to-end sandbox capability, require authenticated per-tenant sender authorization, audit, vetted official sandbox endpoint, correct wire-byte signing and protected key custody, safe persisted outbox worker, bounded authenticated responses, unique packet correlation, official inquiry/final validation and manual sandbox work-folder evidence. A real transport cannot be activated silently by a browser flag. Step17 begins only after explicit owner acceptance.

## 26. Step 17 — Compact RTL invoice dispatch user interface (2026-10-24)

**Owner approval:** Step16 expressly approved by operator in chat, with no attached independent macOS build/browser evidence. Step17 sources committed and operator testing pending. No actual Tax Authority or Sandbox request, no server-side configuration changes, no PostgreSQL migration, no Ubuntu VPS testing, no production send and no tag/release.

### Implemented UI

- **New:** `apps/web/app/invoices/invoice-dispatch-panel.tsx`. A compact Persian RTL component placed **inside the existing invoice details modal**. No redundant full-screen page or multi-page scrolling. Displays server-returned dispatch status, explicit `Mock` label, Sandbox disabled, authority inquiry disabled, and fiscal acceptance false. Refresh control is the only active operation: a scoped `GET /api/invoice-dispatch/{taxpayerId}/{invoiceId}`, `credentials:same-origin`, `cache:no-store`, cancellation on modal unmount and loading/error/empty states.
- Hard-disabled `ارسال صورتحساب` and `استعلام از سازمان` buttons (HTML `disabled`), with explanatory titles. It makes **no POST requests**, signs nothing, and cannot activate Sandbox from client input. The backend Step16 rejects the send route with HTTP 503 regardless.
- Recognizes persisted status names from Step04; `Acknowledged` displayed as **initial receipt**, not legal acceptance; `Indeterminate` requires inquiry and manual investigation; `NotPrepared` does not claim an issued invoice.
- Rejects malformed status payload and anything other than server-confirmed `Mock`, `submissionEnabled:false`, `inquiryEnabled:false`, `fiscalAccepted:false`. No response fields with sensitive credentials or raw financial JSON are rendered.
- Existing editable workspace rows stay on their previous edit flow; dispatch panel appears only for the selected finalized invoice details. No migration.
- **Updated:** `apps/web/app/invoices/workspace.tsx`: imports panel and renders it inside the preexisting details modal; action label reflects status visibility.
- **Updated:** `apps/web/app/globals.css`: compact responsive green-theme layout, small form controls, neutral/teal boundaries, accessible RTL and mobile single-column.
- **New:** `apps/web/tests/phase10-step17-dispatch-ui.test.mjs`: source-level regression for read-only BFF, server-controlled flags, disabled actions and panel integration. These are **structural assertions**, not browser interaction or actual API acceptance.
- **Updated:** canonical `docs/phases/phase-10.md`.

### Strict limits and remaining work

1. This is **Step17 presentation/read** only: no signed fiscal packet transport, mock send trigger, status mutation, inquiry adapter, journal audit, provider acceptance or persistent timeline. Step18 will address richer operation/receipt/timeline details.
2. Server's status from Step16 stays `Mock`, and GET requires current identity session and current taxpayer/invoice ownership. Browser controls do not authorize submission.
3. Real Sandbox UI enablement is contingent on official verified 2026 availability, isolated sandbox memory+key, current endpoint and inquiry documentation, Step09 byte-exact signing proof, Step07 rules, durable attempt pre-network sequence, Step11 real concurrency tests and explicit owner acceptance of the test procedure. There is no fallback to Production.
4. The generic user-facing sender label `ارسال صورتحساب` is intentionally disabled for every user; do not interpret green-themed presentation as live readiness.
5. Validating the optional editable workspace uses its prior wizard; no new direct issuance path is added.

### macOS acceptance: no database migration

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~Phase10DispatchLineagePolicyTests|FullyQualifiedName~Phase10InquiryReconciliationTests"
cd apps/web
node --test tests/phase10-step16-dispatch-gate.test.mjs tests/phase10-step17-dispatch-ui.test.mjs
npm run typecheck
npm run build
cd ../..
git status --short

zsh scripts/dev-status.zsh
# Start only if stopped:
# zsh scripts/dev-up.zsh
```

**Manual operator browser acceptance:** Open `http://127.0.0.1:3000/invoices` while authenticated; select an existing finalized invoice and open its details. Confirm compact RTL panel, `محیط Mock` label, current state loading and refresh, hard-disabled send and inquiry controls, no unexpected HTTP POST requests, proper signed-out/other-owner failures, and no disruption to the legacy invoice wizard. A workspace not yet issued is not a real Tax Authority invoice and should not show legal acceptance.

**Status:** Implementation committed; macOS typecheck/build, tests and authenticated manual UI remain unverified until operator supplies output. Step18 must wait for owner signoff.


## 27. Owner request — independent Sandbox fiscal-memory tab (2026-10-24)

**Context:** Owner requested Sandbox setup beside Production fiscal-memory settings: generate **new independent** keypair, obtain Sandbox fiscal-memory identifier, test Sandbox connection, and eventually select environment-dependent token/invoice transport, with Production continuing to use its existing legitimate token and key. Step17 is committed but not yet owner-approved. This is a new separately tracked *partial* feature, **not** completion of Sandbox credential custody / live connection; no additional step approval was inferred.

### Evidence and safety boundary

- Existing Phase09 `LegacyTaxAuthorityConnection.BaseUrl` is pinned to `https://tp.tax.gov.ir/req/api/`, with signed `GET_TOKEN` and `GET_FISCAL_INFORMATION` calls. Reusing it for Sandbox would send a Sandbox key/ID to Production; **forbidden**.
- The published historical Tax Authority announcement points to `https://sandboxrc.tax.gov.ir` as a **test work-folder portal** and supports requesting a separate test fiscal memory. This does **not establish a current official Sandbox API token host**. No sandbox token host/route should be guessed by string-replacing `tp` with `sandboxrc`.
- Any failover from Sandbox to Production (or vice versa) is prohibited, including authentication failure, timeout, connection loss, missing provider configuration and manual UI environment switch.
- Production DB rows, key versions, import/generation journal, vault records, and token adapter are **unchanged**. Nothing is sent to the authority.

### Code delivered now

- `src/ArginTaxPayer.Domain/Taxpayers/FiscalConnectionEnvironmentPolicy.cs`: explicit `Production` / `Sandbox` enum and 5-tuple credential identity (owner/taxpayer/fiscal-memory/environment/signing key version). Pure matching and immutable no-fallback guard; readiness requires verified sandbox origin, isolated custody and Sandbox registration evidence.
- `apps/web/app/taxpayers/taxpayer-connection-panel.tsx`: top-level environment tabs. The original key-import/generate/connection-test UI renders exclusively under `Production`. `Sandbox` has its own standalone panel; previously configured production memory is never automatically reused for sandbox.
- `apps/web/app/taxpayers/sandbox-fiscal-memory-panel.tsx`: compact, RTL Sandbox onboarding UI. Browser WebCrypto builds independent RSA-2048 SHA-256 keypair and offers initial/again downloads of public SPKI PEM and private PKCS8 PEM (both files **unencrypted**, operator must secure them). The app does **not store or transmit** the private key, does not access production keys and does not claim an active Sandbox session. Linking to the historical Sandbox portal is informational. Fiscal memory ID field is **not persisted** and intentionally marked pending independent backend implementation. Both `Register Sandbox ID` and `Sandbox connection test` are disabled until a distinct server-side vault credential namespace, persistence migration and verified live Sandbox API are implemented.
- `apps/web/app/globals.css`: compact green RTL responsive layout.
- `apps/web/tests/phase10-sandbox-memory-isolation.test.mjs` and `tests/ArginTaxPayer.Tests/FiscalConnectionEnvironmentPolicyTests.cs`: source regression guards for no production endpoint from Sandbox component, tabs and isolation policy.

### Unfinished functionality / next engineering tasks

1. **Persistence and custody**: design a dedicated Sandbox fiscal-memory mapping with environment in unique keys, per-user/per-taxpayer authorization and secure encrypted key envelope storage; migration must be **additive**, reviewed, backed up and applied only on `argintaxpayer_test` after operator approval. Do not reuse the Production `fiscal_memories` record or Production key version ID. Browser-produced private key currently can only be downloaded; a secure, fresh-login-protected import or separate server-side key generation operation is still needed for automated Sandbox token signing.
2. **Current official API**: verify Sandbox token and fiscal information URL/packet profile from authoritative documentation and/or an explicitly authorized test account. The work-folder address alone is not enough. Add server-pinned allowlisted distinct HttpClient and disallow redirects, caller-provided URLs and cookies; no fallback.
3. **Sandbox test endpoint**: signed GET_TOKEN and GET_FISCAL_INFORMATION using **only Sandbox key+ID**; scope authorization, rate limit, no raw provider body, no persisted token or private key in logs. Confirm returned Sandbox fiscal identifier matches selected local record and display safe result.
4. **Invoice Dispatch**: environment is pinned server-side at reservation time as immutable evidence in outbox, with distinct idempotency, receipts, inquiries and signing evidence; no client-controlled switching of in-flight operations. Sandbox/Production transport choice must be a server-side safe enum with all production-sending code behind operator release gates. Current Phase10 transport intentionally remains disabled.
5. **Key format warning**: the browser example produces RSA-2048 PKCS8+SPKI, not a certified official successful pairing. SDK official signing correctness, key registration and actual current Sandbox compatibility still need independent testing.
6. **QA**: validate no production key reads in Sandbox, tab switching state consistency, key generation and download, credentials isolation, 401/403 and cross-tenant, malformed test memory IDs, unknown endpoints, and no authority calls. No live Sandbox test or Ubuntu test executed yet.

### macOS quick checks

```bash
cd ~/projects/ArginTaxPayer
git status --short
git pull --ff-only origin phase/10-invoice-submission
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~FiscalConnectionEnvironmentPolicyTests"
cd apps/web
node --test tests/phase10-sandbox-memory-isolation.test.mjs
npm run typecheck
npm run build
cd ../..
zsh scripts/dev-status.zsh
```

**Operator UI check:** Sign in, open taxpayer fiscal-memory/key tab; confirm Production works exactly as before, Sandbox tab generates completely new files only after consent, Sandbox ID is unpersisted, register/test cannot be clicked, and **no** network request goes to `tp.tax.gov.ir` while using Sandbox. No migration needed for the committed UI/policy; do not interpret this as completion of user's requested end-to-end workflow.


### Sandbox follow-up implementation: metadata registration (2026-10-24)

This is a **partial feature completion** following owner request to complete the Sandbox tab. This is not Step17 owner acceptance and is not a working token test or invoice submission.

**Research:** Archived public announcement (republished by ITAnalyze, 2023) says the Sandbox work-folder portal at `https://sandboxrc.tax.gov.ir` supports issuing a separate test fiscal-memory identifier and trial invoices. Official current (2026) **API** domain/path for `GET_TOKEN` and `GET_FISCAL_INFORMATION` was NOT authoritatively established. The historic portal domain is **not** proof of the API hostname; do not construct an API endpoint by string replacement. Modern/legacy SDKs have differing profiles, so the correct signed packet must be verified independently. No live authority connection attempted.

**New additive migration** `20261024103000_AddSandboxFiscalMemoryMetadata.cs`:
- SQL-only `sandbox_fiscal_memories` table on `argintaxpayer_test` only. Composite unique owner+taxpayer; FK to `identity_users` / `taxpayer_profiles`, revision, validated Sandbox identifier and RSA public SPKI DER + SHA256 fingerprint. No production-memory table changes, deletions or updates; `Down` refuses destructive rollback.
- No secret private key in this table and no token stored in DB. Public key is public; a separate encrypted private-key custody implementation with environment-bound AAD is still required before real signed token test. Public-only registration intentionally does **not** claim key custody complete.

**New server service** `src/ArginTaxPayer.Infrastructure/Persistence/Signing/SandboxFiscalMemoryMetadataService.cs`:
- Re-evaluates live bearer authorization using `TaxpayerScopeService.ResolveAsync` and current `ScopedQuery`, verifies active taxpayer ownership, parameterized SQL, test database name; accepts exactly one Sandbox metadata registration for owner/taxpayer without overwriting existing data.
- Parses supplied public PEM with .NET RSA cryptography, requires RSA-2048 with exponent 65537, derives canonical SPKI DER and SHA256 fingerprint on server. Browser's claim of a fingerprint is never authoritative. No private PEM or production key is accepted here.
- Missing results are safe; no leaking account data or keys.

**New API** `src/ArginTaxPayer.Api/Taxpayers/SandboxFiscalMemoryDevelopmentEndpoints.cs`, registered with DI under Development only:
- `GET /api/dev/taxpayers/{taxpayerId}/sandbox-memory`: read safe independent Sandbox metadata, authenticated/scope-controlled, loopback only.
- `POST /api/dev/taxpayers/{taxpayerId}/sandbox-memory`: register Sandbox ID/public PEM, rate limited, loopback only, rejects Origin and oversize body, never invokes authority.
- `POST .../connection-test`: intentionally HTTP503 `sandbox_authority_api_not_verified` **after** owner/invoice scope check. NO live token call. Not routed through the BFF, so UI cannot mistakenly enable this until credentials and official API are ready.

**New BFF** `apps/web/app/api/sandbox-memory/[taxpayerId]/route.ts`: cookie/bearer only, POST same-local-origin, accepts only `memoryIdentifier`+ `publicKeyPem`, no user-selected environment/URL, restricted response and no secret. UI `sandbox-fiscal-memory-panel.tsx` now reads existing registered metadata and offers `ثبت شناسه و کلید عمومی Sandbox` after generating keys and obtaining ID from portal. It continues to download unencrypted private PKCS8 PEM locally; file security is operator's responsibility. This PEM is not uploaded/stored and token signing is impossible at this stage. Once metadata exists, no rotation/new key is silently allowed (separate review required).

**Tests** `apps/web/tests/phase10-sandbox-metadata.test.mjs`, updated `phase10-sandbox-memory-isolation.test.mjs`, existing `FiscalConnectionEnvironmentPolicyTests.cs`. Structural assertions only, not DB integration tests; operator macOS Build and route/DB checks pending.

**Migration review** `scripts/phase10-sandbox-migration.zsh` sources the shell environment from the operator (must run `set -a; source .env.dev.local; set +a` first), verifies `127.0.0.1:15432/argintaxpayer_test`, migration head Step11 and absence of Sandbox table, takes `pg_dump -Fc` backup, generates target SQL, then prompts `APPLY` only on operator-approved SQL; does not touch Ubuntu OS or production. **Do not type APPLY until SQL reviewed.** Verify index and FK definitions; no destructive SQL.

**Remaining blocker list and acceptance gate**:
1. Independently pinned Sandbox signing-key custody with encryption at rest, AAD including environment `Sandbox`, protected retrieval and never exposing original private key after initial handoff. Existing Production custody cannot be reused implicitly.
2. Official current Sandbox token/inquiry API origin and full request signature profile, with approved test-only memory and networking. Without this, live connection test MUST remain disabled; cannot honestly claim complete request.
3. Safe server-side environment route binding at immutable invoice-outbox reservation time; current Step16 `Mock` status and disabled `POST send` must remain unchanged until official Sandbox testing is authorized and all source-signature blockers solved.
4. Build/tests, database Migration SQL review, backup and operator apply, authenticated route QA (own taxpayer/other owner/expired token/replay), cross-machine race tests, actual private custody/encrypted persistence tests.
5. Never share Sandbox private key in chat, commit, logs or screenshots; the browser-downloaded PEM is unencrypted, handle as secret. Once key changes, manual registration in Sandbox work-folder is needed.
6. Do not call Sandbox test a live result merely because the user typed an ID. No fallback to `tp.tax.gov.ir`.

#### macOS commands (before migration)

```bash
cd ~/projects/ArginTaxPayer
git status --short
git pull --ff-only origin phase/10-invoice-submission
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~FiscalConnectionEnvironmentPolicyTests"
cd apps/web
node --test tests/phase10-sandbox-memory-isolation.test.mjs tests/phase10-sandbox-metadata.test.mjs
npm run typecheck
npm run build
cd ../..
zsh scripts/dev-status.zsh
# If tunnel and services are stopped:
# zsh scripts/dev-up.zsh
set -a
source .env.dev.local
set +a
zsh scripts/phase10-sandbox-migration.zsh
```

**SQL review pause:** press Enter on the first execution, inspect generated `artifacts/sandbox-*.sql`, then rerun and type `APPLY` after review. No operator DB migration or live connection has been performed from this chat.

#### Manual UI check

After verified migration, open the existing taxpayer connection panel. Production should retain original token/test behavior; Sandbox tab should generate independent key files, allow public ID+public key registration, reload persistent metadata, and continue displaying Sandbox connection test disabled. Check browser Network: Sandbox operations must never hit production `GET_TOKEN`; no private PEM must appear in JSON/request bodies.


### Sandbox follow-up: encrypted private custody and offline signing verification (2026-10-24)

**Scope:** Owner requested completion of separate Sandbox memory + key workflow after the partial Sandbox metadata milestone. The following source work is now committed on `phase/10-invoice-submission`. No local builds, web browser, database migrations or live Tax Authority operations are claimed as passed. **This is not official live Sandbox token connectivity.**

- Extended still-pending **additive, test-only** `20261024103000_AddSandboxFiscalMemoryMetadata.cs` *before migration application*: new nullable `private_envelope_json` (ciphertext / wrapped DEK JSON only), immutable `signing_key_version_id` and `origin_operation_id` identifiers. No plaintext private key columns, no writes to `fiscal_memories` or production signing entities. Rollback remains disallowed.
- Extended existing Phase09 `KeyProtectionIdentity` with default `Sandbox=false` so *existing Production AES-GCM AAD remains byte-for-byte unchanged* (`argin:key:v1`); existing Vault production `StoragePath()` also unchanged. The explicit Sandbox profile binds its AAD to `argin:key:sandbox:v1` and uses a separate `keys/sandbox/...` namespace. This is cryptographic isolation, not just UI branding. `tests/ArginTaxPayer.Tests/Phase10SandboxKeyIsolationTests.cs` verifies both representations.
- `SandboxFiscalMemoryMetadataService.ImportPrivateAsync`: accepts private PKCS8 PEM only after separately registered Sandbox ID and public SPKI, validates RSA 2048/65537, compares canonical SPKI in constant time to the existing Sandbox public key, creates one encrypted envelope with `KeyEnvelopeProtector.ProtectAsync`, persists via row-locked revision-CAS `UPDATE...WHERE private_envelope_json IS NULL` inside test-only PostgreSQL transaction, zeroes exported PKCS8 bytes, rejects key replacement/replays and never touches Production keys. Existing configuration must register Phase09 protection provider; if unconfigured, returns a safe unavailable result. The JSON bound private PEM exists transiently in memory while uploading; do not enable verbose request-body logs.
- `SandboxFiscalMemoryMetadataService.SelfTestAsync`: decrypts the Sandbox envelope with Sandbox AAD, signs a randomly generated challenge using the stored RSA private key and verifies with the registered public key; only a boolean-safe result is returned. This is **offline key-health validation**, not authenticated Tax Authority connection, not memory activation and not invoice submission.
- New development-only scoped API routes `POST /api/dev/taxpayers/{taxpayerId}/sandbox-memory/private-key` and `.../key-self-test`, accessible only on loopback with current role/taxpayer auth. They do not construct or invoke any authority URL or token HTTP transport.
- New BFF `apps/web/app/api/sandbox-memory/[taxpayerId]/private-key/route.ts`, `.../key-self-test/route.ts`: cookie session and strict same-origin protection; only an exact small JSON private PEM import is permitted; sanitized success/failure JSON contains **no private key**, no request/provider trace. The existing scoped public metadata BFF now returns a safe `keyCustodyReady` boolean.
- UI Sandbox tab now provides steps: create distinct public/private keypair, register public key into Sandbox portal, enter and persist sandbox fiscal memory identifier + public key, select saved Sandbox-only private PEM and store it encrypted, **test key pair offline**, then the separately labelled official authority connection test stays **disabled**. No client-selected URL or fallback to production. Test status does not assert fiscal acceptance.
- Extended structural test `apps/web/tests/phase10-sandbox-metadata.test.mjs` and prior `phase10-sandbox-memory-isolation.test.mjs`; assertions guard encrypted-key scope, AAD separation, no Production URL in Sandbox endpoints and explicitly disabled live connection.
- **Current blocking dependency**: could not verify a current Iranian Tax Authority *Sandbox API* host/route and signed token contract from official current sources. `sandboxrc.tax.gov.ir` is a historical **work-folder portal**, not sufficient proof of a token API host. The active Production `tp.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN` and `GET_FISCAL_INFORMATION` must **never** be called with Sandbox test credentials. Before enabling live Sandbox, require a pinned official test API origin, per-environment credential and memory validation, host allowlist, bounded/no-redirect TLS HTTP client, signed packet verification, isolated outbox, current tax SDK handshake tests and explicit owner approval. Current Step16 dispatch send and Sandbox online connection remain disabled.

#### Acceptance commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
git rev-parse HEAD
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~FiscalConnectionEnvironmentPolicyTests|FullyQualifiedName~Phase10SandboxKeyIsolationTests"
dotnet test ArginTaxPayer.sln --no-build
cd apps/web
node --test tests/phase10-sandbox-memory-isolation.test.mjs tests/phase10-sandbox-metadata.test.mjs
npm run typecheck
npm run build
cd ../..
zsh scripts/dev-status.zsh
```

**Operator-only migration**, not executed here:
```bash
set -a
source .env.dev.local
set +a
zsh scripts/phase10-sandbox-migration.zsh
```
Script checks exact test DB, dumps backup, generates migration SQL and asks for `APPLY`. First run: press Enter to cancel, share/read SQL and verify non-destructive behavior. Only after explicit SQL review may operator rerun and approve. No migration auto-applied by API/Workers.

**Manual QA after migration:** Open taxpayer connection, Sandbox tab; generate keypair; upload *public* into Sandbox portal and obtain separate Sandbox code, register code+public via UI, upload same generated *private* key to dedicated secret route; confirm `keyCustodyReady`, offline challenge PASS; refresh; ensure old Production memory, key fingerprints and connection test remain identical. Wrong private key must fail 409 without overwriting. Try unauthorized user, different taxpayer and expired session; no secrets returned. Confirm live Sandbox connection test remains disabled and no HTTP to Production is made from Sandbox.

**Open security/performance gates:** (1) enforce fresh reauthentication and audit on private PEM import before promoting past development; (2) bounded streaming/PII-safe logging and CSRF already performed at BFF, but direct API is restricted to local requests without Origin and requires bearer; (3) concurrent PostgreSQL replays and encrypted roundtrip integration tests not yet executed, (4) actual current official Sandbox token endpoint unknown, (5) enterprise/production migrations need their own reviewed process and operational support. No live Sandbox invoice transport permitted based on offline success.


### Sandbox encrypted custody schema drift: verified operator finding and forward-only repair (2026-10-24)

**Operator evidence:** `__EFMigrationsHistory` has `20261024103000_AddSandboxFiscalMemoryMetadata` at head. Physical `sandbox_fiscal_memories` has precisely nine initial metadata columns: `id`, `customer_user_id`, `taxpayer_id`, `memory_identifier`, `public_spki_der`, `public_fingerprint_sha256`, `revision`, `created_at_utc`, `updated_at_utc`. It lacks `private_envelope_json`, `signing_key_version_id`, `origin_operation_id`. Operator's original script correctly stopped at `SCHEMA DRIFT: Missing column: private_envelope_json`. **Cause:** the already-applied historical migration C# was modified after being applied, so EF regards the migration as complete and will not replay its newly added SQL. This is a deployment drift, not a reason to delete data or rewrite history.

**Repository remediation:**
1. Restored `20261024103000_AddSandboxFiscalMemoryMetadata.cs` to exactly the original nine-column shape already applied, preserving migration ID.
2. Added `20261024170000_AddSandboxEncryptedCustodyColumns.cs`: a new migration with an isolated test DB guard, a guard requiring the original Sandbox table and requiring all three custody columns be absent, then adds three columns **without dropping or overwriting existing metadata**. Existing rows receive freshly generated `signing_key_version_id` and `origin_operation_id` UUIDs; those UUID columns become NOT NULL; encrypted envelope remains NULL until explicit one-time encrypted import. Adds length constraint. The same flow works with zero Sandbox rows and for fresh schema built by sequential migrations. No Production memory or signing tables modified. `Down` refuses destructive rollback.
3. Added `scripts/phase10-sandbox-custody-repair.zsh`: refuse any DB other than `127.0.0.1:15432/argintaxpayer_test`, check exact previous migration head/table/columns, stop on partial drift, report row count, take `pg_dump -Fc` and validate archive listing, build .NET, generate exact migration SQL, then require operator review and manually typed `APPLY`. On applying, recheck unchanged head and row count, execute only the new migration, and verify row count/column list/history. Does not change DB unless owner types `APPLY`.
4. Added `apps/web/tests/phase10-sandbox-custody-repair.test.mjs` source regression checks for migration immutability, additive-only SQL and manual operator gate.

**Operator macOS test:**
```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
cd apps/web
node --test tests/phase10-sandbox-custody-repair.test.mjs
cd ../..
zsh scripts/dev-status.zsh
set -a
source .env.dev.local
set +a
zsh scripts/phase10-sandbox-custody-repair.zsh
```
**First run:** Do not type `APPLY`. Press Enter to leave DB unchanged, inspect `artifacts/sandbox-custody-repair-*.sql` and the backup path. After explicit reviewed SQL approval, rerun and type `APPLY`. Do not run the old `scripts/phase10-sandbox-migration.zsh` for this case. Never `DROP TABLE`, `migrations remove`, edit `__EFMigrationsHistory`, or redo a previously applied migration. No Ubuntu OS work before Step23.

**No DB mutation performed by assistant:** All recorded success is source-level GitHub changes, not an operator execution result. Afterwards test application-specific encrypted-key import and offline signing. The live Sandbox authority connection remains blocked pending official API verification.


### Sandbox real authority connection test — isolated, explicit opt-in (2026-10-24)

**User request:** After successfully pairing Sandbox public/private keys and pushing recent UI fixes, provide the real sandbox equivalent of Phase09 signed fiscal-memory test. This implementation deliberately does **not** activate invoice sending.

**Live source review:** The public Tax Authority announcement (republished in 2023) describes the Sandbox **portal** `https://sandboxrc.tax.gov.ir` as separate from Production `https://tp.tax.gov.ir`, allowing a test fiscal ID. It does **not** establish a CURRENT official 2026 Sandbox GET_TOKEN/GET_FISCAL_INFORMATION REST API URL/route. SDK v0.0.30 specifies Production `self-tsp/sync/GET_TOKEN` and `GET_FISCAL_INFORMATION`, but its applicability to Sandbox must be independently confirmed. Therefore **SandboxConnectionTest is OFF by default**. An operator must positively verify the official API endpoint before setting the opt-in switches; do not assume the work-folder URL hosts the API.

**Implementation:**
- `src/ArginTaxPayer.Infrastructure/Signing/Connection/SandboxAuthorityTokenProbe.cs`: read-only signed GET_TOKEN -> bearer-scoped GET_FISCAL_INFORMATION, using **the same SDK 0.0.30 packet signing normalization** as Phase09, but **only** the chosen Sandbox private key and public SPKI. No invoice transport. Serializes the correct SDK packet, enforces 18s timeout, bounded response 256KiB / max depth 16, never returns raw token or response body to client.
- `SigningConnectionRegistration.AddPhase10SandboxConnection` and `Program.cs`: separate named `phase10-sandbox-authority` HttpClient, cookies disabled and redirects disabled. A pinned Origin guard: `SandboxConnectionTest:Enabled=true`, `SandboxConnectionTest:OperatorApprovedExperimentalApi=true`, and `SandboxConnectionTest:ApiBaseUrl` must ALL exist; HTTPS default port 443, **exact host** `sandboxrc.tax.gov.ir`, path `/req/api/`, no query/userinfo/fragment. This is a conservative allowlist, not proof the endpoint really exists. Production `tp.tax.gov.ir` and arbitrary URLs are never permitted, even on timeout/failure. If the actual official Sandbox API uses another hostname or packet profile, DO NOT override guard casually: verify official documentation and update the allowlist in a reviewed commit and tests instead.
- `SandboxFiscalMemoryMetadataService.TestAuthorityAsync`: authorizes current user and selected taxpayer, reads **only** the `sandbox_fiscal_memories` table, locates AAD-bound encrypted PKCS8 with `Sandbox:true`, decrypts transiently in memory, uses signed Sandbox probe and returns status/fiscal info without key/token. No production key selection; no DB mutations.
- `SandboxFiscalMemoryDevelopmentEndpoints.cs`: existing `POST /api/dev/taxpayers/{taxpayerId}/sandbox-memory/connection-test` now invokes the real opt-in adapter, with loopback, no Origin and empty-body guard; returns safe status codes and never claims portal activation.
- `apps/web/app/api/sandbox-memory/[taxpayerId]/connection-test/route.ts`: cookie-backed, same-local-origin, no request body, bounded JSON, allowlisted statuses, no raw upstream response. `apps/web/app/taxpayers/sandbox-fiscal-memory-panel.tsx`: enables `تست اتصال واقعی Sandbox` only when encrypted custody is ready, displays token/fiscal-info result, distinguishes `Authenticated`, rejected, unavailable, unknown, timeout and explicitly unavailable profile. A 200+real token+valid fiscal response means **authentication only**, not legally accepted invoices or active memory.
- `tests/ArginTaxPayer.Tests/Phase10SandboxAuthorityOriginTests.cs` and `apps/web/tests/phase10-sandbox-authority-connection.test.mjs`: strict endpoint rejection and no fallback structural regressions. Existing Sandbox tests adjusted for real opt-in button. Build and authenticated Sandbox tests remain operator-pending.

**Only AFTER current official Sandbox API base URL & wire contract have been verified and approved**, set these values **outside source control**, on the LOCAL development server environment (never in Git):

```text
SandboxConnectionTest__Enabled=true
SandboxConnectionTest__OperatorApprovedExperimentalApi=true
SandboxConnectionTest__ApiBaseUrl=<CURRENT_VERIFIED_OFFICIAL_SANDBOX_API_BASE_URL>
```

The application deliberately rejects every URL except exact `https://sandboxrc.tax.gov.ir/req/api/`. **Do not assume or enter this candidate until official API verification.** If a verified current API URL differs, guard and tests must be amended from authenticated primary documentation. No secrets or fiscal tokens in environment values; key stays in separate Sandbox AES-GCM envelope.

**macOS operator checks**:
```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin --prune
git switch phase/10-invoice-submission
git pull --ff-only origin phase/10-invoice-submission
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore \
 --filter "FullyQualifiedName~Phase10SandboxAuthorityOriginTests|FullyQualifiedName~Phase10SandboxKeyIsolationTests|FullyQualifiedName~FiscalConnectionEnvironmentPolicyTests|FullyQualifiedName~Phase09LegacyAuthorityConnectionTests"
dotnet test ArginTaxPayer.sln --no-build
cd apps/web
node --test tests/phase10-sandbox-authority-connection.test.mjs tests/phase10-sandbox-memory-isolation.test.mjs tests/phase10-sandbox-metadata.test.mjs
npm run typecheck
npm run build
cd ../..
git status --short
```
Check the Authenticated Sandbox UI state only with an explicitly approved isolated test account and verified official API route; otherwise a safe `Unavailable` is the correct behavior. No production token/network calls from Sandbox and no migration required. This implementation has not executed live Sandbox calls from GitHub tools or locally.

**Additional public evidence (2024 developer samples, not current official validation):** A B4X support thread includes a real sample calling `https://sandboxrc.tax.gov.ir/req/api/self-tsp/sync/GET_TOKEN`; a 2024 Java SDK example uses `https://sandboxrc.tax.gov.ir/req/api/self-tsp`; and a 2023 C# discussion uses `https://sandboxrc.tax.gov.ir/req/api/`. Sources: https://www.b4x.com/android/forum/threads/solved-convert-java-code-to-b4x-code.160190/ ; https://www.b4x.com/android/forum/threads/inline-java-erorr.160294/ ; https://csharpforums.net/threads/the-request-was-aborted-could-not-create-ssl-tls-secure-channel.9173/ . This supports an **operator-approved experimental** live test against the historical candidate API but does not prove 2026 availability. Configuration keys intentionally call it `OperatorApprovedExperimentalApi`, not official verification. No dynamic discovery, production fallback or untrusted arbitrary origin permitted.
