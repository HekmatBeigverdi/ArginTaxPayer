# ArginTaxPayer — Phase 07: Invoice Domain and Rule Engine

**Status:** Step 01 owner-approved 2026-10-17; Step 02 owner-approved 2026-10-17 (macOS test logs not attached); Step 03 owner-approved 2026-10-17 (test output not attached); Step 04 implemented and awaiting macOS test and owner acceptance. Steps 05–24 not started.
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
| 01 | Architecture and contracts | Inspect live repository, Phase06 handoff, security and roadmap; freeze boundaries, ADR and acceptance | Owner architecture approval; no runtime or schema changes | Owner-approved 2026-10-17 |
| 02 | Invoice aggregate | Identity, ownership, header, parties, invoice invariants | Domain and isolation tests | Owner-approved 2026-10-17; test outputs not attached |
| 03 | Types and patterns | Versioned invoice types/pattern matrix | Allowed and forbidden combinations verified against cited official source | Owner-approved 2026-10-17; current-spec verification remains an explicit release gate |
| 04 | Invoice subjects | Original, correction, cancellation and sales return subject model | Invalid subject transitions rejected | Implemented; macOS tests and owner acceptance pending |
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

**Owner signoff:** Approved 2026-10-17.

## Step 02 — Invoice Aggregate — Completion Record (2026-10-17)

**Implementation:** Added the independent `ArginTaxPayer.Domain.Invoices.InvoiceAggregate` root and immutable participant snapshots. The invoice has immutable `Id`, `CustomerUserId`, `TaxpayerId`, optional `FiscalMemoryId`, `CreatedAtUtc`; it begins as a local Draft with revision 1 and supports validated changes to seller/buyer and UTC issue date, incrementing revision only on change. These are internal application identities, not a regulatory invoice number or issued fiscal record.

**Security boundary:** Customer→taxpayer/memory membership and live Level1/Level2 scope must be checked by Application/Infrastructure before calling `CreateDraft`. Domain construction is NOT an authorization gate; no API or persistence was added in this step. Do not infer membership from matching arbitrary GUIDs or legacy taxpayer-level partner grants. Future persistence must use customer-scoped reads and optimistic concurrency.

**Scope intentionally deferred:** Type/pattern Step03; issue subject Step04; catalog line snapshots Step05; financial rules Step06; rule engine Steps07–12; lifecycle transitions Step13; references Steps14–17; EF model/migration Step18; authorization and API Step19.

**Changed paths:**
- `src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs`
- `tests/ArginTaxPayer.Tests/Phase07InvoiceAggregateTests.cs`
- `docs/phases/phase-07-invoice-domain-rule-engine.md`

**Migration:** None. **Production changes:** None. **Runtime routes:** None.
**Validation:** Tests added, not executed through GitHub connector; macOS operator execution required. No Ubuntu test necessary for a pure-domain addition.

**macOS commands:**
```bash
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter FullyQualifiedName~Phase07InvoiceAggregateTests --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Acceptance:** Valid/invalid identity and dates, immutable ownership properties, party snapshot validation, draft revision progression, and no premature issuance. Cross-customer isolation at API/DB remains a Step19 requirement, not a completed Step02 test.

**Owner approval:** Approved 2026-10-17 by owner; local test logs not independently attached.

## Step 03 — Types and Patterns — Completion Record (2026-10-17)

**Implementation:** Introduced `ElectronicInvoiceType`, nullable pattern `InvoiceTypePattern`, immutable `InvoicePatternSource`, `InvoicePatternMatrix` and explicit-edition `InvoicePatternRegistry`. Historical notice No. 6 records Type1 patterns 1–7; Type2 patterns 1–2; Type3 patternless POS/payment-gateway receipt. The historical rule matrix contains exactly 10 combinations; no implicit fallback for unknown editions or unsupported pairs. Effective times use UTC half-open windows. There is no tax authority claim for this old notice.

**Regulatory sources examined:** Notice No. 6, dated 1401/11/18, reproduced at `https://taksatsp.ir/اطلاعیه-شماره-6/` and `https://maliatman.com/انواع-الگوهای-صورتحساب-الکترونیکی-قو/` (tax-authority announcement reproduced by third parties). Prior Phase06 records an owner-provided `RC_IITP.IS V7.9`, but a currently authoritative complete matrix, including later additions/withdrawals and pattern field-level requirements, was not independently authenticated in this step. Treat this built-in matrix as **historical reference only**. `VerifiedForSubmission=false`, so `requireSubmissionVerified=true` fails closed until a current edition is independently verified and registered.

**Date limitation:** Historical effective-from uses 1402/01/01 as an illustrative modern effective period boundary taken from notice applicability; it must not be used to claim that all patterns had exactly that regulatory effective date. Update the effective intervals when official edition dates are verified. This is not automatic date based approval.

**Changed files:**
- `src/ArginTaxPayer.Domain/Invoices/InvoicePatternMatrix.cs`
- `tests/ArginTaxPayer.Tests/Phase07InvoicePatternsTests.cs`
- `docs/phases/phase-07-invoice-domain-rule-engine.md`

**Scope guard:** No changing Phase06 catalog, Step02 aggregate, VAT, subjects, API, DB schema, migration, or public route. Step09 will handle pattern-specific field validation, and Step12 will manage rule bundle persistence/effective edition. Step03 is classification only.

**Tests added:** supported/unsupported pairs; Type3 no pattern; explicit missing edition; current submission trust fail-closed; UTC half-open date checks; invalid matrix rejection; collection immutability. Tests are not independently executed by GitHub connector.

**macOS:**
```bash
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07InvoicePatternsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Migration:** None. **Ubuntu test:** Deferred; pure Domain code. **Owner approval:** Approved 2026-10-17 by owner; test logs not independently provided. Current authoritative matrix verification remains open.

## Step 04 — Invoice Subjects — Completion Record (2026-10-17)

**Status:** Committed; operator macOS tests and owner acceptance pending.

**Scope:** Map historical `ins` numeric subject values: original=1, correction=2, cancellation=3, sales return=4; this is separate from invoice type `inty` and local lifecycle. Original carries no predecessor. Each other subject requires one well-formed external reference tax ID (`irtaxid`, 22 ASCII alphanumeric characters, with no trimming or lossy conversion), optionally linked to a distinct internal local invoice ID. Reject unknown subject values, empty/invalid reference, and immediate self-reference. This checks syntax and shape only, not existence or authority validity.

**Domain contract:** `InvoiceSubjectSelection.Create` validates subject/reference pairs; `InvoiceAggregate.CreateDraft` defaults to original without breaking Step02 callers and accepts a subject selection as final optional argument. `UpdateDraftSubject` is draft-only and increments revision only on actual change. It does not issue, validate a reference chain, or alter customer/taxpayer identity. Details of predecessor eligible state, chronological order, cross-tenant access, multiple returns and official restrictions are deferred explicitly to Steps14–17 and 19; they must not be mistaken for a currently valid tax operation.

**Sources / caution:** Historical tax-authority technical instruction `RC_TICS.IS_V01` lists header `ins` as 1/2/3/4 and `irtaxid` as 22-character field. See `https://hpmgroup.co/wp-content/uploads/2022/09/سند_دستورالعمل_فنی_نحوه_اتصال_به_سامانه_مودیان14010422.pdf`. A later reference-rule discussion: `https://chekad.com/other/سامانه-مودیان-مالیاتی/`. This is not proof of the latest official version; Step12 and later authority verification remain required before marking any invoice ready to submit.

**Changed paths:**
- `src/ArginTaxPayer.Domain/Invoices/InvoiceSubjects.cs` (new).
- `src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs` (add subject selection, edit guard).
- `tests/ArginTaxPayer.Tests/Phase07InvoiceSubjectsTests.cs` (new).
- `docs/phases/phase-07-invoice-domain-rule-engine.md`.

**Validation cases:** Four numeric subjects; required reference for three referring subjects; no reference on original; 22-character format, no whitespace; no direct self-reference; no-op update revision stability; authorization-relevant identity unchanged; unknown enum rejected. Existing Step02 tests must continue passing.

**macOS commands:**
```bash
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07InvoiceSubjectsTests|FullyQualifiedName~Phase07InvoiceAggregateTests|FullyQualifiedName~Phase07InvoicePatternsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Migration:** None. **API/UI:** None. **Ubuntu:** Not necessary for domain-only change. **Build/tests:** Source committed; macOS execution evidence not attached. **Owner approval:** Pending.

## Official source priority correction — owner-provided RC_IITP.IS V7.9 (2026-10-17)

**Authoritative input:** The owner uploaded `RC_IITP_IS_V7_9_2(1).pdf` (116 pages), document ID `RC_IITP.IS_V7.9`, Tir 1405. Treat the actual supplied Tax Authority specification as the primary source. Third-party websites are supplemental only; never substitute an older intermediary notice for a newer supplied primary instruction.

**Step03 substantive correction:** Prior embedded Notice 6 historical combinations (Type1 1–7, Type2 1–2) are INCOMPLETE and MUST NOT be selected as the primary V7.9 rules. V7.9 Table 1 p14 and Table 9 p31 provide:
- Type1 patterns 1,2,3,4,5,6,7,8,9,11,13,14 (12 patterns).
- Type2 patterns 1,3,9,13 (4 patterns).
- Type3 is the POS/payment-gateway receipt described in section 3-3 p12, without an ordinary Type1/2 pattern.
- Never silently renumber patterns: Type2 gold is 3 (not 2), and Type1 10/12 are not listed.
- New `OfficialV79InvoicePatternMatrix` is authoritative **for document-content matching**. The obsolete `HistoricalInvoicePatternMatrix` was removed in a subsequent corrective commit; only the official V7.9 catalogue remains in the active domain code. `VerifiedForSubmission=false` still blocks claiming current applicability until issuance effective dates and other regulatory gating are independently confirmed.
- The PDF states edition month Tir 1405 but does not establish exact effective-from dates for all listed patterns. In code `DateTime.MinValue` is explicitly an unrestricted document-catalogue sentinel, NOT an assertion of historical legal effectiveness.

**Step04 cross-check:** V7.9 section 5 pp15–18 and Table10 p32 establish subjects 1=original, 2=correction, 3=cancellation, 4=sales return; correction/cancellation/return require `irtaxid` (Table8 p30). Table10 disallows sales returns on Type1 export (7), waybill (8), and commodity-linked securities (11); added `OfficialV79InvoiceSubjectPolicy`. Other reference-chain, immutable fields, transaction chronology and status rules remain scheduled for Steps14–17.

**New tests:** `Phase07OfficialV79Tests` verifies all 17 documented type/pattern combinations, negative gaps and special subject restrictions. The earlier tests were corrected in a subsequent commit to use V7.9; obsolete notice-based expectations were removed.

**Repository change:** Official PDF is a user-supplied source in the conversation; the 5 MB binary has NOT been checked into the public repo. To make this reproducible later, store an authorized immutable source copy or source fingerprint in the regulated specification registry (future Step12), following source licensing and project document policy.

**macOS validation:**
```bash
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07OfficialV79Tests|FullyQualifiedName~Phase07InvoicePatternsTests|FullyQualifiedName~Phase07InvoiceSubjectsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
```

**Migration:** None. **External submission:** None. **Tests:** Added but awaiting owner macOS results. **Process change:** Source-of-truth priority confirmed for all remaining Phase07 steps.

## Follow-up correction — Remove obsolete third-party pattern source (2026-10-17)

Owner observed that `HistoricalInvoicePatternMatrix` and a non-authority website still existed in live production-domain source after the official V7.9 matrix was added. The earlier correction was incomplete.

- Removed `HistoricalInvoicePatternMatrix` and its third-party URL from `InvoicePatternMatrix.cs`; there is now exactly one built-in pattern catalogue, `OfficialV79InvoicePatternMatrix`, sourced from owner-provided `RC_IITP.IS V7.9`, Table 9 p31.
- Migrated `Phase07InvoicePatternsTests` to V7.9 and removed misleading Notice 6 test expectations, including incorrect Type2 pattern 2 and incomplete Type1 range.
- Kept dynamic version registry support for future *official* documents. `VerifiedForSubmission=false` remains a separate applicability safety gate; document evidence does not imply live authority acceptance.
- Old commit history cannot and should not be rewritten; prior source URL will remain visible in historical commits and in documentation about why correction was needed, but not in current domain source.
- This is a domain/test/docs-only change; no schema migration, API modification, or authority submission.
- Owner macOS validation: `dotnet test ArginTaxPayer.sln --no-restore`. Verification not yet run by assistant in macOS.
