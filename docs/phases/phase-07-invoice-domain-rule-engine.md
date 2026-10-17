# ArginTaxPayer — Phase 07: Invoice Domain and Rule Engine

**Status:** Step 01 architecture baseline prepared on 2026-10-17; awaiting explicit owner acceptance. Steps 02–24 not started.
**Branch:** `phase/07-invoice-domain-rule-engine`
**Baseline:** `main` at `041bea0287c3b907a7b8910ef8602f51a8c1654c` (inspected 2026-10-17).
**Roadmap:** `docs/roadmap/18-phases.md`, Phase 07 of 18.
**Policy:** This is the sole evolving Phase07 plan. Exactly 24 numbered steps, fixed titles and scope. Any amendment requires dated owner authorization; no silent renumbering.

## Goal and phase boundary

Build a versioned electronic invoice domain and deterministic rules engine: legally supported type/pattern/subject combinations, corrections, cancellations and sales returns, field validations, tax calculations, reference integrity, and Phase06 goods/services snapshots. Phase08 owns broad business workflows, approvals, import preview and advanced UI; Phase09 owns private keys/signing; Phase10 owns live tax-authority transmission; Phase11 owns asynchronous inquiry and reconciliation; Phase12 owns entitlements, billing and settlement. Phase07 does not claim official authority approval or live submission.

## Repository evidence (2026-10-17)

- Examined `docs/roadmap/18-phases.md`, `docs/phases/phase-06.md`, `README.md`, source and tests on `main`.
- Phase06 states owner-approved Steps 01–16, with successful macOS and Ubuntu checks reported by the operator, not independently rerun in Step01. Its release/tag status must not be inferred from that documentation alone.
- `src/ArginTaxPayer.Domain/Catalog/InvoiceCatalogHandoff.cs`: version 1 handoff includes customer and catalog item IDs/revisions, item kind, source and unit releases and SHA256, official mapping, effective/capture UTC instants, and `InvoiceVatResolution.Unresolved`.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/InvoiceCatalogHandoffService.cs`: authenticated customer-scoped read-only capture, MFA-checked partner/root access, active and reviewed reference constraints, repeatable-read snapshot. It neither issues invoices nor resolves taxes.
- `tests/ArginTaxPayer.Tests/Phase06InvoiceHandoffTests.cs`: source-level coverage for half-open UTC validity, goods/service distinction, source eligibility, contract completeness and nonissuance.
- Existing invoice *participant* forms/endpoints predate Phase07; do not mistake them for a complete fiscal invoice aggregate.
- Developer commands exist: `scripts/dev-up.zsh`, `scripts/dev-status.zsh`, `scripts/dev-down.zsh`, `scripts/ubuntu-dev.sh`.
- Existing `README.md` describes older milestone status and legacy Level2 taxpayer ownership; roadmap's dated correction governs: commercial ownership is at customer account level, with prospective ownership-transfer semantics.

## Step01 architecture decisions (proposed; pending owner approval)

1. **Boundary and aggregates.** Introduce `Invoice` as a customer-scoped aggregate. Record `CustomerUserId`, explicit `TaxpayerId`, issuer/buyer participant snapshots, an invoice-level type/pattern/subject and immutable line snapshots. Validate current customer→taxpayer authorization server-side and keep root/Level2 roles scoped through current commercial ownership. Never use taxpayer-to-Level2 legacy grants.
2. **Handoff contract.** Consume Phase06 `InvoiceCatalogHandoff` V1 as input and copy the entire provenance-bearing value into invoice line history; never persist only an item pointer. Preserve stock/nonstock/service distinction locally; map the official goods/service classification independently. Do not change Phase06 contract during Step01.
3. **Tax and calculation.** `Vat = Unresolved` is not a tax rate. Do not interpret it as zero or infer a current rate. Separate taxable basis, tax resolution, discounts, charges, rounding, settlement and display formatting. Block tax-ready finalization on unresolved mandatory tax classification. Store exact monetary semantics and precision with a versioned policy once the authoritative specification is reviewed.
4. **Specification-driven rules.** Store source document ID, edition, source checksum, valid-from/to and field applicability per rule set. Provisional combinations require authoritative verification and are not treated as officially valid. An authoritative matrix must address type, pattern, subject, taxpayer/party and reference requirements together. Test negative combinations and effective-date boundaries.
5. **Reference integrity.** Corrections, cancellations and sales returns reference an immutable predecessor with tenant/taxpayer guards. Enforce acyclic chains, legal predecessor states and eligible quantities; retain historical nodes. The precise authority reference and state prerequisites must be sourced before coding Steps14–16.
6. **Separation of states.** Local Draft/Validated/Finalized states are not authority Accepted/Rejected states. Signing, dispatch, external status and retries belong to later phases. Local finalization does not imply fiscal acceptance.
7. **Security.** Authorization and resource scoping in the API and data access; deny cross-customer, former-partner, cross-taxpayer references, and unauthorized writes. External rule and error messages cannot disclose secrets or foreign-tenant records.
8. **Concurrency and audit.** Revision-based optimistic concurrency, idempotency per operation and immutable audit trail. Finalized snapshots cannot be silently rehydrated from mutable catalog values.
9. **Migration safety.** Step01 creates *documentation only*: no schema, SQL, runtime routes, external calls, production changes or seeds. Steps involving EF migrations require explicit preview and `SELECT current_database()` confirming `argintaxpayer_test`, without automatic startup migration.
10. **Source review gate.** Before Step03/08/09/11/14–16 implementation, check current official Iranian taxpayer e-invoice instructions, field schema, reference rules and effective versions. Phase06 notes `RC_IITP.IS V7.9` as a previously owner-supplied source; its currency must be revalidated, not assumed.

## Fixed 24-step implementation and acceptance plan

| Step | Title | Deliverable | Acceptance | Status |
|---|---|---|---|---|
| 01 | Architecture and contracts | Inspect live repository, Phase06 handoff, security and roadmap; freeze boundaries, ADR and acceptance | Owner architecture approval; no runtime or schema changes | Documentation committed; owner review pending |
| 02 | Invoice aggregate | Identity, ownership, header, parties, invoice invariants | Domain and isolation tests | Not started |
| 03 | Types and patterns | Versioned invoice types/pattern matrix | Allowed and forbidden combinations verified against cited official source | Not started |
| 04 | Invoice subjects | Original, correction, cancellation and sales return subject model | Invalid subject transitions rejected | Not started |
| 05 | Invoice lines and catalog handoff | Copy Phase06 V1 immutable snapshots; quantities and units | No mutable-catalog dereference after snapshot | Not started |
| 06 | Financial model | Decimal totals, VAT inputs, discount and rounding policies | Boundary and deterministic calculation cases | Not started |
| 07 | Rule-engine contracts | RuleContext, stable rule keys, severities and deterministic evaluation | Unit-testable pure contracts | Not started |
| 08 | Header validation | Required identifiers, issue time and participant rules | Field-level positive and negative tests | Not started |
| 09 | Type/pattern rules | Field requirements by officially effective matrix | Reject unsupported combinations | Not started |
| 10 | Line and catalog rules | Official identifiers, mapping, unit, kind and source effective date | Fail closed on unresolved or expired sources | Not started |
| 11 | Monetary and tax rules | Totals, VAT resolution and precision validation | No universal rate assumption; exact totals | Not started |
| 12 | Rule-set versioning | Effective-dated immutable rule bundles and localized errors | Historical deterministic replay | Not started |
| 13 | Invoice lifecycle | Draft/validated/finalized and separation of authority outcomes | Illegal transitions rejected | Not started |
| 14 | Correction reference | Immutable predecessor linkage and permitted modifications | Cycle/invalid predecessor blocked | Not started |
| 15 | Cancellation reference | Reference integrity and duplicate-operation guards | No inconsistent second cancellation | Not started |
| 16 | Sales-return reference | Line-level partial/cumulative return constraints | Cannot return more than eligible quantity | Not started |
| 17 | Concurrency and idempotency | Revision/CAS, scoped reference integrity, replay key policy | Concurrent conflict and replay tests | Not started |
| 18 | Persistence and migrations | EF Core PostgreSQL additive schema and constraints | Test DB-only migration review; no production modifications | Not started |
| 19 | Application services and API | Scoped commands/queries and stable error contracts | Authorization and API contract tests | Not started |
| 20 | Invoice-entry UI | RTL dynamic type/pattern, party and item selection | Usable keyboard and responsive flows | Not started |
| 21 | Validation/reference UI | Field errors and related-document history | Accessibility and scoped operation tests | Not started |
| 22 | Comprehensive regression | Domain/integration/contract/security suite | Negative cases, Phase03–06 compatibility | Not started |
| 23 | macOS and Ubuntu acceptance | Local suite, VPS verify, DB/HTTP operator evidence | Both environments owner-approved | Not started |
| 24 | Documentation and release | Close phase record, merge, annotated tag, GitHub release | Separate approval and release evidence | Not started |

## Working protocol

After each step: record changed paths and commit, domain/schema/security impact, focused tests, negative cases, operator macOS evidence, Ubuntu evidence where meaningful, and explicit owner approval. Do not start the following step without approval. Keep all step completion records in this file to avoid creating dozens of docs.

Mac local development: `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh`, `zsh scripts/dev-down.zsh`; existing runner manages AutoSSH to loopback `127.0.0.1:15432` with keepalive and exit-on-forward-failure. Ubuntu verification: `bash scripts/ubuntu-dev.sh verify` and operator-only loopback smoke where relevant. Avoid starting services or modifying DB solely to verify Step01's documentation.

## Step 01 — Architecture and Contracts — Completion Record

**Work done:** Repository and Phase06 source contracts inspected; cross-phase bounded context, lifecycle, catalog handoff and invariants documented; frozen 24-step plan created.
**Changed paths:** `docs/phases/phase-07-invoice-domain-rule-engine.md` only.
**Database migration:** None. **Runtime behavior:** None. **Build/tests:** Not executed (documentation-only step). **Release:** Not initiated.
**Open issues/gates:** Official current field-pattern matrix/source verification; invoice numbers and reference semantics; detailed monetary precision and rounding; possible conflict between Phase07 UI steps and the fuller Phase08 workflow scope; owner acceptance.
**Acceptance checklist:**
- [x] Inspect live repository, 18-phase roadmap and Phase06 invoice handoff implementation/tests.
- [x] Establish scope, design invariants, ownership, source rule/version strategy and negative-test requirements.
- [x] Freeze 24-step titles and outcomes in one phase file.
- [x] Create dedicated Phase07 Git branch and commit documentation.
- [ ] Obtain owner approval of Step01 before implementing Step02.

**Owner signoff:** Pending.
