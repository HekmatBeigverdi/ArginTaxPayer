# ArginTaxPayer — Phase 10: Tax Authority Invoice Submission (Two-Pattern Delivery)

**Status:** Steps 01–09 owner-approved; Step10 safe transport/receipt contract and simulated unit tests committed; macOS acceptance pending. No live submission, signing activation or DB migration.  
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

**Acceptance claim:** Step10 delivers safe, testable mock transport and initial-receipt processing contracts; it does **not** deliver a functioning real Tax Authority network sender. Completion of this step shall not be presented as an end-to-end successful dispatch. Step11 remains blocked until operator macOS tests and explicit owner signoff.
