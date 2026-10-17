# ArginTaxPayer — Phase 07: Invoice Domain and Rule Engine

**Status:** Step 01 owner-approved 2026-10-17; Step 02 owner-approved 2026-10-17 (macOS test logs not attached); Step 03 owner-approved 2026-10-17 (test output not attached); Step 04 owner-approved 2026-10-17 (macOS evidence not attached); Step 05 owner-approved 2026-10-17 (operator test log not attached); Step 06 owner-approved 2026-10-17 (operator test logs not attached); Step 07 owner-approved 2026-10-17 (operator test logs not attached); Step 08 owner-approved 2026-10-17 (test logs not independently attached); Step 09 owner-approved 2026-10-17 (operator test logs not supplied); Step 10 owner-approved 2026-10-17 (operator test logs not attached); Step 11 owner-approved 2026-10-17 (operator test results not independently attached); Step 12 owner-approved 2026-10-17 (independent macOS logs not attached); Step 13 owner-approved 2026-10-17 (macOS logs not supplied); Step 14 owner-approved 2026-10-17 (operator macOS logs not attached); Step 15 owner-approved 2026-10-17 (operator macOS logs not attached); Step 16 sales-return domain rules implemented, awaiting macOS verification and owner approval. Steps 17–24 not started.
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
| 04 | Invoice subjects | Original, correction, cancellation and sales return subject model | Invalid subject transitions rejected | Owner-approved 2026-10-17; local test logs not attached |
| 05 | Invoice lines and catalog handoff | Copy Phase06 V1 immutable snapshots; quantities and units | No mutable-catalog dereference after snapshot | Owner-approved 2026-10-17; test output not independently provided |
| 06 | Financial model | Decimal totals, VAT inputs, discount and rounding policies | Boundary and deterministic calculation cases | Implemented; macOS tests and owner acceptance pending |
| 07 | Rule-engine contracts | RuleContext, stable rule keys, severities and deterministic evaluation | Unit-testable pure contracts | Owner-approved 2026-10-17; operator test logs not attached |
| 08 | Header validation | Required identifiers, issue time and participant rules | Field-level positive and negative tests | Owner-approved 2026-10-17; operator test logs not attached |
| 09 | Type/pattern rules | Field requirements by officially effective matrix | Reject unsupported combinations | Owner-approved 2026-10-17; test logs not independently attached |
| 10 | Line and catalog rules | Official identifiers, mapping, unit, kind and source effective date | Fail closed on unresolved or expired sources | Owner-approved 2026-10-17; operator test logs not attached |
| 11 | Monetary and tax rules | Totals, VAT resolution and precision validation | No universal rate assumption; exact totals | Owner-approved 2026-10-17; operator test logs not attached |
| 12 | Rule-set versioning | Effective-dated immutable rule bundles and localized errors | Historical deterministic replay | Owner-approved 2026-10-17; operator test logs not attached |
| 13 | Invoice lifecycle | Draft/validated/finalized and separation of authority outcomes | Illegal transitions rejected | Owner-approved 2026-10-17; operator test logs not supplied |
| 14 | Correction reference | Immutable predecessor linkage and permitted modifications | Cycle/invalid predecessor blocked | Owner-approved 2026-10-17; operator logs not attached |
| 15 | Cancellation reference | Reference integrity and duplicate-operation guards | No inconsistent second cancellation | Owner-approved 2026-10-17; operator macOS logs not attached |
| 16 | Sales-return reference | Line-level partial/cumulative return constraints | Cannot return more than eligible quantity | Domain implementation committed, macOS tests and owner acceptance pending |
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

## Step 05 — Invoice Lines and Catalog Handoff — Completion Record (2026-10-17)

**Status:** Implemented and committed; macOS verification and owner acceptance pending. Step04 marked owner-approved; no separate operator macOS logs were provided.

**Official primary source:** Owner-supplied `RC_IITP.IS V7.9` (Tir 1405), field validation tables 29–32, pp50–52 (goods/service ID `sstid`, title `sstt`, quantity `am`, measurement unit `mu`); SDK 1405 pp9–10 is DTO-shape evidence, not normative rules. Unit/mapping provenance comes from Phase06 V1, not a tax authority rule. See [reference policy](../references/tax-authority-sdk-catalog.md).

**Implementation:** 
- Immutable `InvoiceLineSnapshot` captures a value copy of the *complete* authenticated Phase06 `InvoiceCatalogHandoff` V1: customer and catalog IDs, catalog and mapping revisions, item kind, internal and official descriptions, unit references, release documents/editions/SHA256, official mapping validity interval, effective and capture UTC times and unresolved VAT state. No mutable catalog row lookup occurs after capture.
- `InvoiceAggregate.AddDraftLine` accepts the externally authorized handoff, ensures same `CustomerUserId`, complete V1, effective mapping at handoff's date, unique nonempty invoice line GUID, positive decimal quantity, and one-based line ordering.
- `UpdateDraftLineQuantity` changes only quantity; `RemoveDraftLine` reindexes subsequent positions. Both operate only on local Draft and bump revision on actual mutation. Public Lines exposed via a non-modifiable read-only collection.
- `sstid`, `sstt`, `mu` are projections of the captured official values. Quantity `am` is decimal internally; wire format/precision and pattern-specific validation are deferred to Steps06/10/11 and the official edition.
- Neither a VAT rate nor zero VAT is inferred from `InvoiceVatResolution.Unresolved`. No finalization, issuer authorization, persistence, tax readiness, issuance, submission or transport behavior added.
- The `CustomerUserId` check protects against simple cross-customer handoff injection; live access to customer, taxpayer, catalog item and issuance rights *must* be checked by the Application handoff capture and future APIs. Domain IDs alone are not a security credential.

**Changed:** `src/ArginTaxPayer.Domain/Invoices/InvoiceLineSnapshot.cs`; `src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs`; `tests/ArginTaxPayer.Tests/Phase07InvoiceLinesTests.cs`; this single phase document.

**Tests:** Verify independent captured values after catalog changes, provenance/versions, cross-customer failure atomicity, invalid mappings and version rejection, no-op revisions, invalid quantities/duplicates, remove/reindex, immutable exposed line collection, services vs stock goods. Existing Phase06 and prior Phase07 tests must remain green.

**macOS:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07InvoiceLinesTests|FullyQualifiedName~Phase06InvoiceHandoffTests|FullyQualifiedName~Phase07InvoiceAggregateTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```
**Migration:** None. **Ubuntu:** Not required for domain-only change; final platform checks still Step23. **Owner acceptance:** Pending.

## Step 06 — Financial Model — Completion Record (2026-10-17)

**Implementation committed. Owner acceptance:** pending operator macOS tests. **Step05:** owner-approved 2026-10-17; operator test evidence not independently attached.

### Official source traceability

Primary: owner-provided Iranian Tax Authority `RC_IITP.IS V7.9`, Tir 1405:
- Tables 15–20, **pp39–42**: `tprdis` sum before discount; `tdis` sum discount; `tadis` sum after discount; `tvam` sum VAT; `todam` sum other taxes/legal dues; `tbill` sum line totals.
- Table40, **p58**: ordinary `prdis = am * fee`; currency exchange and airline have different formulae.
- Table41, **p59**: `0 <= dis <= prdis`.
- Table42, **p60**: ordinary `adis = prdis - dis`; gold pattern and commodity-exchange pattern differ.
- Tables43–44, **pp61–63**: the VAT rate `vra` is controlled by official goods/service classification and law (not universal). Ordinary `vam = adis * vra / 100`; gold, foreign exchange and petroleum variants are different. Exemptions and zero-rated cases require relevant legal classifications.
- Monetary output fields in these tables have **zero allowed decimal places**. These passages do not establish a universal midpoint/tie-breaking algorithm. The model uses a **caller-selected edition-named rounding policy** for *draft preview only*, not a claim of official rounding compliance.
- Relevant technical SDK evidence: official 1405 certificate SDK manual pp9–13 lists `InvoiceDto` monetary DTO fields. Primary validation rules still come from `RC_IITP.IS V7.9`. Consult mandatory [official source/SDK catalogue](../references/tax-authority-sdk-catalog.md).

### Domain behavior and scope

- Added `InvoiceFinancialInput`, `InvoiceFinancialLine`, `InvoiceFinancialPreview`, explicit `InvoiceRoundingPolicy` and pure `InvoiceFinancialCalculator` in `InvoiceFinancialModel.cs`. All arithmetic is `decimal` with checked overflow, in **IRR (Rial)** rather than toman.
- Supported preview is deliberately restricted to **Type 1, Pattern 1 (ordinary sale of goods/services)**. Every other type or pattern throws `NotSupportedException` rather than silently applying general formulae to gold, foreign exchange, exports, airlines, bills, petroleum etc.
- For each line: `prdis = Round(am*fee)`; nonnegative whole-rial `dis <= prdis`; `adis = prdis - dis`; with an *explicitly supplied* VAT percentage, `vam = Round(adis*vra/100)`; other taxes/legal fees are nonnegative separate whole-rial inputs, total = after-discount + VAT + those extras. Header totals are checked sums of line outputs.
- `VatRatePercent = null` means **unresolved** and causes `VatRial` and `GrandTotalRial` to remain `null`, never zero. Explicit 0% is a different input and is **not** legal evidence of exemption. `InvoiceVatResolution.Unresolved` in Phase06 remains untouched; therefore a preview is **never** authorization to sign/send.
- The aggregate offers `PreviewDraftFinancials(inputs, typePattern, rounding)`: read-only and Draft-only. It requires financial inputs to match **all** current invoice line IDs and quantities, rejects duplicates/foreign/removed/stale line IDs, and does not persist amounts or mutate revisions, source snapshot or VAT classification.
- Intentionally **not included**: per-taxpayer legal rate verification, regulatory effective-period selection, special-pattern formulae, definitive currency decimal/rounding standard, actual issuance and finalization. These belong to fixed Steps07–13. Price and VAT inputs are supplied by caller for preview only and must not be trusted for tax submission.

### Changes and tests

Files:
- `src/ArginTaxPayer.Domain/Invoices/InvoiceFinancialModel.cs` (new)
- `src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs`
- `tests/ArginTaxPayer.Tests/Phase07InvoiceFinancialTests.cs` (new)
- `docs/phases/phase-07-invoice-domain-rule-engine.md`.

Coverage includes multiple decimal-quantity lines and header reconciliation, missing VAT vs explicit zero, rounding at midpoint under two explicit policies, negative/excessive discount, negative VAT rate, special-pattern fail-closed, stale/foreign invoice-line inputs and checked decimal overflow.

**macOS:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07InvoiceFinancialTests|FullyQualifiedName~Phase07InvoiceLinesTests|FullyQualifiedName~Phase06InvoiceHandoffTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Database migrations:** none. **Ubuntu:** deferred for domain-only code. **No live Tax Authority calls.** Acceptance and official financial-rule readiness are separate gates.

## Step 07 — Rule-engine contracts — Completion Record (2026-10-17)

**Status:** Code and tests committed; local macOS operator tests and owner approval pending. **Step06:** owner-approved on 2026-10-17, but raw test logs not attached.

### Contract and standards provenance

- Authority source: owner-provided Tax Authority `RC_IITP.IS V7.9` (Tir 1405). Step07 does **not** purport to implement substantive authority validity: its contract must let upcoming rules cite an explicit official document ID, edition, section/table/page and optional SHA-256 checksum. For example, Table9 p31 addresses patterns, Table10 p32 subjects, Tables15–20 pp39–42 header monetary totals and Tables40–44 pp58–63 line monetary/VAT rules. Complete effective dates and cross-pattern requirements are **not** inferred by this step.
- Mandatory reference governance: [Tax Authority & SDK source catalogue](../references/tax-authority-sdk-catalog.md). Both SDK families 0.0.x and 2.0.x remain separately retained as evidence. SDK DTOs and third-party blogs **do not** set legal validation policy.
- This is a deliberately *internal* Domain contract. The official fiscal document does **not** prescribe these C# type names, internal rule keys, result envelopes, sort order or message-key taxonomy. Those are project architecture choices for deterministic, testable implementation.

### Implementation

- New `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleEngine.cs`: `InvoiceRuleKey` ensures stable lowercase dot/underscore identifiers; `InvoiceRuleSeverity` has Information, Warning and Error.
- `InvoiceRuleSource` captures official document ID, edition, exact section/page and optional 64-character hex source checksum.
- `InvoiceRuleContext` freezes invoice ID, customer ID, taxpayer ID, revision, UTC issue date, invoice type/pattern, subject, invoice line IDs and explicit rule-set edition. It snapshots the line-ID enumerable into a read-only array to prevent external mutation. *Identities are scope metadata, not proof of authorization*; API and persistence must verify the actor's access.
- `IInvoiceRule` exposes key, provenance and an evaluation method. Findings carry a rule key, severity, stable localized message key, and optional field path, not dynamic sensitive content.
- `InvoiceRuleEngine` rejects duplicate rule keys and rule-set edition mismatch. It snapshots registrations, evaluates in rule-key order, and sorts findings by rule key, path, severity and message key (ordinal comparison). It rejects null or foreign-key findings. Exceptions propagate, so internal failures cannot be interpreted as successful validation.
- `InvoiceRuleEvaluation` reports `HasErrors` but **always returns `IsSubmissionAuthorized=false` in Step07**. An empty rule set or lack of findings does not assert fiscal correctness.
- Rules are required by design to be pure and free of I/O, wall-clock time and mutable global state; this contract does not enforce purity on arbitrary third-party implementations. Rule bundling with effective dates and formal submission authority remains reserved for fixed Step12.

### Scope and regression

- Source-only Domain and xUnit changes. **No database/EF migration**, no API or UI routes, no signer, network, settlement, document status transition or authority submission.
- Added `tests/ArginTaxPayer.Tests/Phase07RuleEngineContractsTests.cs`: stable key/value semantics, provenance, deep-enough snapshot isolation for line IDs, deterministic order independent of registration order, immutable output collections, empty-set fail-closed, duplicate-key/edition rejection, foreign finding rejection, exception propagation, and invalid severities.
- Existing steps 02–06 are unchanged. The main `InvoiceAggregate` remains separate from rules; Step08 and later will connect specific official field validators, and Step12 will manage effective-dated certified rule sets.

### Operator macOS commands

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07RuleEngineContractsTests|FullyQualifiedName~Phase07InvoiceFinancialTests|FullyQualifiedName~Phase07InvoicePatternsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Ubuntu:** no separate test needed for Domain-only Step07; final environment acceptance stays Step23. **Production database:** untouched. **Owner gate:** pending.

## Step 08 — Header validation — Completion Record (2026-10-17)

**State:** Implemented in branch and committed; macOS execution and owner approval pending. Step07 was explicitly owner-approved 2026-10-17; no macOS logs independently attached.

**Mandatory primary-source basis:** owner-supplied Tax Authority `RC_IITP.IS V7.9` (Tir 1405), **Table 3 p24** (`taxid` required, length 22; full check algorithm described separately in `RC_DCPS.SN`), **Table 4 pp25–26** (`indatim` Unix epoch milliseconds, not future, issue-date/reference/tax-ID chronological consistency), **Table 10 p32** (buyer fields **outside pattern** on referring subjects), and **Table 11 pp33–35** (`tins` 11/14 digits, `tinb`, `tob`, `bid`, `bpc`, branch fields and buyer requirements). SDK guidance is supplemental for serialization only; see [mandatory reference catalogue](../references/tax-authority-sdk-catalog.md).

**Implementation:**
- New `InvoiceHeaderFields`: immutable projected tax-authority field values, kept separate from internal `InvoicePartySnapshot` so that a person's display name or arbitrary free-form identifier is **never assumed** to be verified `tins`/`tinb`. All values are caller-supplied until checked against authoritative registration/credentials.
- `InvoiceRuleContext` now accepts optional projected header and caller-injected `AsOfUtc` clock; existing Step07 constructor calls remain valid. No implicit `DateTime.UtcNow` inside rules (determinism).
- Four distinct `IInvoiceRule` instances, registered with the existing deterministic engine: `invoice.header.taxid` required and 22 ASCII-alphanumeric structural shape; `invoice.header.issue_date` required 13-digit max Unix-ms matching snapshot UTC time and not future relative to injected clock; `invoice.header.seller` numeric economic ID 11/14 digits and 4-digit optional seller branch; `invoice.header.buyer` optional by invoice type/pattern and entirely outside-pattern for correction/cancellation/return, otherwise enforces buyer type (1..4), numeric economic/identity/postal/branch field shapes, and conditional minimal required buyer identifiers for Type1 ordinary sale.
- Stable finding message keys and official JSON field paths; findings carry no raw PII. Source metadata is `RC_IITP.IS` edition `V7.9` with page/table. `IsSubmissionAuthorized` always false. The engine is pure and does not do database lookups.
- **Guardrails/deferred:** `taxid` uniqueness, Verhoeff/check-digit and fiscal-memory/date encoding need official `RC_DCPS.SN` and issuance state; seller-to-taxpayer matching and buyer registration checks need scoped authority access; dynamic statutory time limits and `indati2m`/special Article9 flow require external versioned rules; type/pattern/subject applicability and custom pattern header fields are Steps09/12/14–17. All are **not implemented** and must be blocked before fiscal issuance. Note p32 Type1 cancellation has type/pattern fields outside pattern, so Step09 must project fields conditionally instead of treating a draft classification as a required payload field.
- **Important:** These checks are only the subset grounded by the cited tables; passing Step08 is **not** legal validity and no API will accept the outputs directly without the remaining validators.

**Changes:** `src/ArginTaxPayer.Domain/Invoices/InvoiceHeaderRules.cs` (new), `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleEngine.cs` (add immutable optional header and injected clock), `tests/ArginTaxPayer.Tests/Phase07HeaderValidationTests.cs` (new), this single phase document.

**Tests:** Valid Type1 ordinary legal buyer, missing/invalid taxid, Unix-ms required/mismatch/out-of-time, economic ID length and ASCII digits, natural-person identity+postal alternatives, legal buyer economic requirement, Type2 buyer optional, reference subjects buyer forbidden, special export/commodity-exchange optional, missing header fail closed.

**macOS commands:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07HeaderValidationTests|FullyQualifiedName~Phase07RuleEngineContractsTests|FullyQualifiedName~Phase07InvoiceSubjectsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```
**Migration:** None; **Ubuntu:** not required for Domain-only; **Owner acceptance:** Pending.

## Step 09 — Type/pattern rules — Completion Record (2026-10-17)

**Status:** Implemented and committed to Phase07 branch; owner acceptance and macOS tests pending. **Step08:** owner-approved 2026-10-17; operator test logs not independently attached.

**Mandatory primary sources:** Owner-supplied Iranian Tax Authority `RC_IITP.IS V7.9` (Tir 1405). **Table1 p14**: field presence matrix for sixteen Type1/Type2 patterns; **Table8 p30**: `irtaxid` reference is required for corrective, cancellation and sales-return subjects; **Table9 p31**: supported Type1 patterns 1,2,3,4,5,6,7,8,9,11,13,14; Type2 patterns 1,3,9,13; gold construction/profit/commission/total required; **Table10 p32**: export/waybill/commodity exchange cannot use sales return; cancellation `inty` and `inp` are outside pattern; referring subjects buyer fields are outside pattern; **Table11 pp33–35**: conditional person/branch, `crn` contract number for contracting, flight type `ft` for airline; **Table14 p38**: bill identifier for utility pattern. Rule schema is primary-source derived, not based on nonofficial articles. See [mandatory Tax Authority and SDK catalogue](../references/tax-authority-sdk-catalog.md). SDK 0.0.x/2.0.x kept as separate historic/current integration evidence, not as normative field requirements.

**Scope delivered:**
- New `InvoicePatternHeaderFields` is an immutable projected payload presence structure separate from `InvoiceRuleContext.Classification`. The internal classification stays available to reconstruct and validate cancellation invoices, but external `inty`/`inp` must be absent on cancellation according to Table10. Fields include outbound type/pattern, optional reference `irtaxid`, contracting `crn`, utility bill `billid`, flight `ft`, and four gold-specific components.
- New `OfficialV79TypePatternRuleFactory` creates four source-cited `IInvoiceRule` instances keyed `invoice.pattern.combination`, `invoice.pattern.subject`, `invoice.pattern.payload` and `invoice.pattern.conditional`, using existing deterministic Rule Engine.
- **Combination:** rejects every type/pattern pair absent from official V7.9 (including false legacy Type2 pattern2 and gaps 10/12). The existing historical matrix remains removed.
- **Subject:** rejects sales-return on Type1 export(7), waybill(8), commodity-linked securities(11), reusing V7.9 subject policy.
- **Payload:** requires matching type/pattern values when transmitted on noncancellation; requires their absence on cancellation; forbids reference on original and requires 22-character ASCII-alphanumeric reference shape on referring subjects. It does **not** certify referenced invoice existence or same workspace; see Steps14–17.
- **Conditional fields:** for first-type *original* contracting(4), utility bills(5), airline(6), validates presence and basic numeric shape of `crn`, `billid`, `ft` respectively. For Type1/Type2 gold(3), non-cancellation requires four monetary components (only presence, not values/formulae; those belong to Step11 and special-pattern calculators). Rejects unrelated `crn`, `billid`, `ft` on ordinary original patterns.
- Rule evaluation is read-only, all findings are stable localization keys and field paths, edition pinning is `RC_IITP.IS_V7.9`, and **`IsSubmissionAuthorized` remains false**.

**Limits, tracked as release gates:** Table1 is extensive; this step implements a verified *subset* of its field-level requirements and exceptions and does not claim complete serialization/pattern readiness. It does not implement gold amounts, exchange-rate payload, fuel calculations, insurance, trade chain, export customs information, stock-exchange evidence, pattern-specific body fields, and industry registration checks. Additional specialized validators and complete effective-dated rules will be added in fixed Steps10–12; until then, issuance must be blocked. Pattern/subject authority **effective dates** are not proven solely by the edition month of V7.9; the built-in matrix has `VerifiedForSubmission=false`. In particular, cancellation has omitted transmitted `inty`/`inp` although the internal classification has them.
 
**Changes:**
- `src/ArginTaxPayer.Domain/Invoices/InvoiceTypePatternRules.cs` (new)
- `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleEngine.cs` (optional immutable pattern payload projection in context)
- `tests/ArginTaxPayer.Tests/Phase07TypePatternRulesTests.cs` (new)
- This single master document.

**Tests:** All 17 officially documented type/pattern combinations; unsupported gaps; disallowed sales returns on export/waybill/securities; cancellation type/pattern absent; correction/reference-required, original/reference-forbidden; mandatory contracting/utility/flight fields; gold four fields; unrelated payload rejected; null payload fail closed; repeated evaluations deterministic. These tests have not been executed in operator macOS.

**macOS operator commands:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07TypePatternRulesTests|FullyQualifiedName~Phase07HeaderValidationTests|FullyQualifiedName~Phase07OfficialV79Tests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Database/migrations:** None. **Ubuntu-specific test:** Not necessary for domain-only change; Phase07 Step23 remains the full environment acceptance gate. **Owner approval:** Pending.

## Step 10 — Line and Catalog Rules — Completion Record (2026-10-17)

**Status:** Domain implementation and xUnit tests committed; macOS test run and owner approval pending. **Step09:** owner-approved 2026-10-17; no independent macOS logs supplied.

### Mandatory regulatory and SDK source traceability

Primary owner-uploaded tax-authority document `RC_IITP.IS V7.9` (Tir 1405):
- **Table 29 p50**: `sstid` is mandatory, exactly **13 ASCII digits**, and must match the tax-authority goods/services identifier database (`stuffid.tax.gov.ir`). The excerpt additionally specifies transport services, exchange services, stock-exchange seller evidence and chain goods/services whitelist; these require verified contextual evidence not carried by Phase06 V1.
- **Table 30 p51**: `sstt` is **optional**, at most 400 characters; do not incorrectly mark it mandatory in fiscal payload. The Phase06 internal handoff may require a local title.
- **Table 31 pp51–52**: `am` is mandatory, positive, at most **18 integer digits / 8 decimal digits**. Type1 patterns 6 (airline ticket) and 8 (waybill) require quantity 1 per line; comparison to export declaration / stock-exchange sales notice needs external evidence.
- **Table 32 p52**: `mu` is **optional**, ASCII-numeric, at most 8 characters, must come from a current reviewed `RC_UMGS.ST` release; the Phase06 contract requires an official unit resolution internally. Stock-exchange unit equality needs authoritative sales-notice evidence.
- **Table 29 p50**: Type1 patterns 11 (commodity-linked securities) and 14 (chain sale) permit only one goods/services line. No source is silently assumed to be "active today" just because a historical release is captured.
- **p23 General Rule**: monetary/foreign-currency field decimal precision is calculated by **truncation (cutting)**, not a universally selectable midpoint rounding algorithm. **Compatibility finding:** Phase07 Step06 introduced `InvoiceRoundingPolicy` with `MidpointRounding`. That is a *draft-only preview*, and **MUST be replaced/overridden with source-grounded truncation before any fiscally authoritative totals or external submission**. Step11 Monetary and Tax Rules is the required correction gate, with explicit regression tests for midpoint 0.5, negative values if permitted, and cross-line totals. Do not use Step06 preview for submission.
- Both SDK generations `0.0.x` and `2.0.x` retained as distinct source code/NuGet evidence, per [mandatory reference catalogue](../references/tax-authority-sdk-catalog.md). Regulatory requirements are sourced to V7.9; SDK DTO conventions are not a substitute.

### Delivered implementation

- New `OfficialV79LineCatalogRuleFactory` implements five source-labeled `IInvoiceRule` contracts: `invoice.lines.presence`, `invoice.lines.catalog`, `invoice.lines.quantity`, `invoice.lines.unit` and `invoice.lines.pattern`.
- `InvoiceRuleContext` gains optional immutable, read-only copy of `IEnumerable<InvoiceLineSnapshot>` as `CatalogLines`. Existing constructor signatures remain compatible (new trailing optional argument); source snapshots are not resolved via live Catalog database queries.
- **Presence/integrity:** requires nonempty line snapshots on non-cancellation; exact line ID/order/one-based positions match `LineIds`; rejects missing/mismatched snapshots and duplicate IDs; cancellation body is **not** assumed valid until separate lifecycle/reference-field contract is established.
- **Catalog:** Phase06 V1 `InvoiceCatalogHandoffPolicy.IsComplete` plus customer ownership, item kind enum, release documents/editions, valid SHA256 syntax; 13 ASCII-digit `sstid`; mapping validity is checked at **invoice's issue UTC date**, not merely at original capture timestamp; capture time cannot be after injected evaluation time; official description optional with max 400 and no control chars.
- **Unit:** Phase06 internal official-unit code required, 1..8 ASCII digits, source document `RC_UMGS.ST` required; no claim of current directory membership from a label or SHA-256 alone.
- **Quantity:** positive, max 18 integer digits, at most 8 decimal places; quantity exactly 1 for Type1 airline(6) and waybill(8). Type1 commodity securities(11) and chain sale(14) have exactly one line.
- Findings use localized stable keys and JSON field paths `body[index].sstid/.sstt/.am/.mu`; deterministic engine; zero live lookups, no state mutations, `IsSubmissionAuthorized=false`.

### Known limitations / fail-closed gates

- Captured SHA-256/edition is evidence of **which release was used**, not evidence that the external authority's live identifier database still recognizes the entry or that the unit release is current. These checks require a verified published reference snapshot or official lookup with edition/effective scope before fiscal-ready submission.
- Phase06 `Vat=Unresolved` remains unresolved, **not zero VAT**. Classification of official goods/service type, allowed exchange/transport/chain whitelist, stock exchange notice and declaration alignment, unit-to-item membership and latest VAT resolution need more external verified facts or rule-set evidence. No false final acceptance is emitted.
- Source-effective validity is checked from Phase06 mapping interval and the **invoice issue timestamp**. Trusted snapshot currency and external directory applicability are enforced by future Step12 release/version and Step10 application-layer source-eligibility checks; domain identifiers alone do not grant authorization.
- Special correction/cancellation/return subject bodies and reference line constraints belong to Steps14–16. Step10 does not implement live issuance/signing or alter tenant authorization.
- **Step06 source discrepancy** noted above is a release blocker to be resolved at Step11, not silently treated as compliance.

### Changes, tests and environment

Changed:
- `src/ArginTaxPayer.Domain/Invoices/InvoiceLineCatalogRules.cs` (new)
- `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleEngine.cs` (optional immutable CatalogLines)
- `tests/ArginTaxPayer.Tests/Phase07LineCatalogRulesTests.cs` (new)
- This single master phase document.

New tests: eligible V1 handoff, missing/empty/foreign/stale lines, customer isolation, invalid 13-digit goods/service code, expired mapping at issue date, source fingerprint validation, unresolved unit code, positive quantity with 8-place precision, quantity 1 constraints on Type1 patterns 6/8, one-line constraints on Type1 patterns 11/14, snapshot isolation and deterministic replay.

**Operator macOS:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07LineCatalogRulesTests|FullyQualifiedName~Phase07InvoiceLinesTests|FullyQualifiedName~Phase06InvoiceHandoffTests|FullyQualifiedName~Phase07TypePatternRulesTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Database:** no schema changes or migrations. **Ubuntu:** not required for domain-only change; full dual-platform acceptance at fixed Step23. **Owner acceptance:** pending.

## Step 11 — Monetary and Tax Rules — Completion Record (2026-10-17)

**Status:** Implemented in source-controlled branch, macOS execution and owner approval pending. **Step10:** owner-approved 2026-10-17; independent macOS result unavailable.

### Normative sources and correction of Step06

Authoritative owner-uploaded `RC_IITP.IS V7.9` (Tir 1405):
- **p23, General Rule 1:** Currency/IRR numbers MUST be calculated at the specified decimal precision using **truncation/cutting**, not midpoint rounding. This corrects the Step06 draft-only assumption that `MidpointRounding.ToEven` and `AwayFromZero` were selectable fiscal strategies. Implement `decimal.Truncate` at whole-rial boundaries and reject legacy midpoint modes. For negative values the official permissible domains still apply: discount/tax/ordinary monetary amounts cannot be negative; no general signed-amount relaxation is inferred.
- **Table34 p53–54:** positive unit price `fee` and its field-specific precision (18 integral digits, up to 8 decimal places); **Table31 p51:** quantity precision max 18/8.
- **Tables40–42 pp58–60:** ordinary `prdis = am × fee` (truncate to whole rial), `0 ≤ dis ≤ prdis`, `adis = prdis − dis`. Formula differs for gold, foreign exchange and airline ticket patterns; this step does **not** implement the special cases.
- **Tables43–44 pp61–63:** VAT percentage `vra` allows up to 3 integral digits and 2 decimal places; ordinary `vam = trunc(adis × vra / 100)`. The *correct* rate depends on active law, official goods/service code, taxpayer status and pattern; a caller-provided 0% does **not** establish legal exemption. Special gold, currency and fuel VAT formulae are not implemented. VAT rate for correction/return/cancellation must be retained from referenced invoice, to be enforced with predecessor data in fixed Steps14–16.
- **Tables15–20 pp39–42:** header totals `tprdis`, `tdis`, `tadis`, `tvam`, `todam`, `tbill` are exact sums of line monetary values for supported ordinary pattern; IRR resulting amounts are whole numbers, maximum 18 integral digits. Special export/grand-total rules are not approximated.
- SDKs `0.0.x` and `2.0.32` remain independently catalogued in [official Tax Authority/SDK source register](../references/tax-authority-sdk-catalog.md), not treated as normative tax policy.

### Changes

1. **Correct Step06 financial preview without breaking basic caller API:** `InvoiceRoundingPolicy(edition)` supports the exact `RC_IITP.IS_V7.9` edition and truncates whole-rial results. Legacy midpoint constructor throws `NotSupportedException` for `ToEven`, `AwayFromZero`, etc.; `ToZero` remains accepted for backwards source compatibility. Unsupported precision edition throws on evaluation. This replaces the prior discretionary rounding. `InvoiceFinancialCalculator` enforces quantity/price precision, money width, rate precision, zero-or-positive monetary domains, exact line arithmetic and checked sum overflow.
2. **Monetary payload projections:** new immutable `InvoiceClaimedLineAmounts` and `InvoiceClaimedTotals`; `InvoiceRuleContext` gains optional snapshots `MonetaryInputs`, `ClaimedAmounts`, `ClaimedTotals` without changing earlier positional constructor inputs or previous rule modules.
3. **New `OfficialV79MonetaryRuleFactory`**, four `IInvoiceRule` keys: `invoice.money.inputs` checks precise numeric inputs, scoped line identifiers/quantities; `invoice.money.lines` compares all claimed line monetary fields with recomputed amounts; `invoice.money.header` checks each of six claimed header totals with precise field-path findings; `invoice.money.tax_resolution` emits an error for missing VAT classification rather than inventing 0%.
4. **Fail closed:** Nonordinary patterns (gold, currency sale, export etc.) and cancellation are rejected as unimplemented; a clean result for Type1/Pattern1 stays `IsSubmissionAuthorized=false`. User-supplied VAT numeric values are provisional and **do not constitute signed verified legal rule resolution**. Official, versioned VAT classification is Step12 rule-set/source verification and subsequently issuance gates.
5. The engine continues to return stable localization message keys without leaking customer/taxpayer identifiers, and is pure: no database migration, HTTP requests, SDK calls, signing, payment, workflow state transitions or production changes.

### Tests and acceptance

- Adjusted older `Phase07InvoiceFinancialTests` to the official truncation rule, including rejection of nontruncating midpoint policies and unsupported editions, and width enforcement.
- New `Phase07MonetaryTaxRulesTests`: truncation at both prediscount and VAT calculation, exact header and line reconciliation, unresolved vs explicit-zero VAT, precision invalid, stale quantity relative to immutable catalog, unsupported special patterns, deterministic repeat results and nonmutating context.
- **Observed testing status:** tests written and committed, not executed by assistant against a macOS/.NET runtime; do not mark compile/test acceptance green until operator provides results.
- **Out of scope / release blockers:** legally effective official VAT rate lookup, reference VAT locks, exceptions for gold/exchange/export/fuel, and final source-version applicability; these do not silently default. Step12 must select certified versioned rules before fiscal readiness.

**macOS operator commands:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07MonetaryTaxRulesTests|FullyQualifiedName~Phase07InvoiceFinancialTests|FullyQualifiedName~Phase07LineCatalogRulesTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Database:** no migrations; **Ubuntu:** no new runtime service; fixed Step23 final dual-platform tests still required. **Owner acceptance:** pending.

## Step 12 — Rule-set Versioning — Completion Record (2026-10-17)

**Status:** Code, tests, and source governance committed. Owner acceptance and local macOS build/regression evidence pending. Step11 expressly owner-approved 2026-10-17; no independent test logs attached.

### Primary source and governance

The owner-supplied authority document **RC_IITP.IS V7.9**, Tir 1405, is the normative *content* reference for rules: Table1 p14 (field applicability), Table9 p31 (type/pattern), Table10 p32 (subject), Tables29–32 pp50–52 (lines), p23 (truncation), Tables40–44 pp58–63 (monetary/VAT). The authority PDF's *publication month does not prove the effective date of any rule or whether V7.9 is presently the operative edition*. Thus the official V7.9 built-in factory deliberately creates a **provisional, draft-only** bundle using the operator's explicitly provided comparison window. It neither makes up an official start date nor presents the owner-supplied document as currently verified for submission.

Mandatory reference catalogue: [tax-authority-sdk-catalog.md](../references/tax-authority-sdk-catalog.md). Keep source and binary SDK generations 0.0.x and 2.0.x separately traceable; they do not override normative tax rules.

### Architecture and contracts

- New `InvoiceRuleSetSource.cs`: `RuleSetPrimarySource` stores official document ID, edition, precise citation and SHA-256. `RuleSetValidity` stores half-open UTC `[start, end)` applicability with explicit `DateVerified` and separate `DateEvidence`. Missing official effective-date evidence is not silently completed from publication month.
- New `InvoiceRuleSetRegistry.cs`: `InvoiceRuleBundle` freezes scope, exact engine edition, source, validity window, explicit coverage keys and `CoverageReviewed` assertion. `InvoiceRuleSetRegistry` snapshots the bundle list and rejects duplicate edition/scope and overlapping time windows for the same scope even across different editions. Adjacent windows are permitted.
- `Resolve(scope, edition, issueUtc)` selects **only** the requested edition on the requested UTC issue date. No "latest" fallback; unknown edition, outside-window, or unverified-date requests return distinct status outcomes. `ReplayDraft` evaluates the exact selected rule engine deterministically, with historical context pinned to that edition.
- `CheckEvidenceReadiness` enforces explicit date-evidence and declared rule-coverage review before returning a selected bundle. Such claims are governance input, not independently validated governmental recognition. **All lookup and engine result types continue to expose `IsSubmissionAuthorized=false`**. Present Step12 bundles do not establish full legal coverage or complete the missing specialized validators in Steps08–11. Actual issuer/rate/certificate/authority acceptance is out of scope. No false-ready output.
- `OfficialV79RuleSet.Draft(engine, fromUtc, toUtc, keys)` pins SHA256 to the owner-supplied V7.9 PDF `fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be` and labels the window **provisional**; engine and source edition must agree. Provisional windows are for controlled test replay only.
- New `InvoiceRuleMessageCatalog` registers explicit `fa-IR` and `en-US` localized strings by stable `MessageKey`, fails on duplicate keys and unsupported locales, and never substitutes user data into rule messages. Missing translations return `false`, with no silent translation fallback.

### Boundaries and follow-up gates

- Source scope is a Domain-only rule/version registry: not a database-persisted version store, not a complete production rule activation pipeline and not official date attestation. Later workflow/API must bind approved rule-set IDs and fingerprints to each invoice revision; cross-tenant authorization and immutable audits remain mandatory.
- **The first official applicability notice per tax rule is still required** before any date-window may be treated as legally verified. Registration with `DateVerified=true` is merely an assertion supported by a separately supplied source; implement multi-person review/approval in application/workflow phases. Never infer fiscal validity from a clean validation result.
- Partial monetary and pattern-specific gates (gold/exchange/export/fuel/chain and authoritative VAT classifications) remain unresolved and must prevent actual issuance. No SDK signing, tax authority calls, transport, business workflow, EF migration or PostgreSQL changes.

### Files and tests

- `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleSetSource.cs` (new).
- `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleSetRegistry.cs` (new).
- `src/ArginTaxPayer.Domain/Invoices/InvoiceRuleMessageCatalog.cs` (new).
- `tests/ArginTaxPayer.Tests/Phase07RuleSetVersioningTests.cs` (new): provisional V7.9 registration, explicit edition selection, half-open boundaries, overlapping and duplicate rejection, missing edition, date provenance, frozen collections, incomplete coverage and inability to authorize submission.
- `tests/ArginTaxPayer.Tests/Phase07LocalizedRuleMessagesTests.cs` (new): Persian/English lookup, unavailable messages and unsupported/duplicate locales.
- This master phase file only; no step renumbering.

**macOS operator verification:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07RuleSetVersioningTests|FullyQualifiedName~Phase07LocalizedRuleMessagesTests|FullyQualifiedName~Phase07RuleEngineContractsTests|FullyQualifiedName~Phase07MonetaryTaxRulesTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Migration/database:** none. **Ubuntu-specific execution:** deferred for Domain-only change; final macOS/Ubuntu smoke acceptance remains fixed Step23. **Owner approval:** pending.

## Step 13 — Invoice Lifecycle — Completion Record (2026-10-17)

**Status:** Domain implementation and xUnit tests committed; owner approval and operator macOS build/test pending. **Step12:** approved by owner 2026-10-17, independent operator test logs not attached.

### Regulatory and architecture provenance

Primary source: owner-provided Tax Authority **RC_IITP.IS V7.9** (Tir 1405), **pp14–18** (types/patterns/subjects), **Table10 p32** (original, correction, cancellation, return), **Table8 p30** (reference invoice ID rules). These official fields describe **fiscal subject and conditions**, not an internal software editing workflow. The internal states `Draft`, `Validated`, `Finalized` are **ArginTaxPayer architecture**, *not tax-authority outcomes*. No claim of Tax Authority acceptance, transmission or issuance follows from these transitions.

### Domain implementation

- Added `InvoiceLifecycle.cs` with `InvoiceLifecycleState`: `Draft=1`, `Validated=2`, `Finalized=3`; independent `TaxAuthorityProcessingState` with `NotSubmitted`, `Pending`, `Accepted`, `Rejected`, `Unknown` for future Phase10/11 integration. **Only NotSubmitted is initialized here**; no API allows pretending an authority response occurred.
- `InvoiceValidationReceipt` pins invoice ID, pre-validation revision, named rule-set edition and 64-hex rule-set fingerprint, UTC evaluation time and explicit error flag. `MarkLocallyValidated` requires a clean matching receipt and Draft state; increments revision and moves to Validated, without claiming fiscal submission readiness or full official legal validity. A receipt is an *input assertion*, not a cryptographic capability and must be created by a trusted application service.
- `InvoiceInternalFinalizationApproval` records invoice ID, validated revision, approving user identity, workflow reference and UTC approval timestamp. `FinalizeInternally` requires the current Validated revision, current receipt bound to the immediately preceding revision, and approval not older than validation. Internal finalization increments revision and **locks all draft mutation methods** including buyer/seller/issue-date, subject, add/update/delete line, and financial preview; this is an internal immutable record, **not an already transmitted fiscal invoice**.
- `ReopenValidatedDraft` permits `Validated → Draft`, discards the previously captured validation, increments revision and allows further draft edits. `Finalized` is terminal in this domain aggregate; future corrections, cancellations and returns must generate new referencing invoices rather than mutate this record.
- The separate `InvoiceSubject` enum `Original/Correction/Cancellation/SalesReturn` remains unchanged. Do not identify a canceled source invoice with local Finalized state or assume a sent invoice is accepted.
- `InvoiceLifecyclePolicy.CanTransition` documents legal topology of *internal* state machine: `Draft→Validated`, `Validated→Draft`, `Validated→Finalized`, all others rejected.

### Security and completeness gates

- The receipt and approval types validate format and revision but **cannot verify their creators' permissions or prove rule-set/legal completeness**. Before production, a trusted application service must authenticate the actor, verify full authorization to customer/taxpayer and an independently approved rule fingerprint/edition, ensure sufficient official rule coverage and trusted VAT rate resolution, and issue the receipt and approval through a protected workflow. Untrusted caller-supplied receipts/approvals MUST NOT be accepted by production API.
- **Current partial V7.9 validators do not establish full fiscal readiness**, especially special patterns and legal effective dates. Therefore `FinalizeInternally` is categorically *local-only*; it must never cause signing, tax-ID assignment, queueing or sending in future until regulatory and security gates succeed.
- Transition persistence and atomic revision-based optimistic concurrency / idempotency are fixed Step17/Step18 obligations; these Domain transitions do not perform database writes.
- No database migration, no SDK transport, no real Tax Authority contact, no HTTP endpoint, no UI changes.

### Changed files / tests

- New `src/ArginTaxPayer.Domain/Invoices/InvoiceLifecycle.cs`.
- Modified `src/ArginTaxPayer.Domain/Invoices/InvoiceAggregate.cs`: lifecycle state, receipt/approval data, internal transitions.
- New `tests/ArginTaxPayer.Tests/Phase07InvoiceLifecycleTests.cs`: nine tests for Draft defaults, revision pinning, failed/forged/stale receipts, reopen invalidation, frozen finalized aggregate, incorrect approval, reapplication rejection, authority state isolation, invalid security-format evidence.
- This single Phase07 master document only.

**macOS operator check:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07InvoiceLifecycleTests|FullyQualifiedName~Phase07InvoiceAggregateTests|FullyQualifiedName~Phase07InvoiceSubjectsTests|FullyQualifiedName~Phase07RuleSetVersioningTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```
**Ubuntu:** no separate platform run required for Domain-only Step13; full macOS/Ubuntu acceptance Step23. **Owner acceptance:** pending.

## Step 14 — Correction Reference — Completion Record (2026-10-17)

**Status:** Implemented, source controlled; operator macOS compilation/regression tests and owner acceptance pending. Owner confirmed completion of Step13 on 2026-10-17; no independent test evidence provided.

### Primary authority evidence and essential restrictions

Owner-supplied **RC_IITP.IS V7.9**, Tir 1405:
- **p15 §5 and §5.2**: a correction is a *new* invoice with the same original invoice type and pattern, referencing the previous invoice's tax ID; buyer identity information, goods/services identifier (`sstid`), and VAT rate are noneditable. If any must change, cancel the previous invoice and issue a new original. The existing, finalized source aggregate must not be modified.
- **p16 §5.2 item2**: quantity increases may be corrected or separately invoiced; quantity decreases via correction only when correcting incorrectly recorded original quantity, otherwise sales-return is the normal alternate. A correction cannot add a new official goods/service identifier.
- **p16 §5.2 item3**: when previous document itself is correction/return, the reference must have an approved/system-approved/no-response-needed result and must not already have another noncanceled correction or return.
- **p30 Table8 items1–8**: `irtaxid` required for correction/return/cancellation; reference must exist, same issuer workspace, correction/return conflicting simultaneous reference prohibited, reference cannot be cancelled, correction issue time must not precede reference. Official allowed date windows remain externally published and are **not inferred**.
- **p32 Table10 item7**: for commodity-linked securities pattern 11, an original invoice with matching/approved sale notice cannot be corrected; only an unapproved notice allows correction. Missing notice status is not silently accepted.
- **p32 Table10 item4**: buyer fields are outside transmitted pattern on referring invoices; the internal buyer fingerprint comparison protects identity lineage but does **not** claim buyer fields must be sent in a correction.
- **p61 Table43 item8**: VAT rate is unchanged in referenced corrections/returns/cancellations; no universal rate is invented.

### Delivered domain contracts

- New `InvoiceCorrectionReference.cs`: immutable `CorrectionPredecessor` with fiscal Tax ID, local invoice ID, issuer scope, registered/canceled state, issue timestamp, subject and response status, immutable list of official goods/service identifiers with quantity and VAT rate, lineage ancestor IDs, and externally obtained active sibling/conflict flags. `CorrectionDraftEvidence` carries a distinct new invoice ID, matching tenant scope, issue UTC, type/pattern, subject/link, buyer identity fingerprint, proposed line identifiers/quantities/VAT, and an asserted original quantity-entry mistake reason for a decrease.
- New `InvoiceCorrectionRules.cs`: deterministic source-grounded validation of exact reference matching, existence, no cancelled source, same authorized customer/taxpayer, no self/cyclic reference, chronological order, type/pattern and buyer identity unchanged, valid predecessor response and active sibling conflicts, no new/removed/duplicate goods/service identifiers, unchanged VAT, reduction intent, and verified stock-exchange sale-notice status for pattern 11.
- No reference is resolved by untrusted user input alone. A 22-character `irtaxid` is only **structural** identity; it does not establish existence or acceptance in the fiscal portal. The application must obtain facts from an authenticated and scoped reference resolver. User text or personal IDs are not interpolated into output error keys.
- This step models one correction against an authoritative immutable predecessor without changing the previous `InvoiceAggregate` or its `Finalized` state. Source is unchanged; correction is a separate Draft with `InvoiceSubject.Correction`.

### Open gates / deliberately not implemented

- **Not yet a legal issuance decision:** checks are provisional constraints over externally supplied verified facts, not a production reference verification service or legally authorized submission. In particular, `ReductionIsCorrectionOfOriginalMistake=true` is a declared reason, not proof of the historic mistake; documentary/evidence workflow remains necessary.
- Certain pattern-specific fields, exact legal correction time windows, buyer identity based on authoritative tax registry, competing document statuses and legally active VAT revision require authoritative lookup. Unsupported or missing external evidence must block submission at the application boundary.
- Duplicate `sstid` lines in predecessor are currently conservatively rejected rather than silently merged; production implementation must use a verified identity/multiset strategy before accepting such references.
- Neither a no-error result nor an internally Finalized correction should be interpreted as the tax-authority accepting the fiscal correction. Lifecycle, correction, cancellation and return remain separate concepts. Concurrent reference uniqueness/idempotency will be enforced in Step17 and persisted in Step18.
- No API, persistence/migration, tax submission, SDK calls, workstation identity access, or financial-side effects added. Dedicated cancellation and sales-return state machines remain fixed Step15/16.

### Changed files

- `src/ArginTaxPayer.Domain/Invoices/InvoiceCorrectionReference.cs` — new immutable registered-predecessor and correction draft evidence.
- `src/ArginTaxPayer.Domain/Invoices/InvoiceCorrectionRules.cs` — new correction-specific validation.
- `tests/ArginTaxPayer.Tests/Phase07CorrectionInvoiceTests.cs` — new positive/negative correction domain tests (12 scenarios).
- `docs/phases/phase-07-invoice-domain-rule-engine.md` — this completion record, without renumbering prior steps.

### macOS operator test

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07CorrectionInvoiceTests|FullyQualifiedName~Phase07InvoiceSubjectsTests|FullyQualifiedName~Phase07InvoiceLifecycleTests|FullyQualifiedName~Phase07TypePatternRulesTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Database/EF migrations:** none. **Ubuntu:** deferred for this Domain-only step; final platform acceptance Step23 remains fixed. **Owner acceptance:** pending.

## Step 15 — Cancellation Reference — Completion Record (2026-10-17)

**Implementation:** Source and xUnit tests committed; owner acceptance and actual macOS testing pending. **Step14:** expressly approved 2026-10-17; no independent local test logs attached.

### Authoritative legal basis (owner-uploaded V7.9 PDF)

Primary normative reference **RC_IITP.IS V7.9**, Tir 1405:
- **§5 introductory notes, p15:** each tax invoice may serve as the reference for at most one cancellation invoice.
- **§5.3, pp16–17:** cancellation creates a **new** document for changing buyer information, complete return, documented nonoccurrence of the sale or an incorrect transaction date; a previously canceled source cannot be used again. The cancellation references a prior **original, correction, or return** (not a cancellation). Type/pattern, buyer fields and all item lines are obtained by the authority from the reference and are **not repeated in cancellation JSON**.
- **§5.3, p17:** an original with an already approved referring correction/return is effectively canceled; dependent pending/unapproved correction/return may be canceled as a consequence of accepted cancellation. Referring documents pending reaction or rejected do **not by themselves** automatically block a cancellation of the original. These are read-only preflight rules; they do not pretend to change live statuses before tax authority acceptance.
- **Table8, p30:** mandatory `irtaxid`, registered reference, matching issuer workspace, cancellation cannot itself be referenced, no earlier than source issue date; further officially notified issuance deadline windows must be checked later and are **not invented** here.
- **Table10, p32:** for cancellation, `inty` and `inp` and all buyer fields are **outside pattern**; for referencing utility bills, subscriber/bill ID is outside pattern. For export cancellation, issue date must be on/after the recorded customs declaration issue/registration date (source evidence necessary).

### Delivered Domain contracts

- New `InvoiceCancellationReference.cs` contains immutable, externally-provided `CancellationPredecessor` with local and fiscal IDs, issuer/customer scope, existing-registration/cancellation status, source issue time, original classification/subject, already-issued cancellation-request flag, ancestor chain, related correction/return statuses and optional export customs declaration date. Inputs must come from an authenticated, tenant-scoped and fresh authority/workspace resolver (not user HTTP payload).
- `CancellationDraftEvidence` carries new invoice identity, subject+reference, date, classification, enum for the four documented cancellation reasons, an independent evidence reference, and a `CancellationPayloadProjection` describing outbound field presence.
- New `OfficialV79CancellationRules.Evaluate` checks correct cancellation subject, exact fiscal reference and optional local ID match, registered/not cancelled/noncancellation source, no existing cancellation, no cycles, same issuer/customer scope, date not before reference, unchanged internal type/pattern, approved related documents blocking reuse of an effectively canceled original, omission of payload-only fields, stated documentary reason, and export customs chronology or missing export-date evidence. Findings use stable localized keys and field paths with no personal data in message strings.
- A correction/return that is pending reaction or rejected is distinguished from approved/system-approved/no-response-required. This preflight validates reference eligibility and **does not update** the source or child invoice state. The live Tax Authority response determines any subsequent cancellation effects.
- Enforced no-false-success rule: no call sends to portal; no successful `Evaluate` can grant issuance/acceptance. Further status checks, actual duplicate-operation transaction locks and response reconciliation are required before production.

### Limitations and acceptance gates

- `HasExistingCancellationRequest` is a caller-provided fact, **not an atomic uniqueness constraint**. Step17 will implement concurrency and idempotency protection, Step18 persistence/unique indexes.
- External signed proof / authoritative resolution, official date windows, partial-reaction concurrency, status effects on dependent invoices, customs registry identity and tax scope cannot be established merely from 22-character fiscal ID or a true/false flag. Until resolved, Domain rules are **provisional**, not a legal issuance decision.
- `CancellationReason` identifies an asserted policy reason, not verified legal documentary sufficiency. `ReasonEvidenceReference` records workflow evidence ID; authorized verification of documents is a future Application obligation.
- The source `InvoiceAggregate` is **not edited**, reopened or marked accepted/canceled locally in this step. A cancellation request is a separate referencing draft; `TaxAuthorityProcessingState` from Step13 remains separate.
- No schema migrations, no PostgreSQL changes, no API routes, no crypto, SDK submission or payment effects.

### Changed files

- `src/ArginTaxPayer.Domain/Invoices/InvoiceCancellationReference.cs` — immutable predecessor, related documents, cancellation reasons and JSON presence projection.
- `src/ArginTaxPayer.Domain/Invoices/InvoiceCancellationRules.cs` — cancellation preflight validation.
- `tests/ArginTaxPayer.Tests/Phase07CancellationInvoiceTests.cs` — thirteen positive/negative scenario tests including duplicate attempts, mis-scoped/circular references, related-document reactions, outside-pattern fields, utility bill, export customs date and historical replay.
- `docs/phases/phase-07-invoice-domain-rule-engine.md` — this entry (no step renumbering).

**macOS operator verification:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07CancellationInvoiceTests|FullyQualifiedName~Phase07CorrectionInvoiceTests|FullyQualifiedName~Phase07InvoiceLifecycleTests|FullyQualifiedName~Phase07InvoiceSubjectsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Database migration:** none. **Ubuntu-only environment validation:** not necessary for Domain-only change. Final dual-platform test remains fixed Step23. **Owner approval:** pending.

## Step 16 — Sales-return reference — Completion Record (2026-10-17)

**State:** Code, tests, master-plan update committed; owner approval and operator macOS test logs pending. Step15 explicitly owner-approved 2026-10-17; independent macOS logs not available.

### Normative source traceability and representation

Owner-provided Iranian Tax Authority **RC_IITP.IS V7.9** (Tir 1405):
- **§5.4 pp17–18:** sales return is a *separate referring electronic invoice*, using the same original type/pattern and a reference tax identifier, containing **goods/services sold minus returned quantities**. Do **not** create a positive "returned quantity" body and claim it is the official return document. Buyer identity, VAT rate and monetary commercial terms of the goods/service relative to original **may not change**; quantity/line count decreases. Returning **all goods/services** requires a cancellation invoice, not a sales-return invoice.
- **§5.2 p16:** for referring against a previously issued correction/return, predecessor must be approved/system-approved/no-reaction-needed and must not have another noncanceled correction/return.
- **§5 introductory notes p15, Table8 p30:** reference must be registered and valid, issuer workspace the same, reference tax ID verified, no referencing a cancellation, issue time not earlier than predecessor, official time-window checks are external and may not be invented. Simultaneous correction and sales return against same reference is forbidden by Table8.
- **Table10 p32:** Type1 export pattern7, waybill pattern8, commodity-securities pattern11 disallow SalesReturn subject. All buyer fields are outside the transmitted pattern in referring documents. Step16 uses an *internal* buyer fingerprint comparison and does not insert the identity in outward payload.

### Delivered contracts

- New `src/ArginTaxPayer.Domain/Invoices/InvoiceSalesReturnReference.cs`: immutable `SalesReturnPredecessor` (registered reference, issuer/customer scope, original type/pattern, subject and fiscal response, issue time, buyer identity fingerprint, ancestor references, externally verified active-sibling conflict and immutable effective **remaining quantity** lines). For chained returns, authoritative predecessor lines must represent the last officially accepted remaining quantity—not guessed original quantities or a mutable catalog row.
- `SalesReturnLineTerms` stores `sstid`, **remaining** `am`, VAT `vra`, unit price and unit-discount monetary invariants in IRR; no negative quantity, rate outside 0–999.99, or unverified duplicate sstid grouping. Monetary figures are compared as invariant commercial unit terms, **not mistakenly compared as identical line totals across changed quantities**.
- `SalesReturnDraftEvidence` projects a separate referencing draft with its own ID, UTC issue date, original classification, subject+reference, internal buyer fingerprint and immutable proposed *remaining quantities*.
- New `src/ArginTaxPayer.Domain/Invoices/InvoiceSalesReturnRules.cs`: deterministic `OfficialV79SalesReturnRules.Evaluate` emits stable error message keys for wrong subject/reference, missing/unregistered/cancelled predecessor, foreign customer/taxpayer, cycles, chronology, changed type/pattern or buyer identity, unsupported Type1 export/waybill/securities patterns, active related correction/return, and ineligible chained reference response; validates original goods-service membership, unchanged VAT/price/unit discount, non-increasing per-line remaining quantity, **at least one decreased quantity**, and **not all quantities reduced to zero** (cancellation mandatory).
- An omitted reference line may represent a fully returned line only if some other eligible positive quantity remains. The result never authorizes fiscal submission. No mutation of original invoice, fiscal response, inventory, payment or legal VAT evidence.
- Step16 is a Domain-only source rule; **no EF migration, database query, API, runtime side effects, authority calls or transaction locks**.

### Known limitations and mandatory pre-submission gates

- `SalesReturnPredecessor` and its quantity/response/conflict evidence are supplied by the caller and **must come from authenticated, tenant-scoped, fresh authority/workspace sources**, never raw HTTP user input. The Domain cannot certify current authority response status or whether a fiscal ID actually exists.
- For cumulative returns, read the authoritative **latest accepted predecessor** and enforce one active correction/return per reference with atomic transaction locks (fixed Step17) and durable unique constraints (Step18), not only the local arithmetic preflight. Reference chain and eligibility need authoritative reconciliation; no double-return guarantee without persistence.
- Commercial invariants are a minimal projection (`UnitPriceRial`, `UnitDiscountRial` and `VatRatePercent`). Complete pattern-specific financial fields, amount and source-fiscal-unit proof, official time windows, and any formally allowed specialized exceptions are mandatory before actual issuance. The Rule Engine still returns no fiscal send authorization; production signing and submission belong to later global phases.
- Some real invoices may repeat `sstid` across multiple independent rows; this Step16 model rejects ambiguous duplicates instead of silently merging monetary bases. A verified stable line identity and duplicate policy must be introduced before supporting those cases.
- The draft and predecessor snapshot models support a zero remaining quantity only to represent a fully removed individual line; outward DTO serialization must omit such rows according to the official schema. This has not been implemented here.

### Changed files

- `src/ArginTaxPayer.Domain/Invoices/InvoiceSalesReturnReference.cs` — new immutable predecessor and draft line evidence.
- `src/ArginTaxPayer.Domain/Invoices/InvoiceSalesReturnRules.cs` — new source-grounded sales-return reference validation.
- `tests/ArginTaxPayer.Tests/Phase07SalesReturnInvoiceTests.cs` — 13 new positive/negative test cases including partial return, unchanged/increased/full return, immutable commercial terms, same scope, reference response, subject/pattern gates, chained remaining quantity, cycle handling, and deterministic snapshots.
- This single Phase07 master plan; no step renumbering.

**Operator macOS acceptance commands:**
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/07-invoice-domain-rule-engine
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07SalesReturnInvoiceTests|FullyQualifiedName~Phase07CorrectionInvoiceTests|FullyQualifiedName~Phase07CancellationInvoiceTests|FullyQualifiedName~Phase07InvoiceSubjectsTests" --no-restore
dotnet test ArginTaxPayer.sln --no-restore
git status --short
```

**Owner approval:** pending. **macOS tests:** written but not run on operator machine. **Ubuntu:** no separate requirement for domain-only step. **Phase07 Step17 next:** concurrency, optimistic revisions, idempotency and cross-request reference uniqueness.
