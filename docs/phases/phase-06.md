# ArginTaxPayer — Phase 06: Goods and Services

**Status:** Steps01–07 owner-approved (Step07 reported successful tests on 2026-10-16; raw logs not attached). Step08 reference activation/audit implementation committed; acceptance and test-DB migration pending. Steps09–16 not started.
**Branch:** `phase/06-goods-services`
**Baseline:** `main` after Phase05 merge `6a3de74692403029b68c2061251601752eebd518`.
**Roadmap:** `docs/roadmap/18-phases.md`, Phase 06.
**Policy:** This is the single authoritative evolving phase plan. Step count and scope are frozen pending a dated owner-approved amendment.

## Objective and Architecture

Build a versioned official goods/services reference catalog, including tax identifiers, official unit codes and VAT classification; customer-owned product and service catalogs; audited mappings; searchable scoped APIs; and an RTL management UI. Layers are Domain, Application, Infrastructure EF Core/PostgreSQL/import, ASP.NET Core API and Next.js UI.

Preserve Phase03 identity, Phase04 customer/partner ownership and Phase05 parties. Customer ownership governs authorization, not legacy taxpayer-level grants. Reference data is shared read-only to authorized tenants; tenant catalogs are private. Keep official identifiers separate from internal product IDs. Require provenance, effective dates and source-version attribution. Do not assume a universal VAT rate. This phase does not include live authority connectivity, invoice issuance, signing, billing or financial postings.

## Fixed 16-Step Plan

| Step | Title | Deliverable | Acceptance | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Architecture and Contracts | Domain model, ADR, API, permissions and phase boundaries | Contract review and no overlap with prior phases | Phase05 | Owner-approved 2026-10-16; ArginAccounting compatibility amendment |
| 02 | Persistence and Migrations | Reference tables, customer catalog and additive EF migration | Test database only; constraint and rollback review | 01 | Owner-approved 2026-10-16; test logs not independently captured |
| 03 | Official Identifier Model | Official goods/service identifier, origin and verification | Reject invalid/duplicate identifiers and unsupported official claims | 02 | Implemented; operator tests and owner signoff pending |
| 04 | Official Unit Codes | Official unit-code reference and internal-unit mapping | Reject unknown units and incompatible mappings | 03 | Pending |
| 05 | VAT Classification | Date- and source-dependent VAT classification, rates and exemptions | Do not assume fixed rates; test boundary dates | 03–04 | Owner-approved 2026-10-16; tests/migration operator-reported successful |
| 06 | Versioned Reference Catalog | Source versions, validity intervals, snapshots and immutable history | Recover prior versions and control overlap | 03–05 | Owner approved 2026-10-16; tests/migration reported successful |
| 07 | Reference Import Pipeline | Official-file staging, preview and validation | Idempotent repeated import and reported errors | 06 | Owner-approved 2026-10-16; focused tests operator-reported successful |
| 08 | Reference Activation and Audit | Controlled release with audit and logical rollback | Move active version without deleting history | 07 | Implemented; tests and migration acceptance pending |
| 09 | Customer Product Domain | Customer products, status and internal identifiers | Tenant isolation and protection of references | 02–06 | Pending |
| 10 | Customer Service Domain | Customer services and type rules | CRUD, ownership and type-mismatch tests | 09 | Pending |
| 11 | Tax Identifier Mapping | Historical mapping from customer items/services to official identifiers | Reject invalid or wrong-kind mappings | 08–10 | Pending |
| 12 | Catalog API and Search | Search, filtering, pagination and concurrency support | Role, paging and stale-update tests | 09–11 | Pending |
| 13 | RTL Catalog UI | Lists, forms, selection search and localized errors | Authorized paths, validation and accessibility tests | 12 | Pending |
| 14 | Invoice Handoff Contracts | Immutable snapshot contract for Phases 07/08 without invoice issuance | Version contract and historical immutability tests | 06, 11–13 | Pending |
| 15 | Security and Regression | Isolation, race, migration and Phase03–05 regression tests | Prevent cross-customer and former-partner access | 01–14 | Pending |
| 16 | Final Acceptance and Handoff | Mac and Ubuntu aggregate tests, documentation and risk report | Evidence report, owner approval and separate merge/tag | 15 | Pending |

## Engineering and Safety Invariants

1. Use additive, reviewed migrations only. Confirm `current_database() = 'argintaxpayer_test'` before any database change. Never apply migrations automatically or to production.
2. Never invent or claim tax-authority validation. Official imports require traceable provenance and version; unknown classifications remain explicitly unresolved.
3. Historic catalog versions, mappings and future invoice snapshots are immutable and auditable. Never overwrite historical tax rates retroactively.
4. Enforce server-side authorization and database query scoping for Root, Level2 and customer users. Preserve prospective commercial ownership transfer semantics.
5. Do not commit secrets, real certificates, private keys or production dumps.
6. Record focused tests, negative cases, changed paths, commit SHA, migration impact, CI/Mac/Ubuntu evidence and explicit owner approval for each step. Do not start the next step before approval.
7. Step 16 includes final Mac regression and Ubuntu `bash scripts/ubuntu-dev.sh verify`, followed by loopback-only `up/status/down`. Run earlier Ubuntu checks only when schema, infrastructure or platform risk requires them.
8. Use `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh` and `zsh scripts/dev-down.zsh`. The runner manages the owner-approved AutoSSH tunnel at `127.0.0.1:15432`; do not create a manual plain SSH tunnel.
9. Main merge, tag, release and deployment are separate approval gates after phase acceptance.

## Step 01 — Architecture and Contracts (2026-10-16)

**Status:** Design and contract baseline recorded. No runtime functionality was implemented and no build, test, CI or database verification was claimed.

### Design decisions

1. Official reference rows are globally readable only to authenticated, authorized tenants. Import and activation are privileged, audited operations. Customer items are scoped by immutable `CustomerUserId`; do not key ownership by Party, fiscal-memory ID or Level2 partner ID.
2. `CatalogItemId` is a stable UUID. `CustomerCatalogItem.InternalCode` is not an official tax identifier. `OfficialGoodsServiceCode` is a source-versioned regulatory identifier. Never infer an official match from an item name or party identifier.
3. Reference releases are immutable once activated. Store UTC instants and official effective periods independently from import time. Use half-open `[from,to)` intervals. Missing or ambiguous classifications remain unresolved and block confirmed official mapping.
4. Plan unique `(CustomerUserId, normalized InternalCode)` for active items, unique `(releaseId, kind, officialCode)` for official entries, restrictive history foreign keys, revision-based optimistic concurrency and deterministic imports keyed by source and release checksum.
5. Level1 administers root customers under existing scope. Level2 manages only currently owned customer catalogs. Customers manage only their own catalog under approved self-service capability. Former partners lose access immediately after ownership transfer.
6. Do not expose unaudited production routes. Later API work must reuse session resolution, ownership policy and resource access checks, with stable error codes and no secret leakage.
7. Future invoice snapshots must include catalog item, kind, customer name/code, official identifier, release/source version, unit version, VAT provenance and resolved/unresolved outcome. Phase 06 does not create invoices or calculate totals.
8. Import only verifiable official datasets with provenance and checksum. Re-importing identical content is idempotent; new content creates a release. Rollback changes the active pointer with audit and does not delete prior records.

### Step 01 acceptance

- [x] Read the Phase06 roadmap, Phase04/05 compatibility rules and relevant source files.
- [x] Define entities, identifiers, version semantics, authorization matrix and API contracts.
- [x] Establish non-destructive migration handoff and provenance gating for Steps 02–08.
- [x] Define negative-path and cross-phase regression expectations.
- [ ] Review reference datasets, formats, licensing and source questions with the owner.
- [ ] Obtain owner approval to close Step 01 and authorize Step 02.

**Database/migration:** None; no SQL executed and no existing data modified.
**Owner acceptance:** Pending.

## Owner-Approved Scope Clarification — Official Units and Level1 Administration (2026-10-16)

Step 02 designs additive reference tables, idempotent seed/import status and uniqueness checks. Step 03 validates official goods/service identifiers and restricts reference-code changes to Level1. Step 04 imports the complete official measurement-unit catalog. Step 06 preserves immutable source/release versions. Steps 07–08 implement provenance-gated import and publishing. Step 12 enforces authorization on mutating APIs. Step 13 provides scoped read-only reference selection and Level1 administration UI. Step 15 verifies security and replay safety.

The owner-provided `RC_IITP.IS V7.9` specification defines invoice field `mu` as a numeric string with a maximum length of 8 and references the separate `RC_UMGS.ST` official units-of-measure catalog. `RC_IITP.IS` is not an authoritative full units table. A secondary copy, `RC_UMGS.ST_V1.4`, is dated Ordibehesht 1402 and is only an archival candidate, not a verified current release. Import all rows only after verifying the latest official source, version, row count and checksum. Do not use guessed, incomplete or silent legacy fallback data. Example labels in secondary tables: number=1627, liter=1637 and square meter=1645.

Three code namespaces remain separate: `OfficialUnitCode` for regulatory invoice units; `OfficialGoodsServiceCode` for official tax identifiers; and `CustomerCatalogItem.InternalCode` for customer-local SKU/item codes. Customer and Level2 users cannot alter official reference data. Official candidates remain `Unverified` until provenance checks succeed.

Unit seeding requires a checked-in machine-readable source with document identifier, version, issuer, source URL or verified fingerprint, SHA256 checksum, row count, normalization policy and acquisition date. Run explicit migration/seeding commands only against `argintaxpayer_test` after checking `SELECT current_database()`. No seed runs at startup. Existing valid rows and invoice snapshots must remain unchanged.

## ArginAccounting Interoperability and Step 02 Record (2026-10-16)

### Compatibility contract

- `ItemKind` uses `STOCK_ITEM`, `NON_STOCK_ITEM` and `SERVICE` with persisted values 1, 2 and 3. `OfficialGoodsServiceEntry.Kind` is a separate two-state goods/service classification; stock versus non-stock must never become a tax classification.
- Every customer item, including `SERVICE`, has a base measurement unit. Conversion uses exact positive numerator/denominator values and an explicit rounding policy.
- ArginAccounting `CompanyId` and ArginTaxPayer `CustomerUserId` are different scopes. Phase13 requires an explicit company/taxpayer-to-customer mapping; never compare these UUIDs directly.
- Financial balances, FIFO, COGS, purchase/sales prices, warehouse inventory and accounting postings are outside this phase.
- Preserve invoice-line snapshot immutability for later phases.

### Step 02 — Persistence and Migrations

**Status:** Implementation committed; local tests, safe database application and owner acceptance remain pending.

**Entities/tables:** `CatalogReferenceRelease`, `OfficialMeasurementUnit`, `OfficialGoodsServiceEntry`, `VatClassificationEntry`, `CustomerCatalogItem`, `CatalogItemUnitConversion` and `CatalogOfficialMapping`, with their corresponding tables.

**Integrity:** Unique source-document/checksum releases; unique official code per release; per-customer unique internal code; revision concurrency; explicit customer identity and base-unit foreign keys; restrictive history foreign keys; positive exact ratios; UTC effective intervals; nullable VAT rate and legal-basis metadata. Domain operations such as source verification, overlap guards, kind mapping, Level1 authorization and seeding are later-step responsibilities.

**Migration:** `20261016120000_AddPhase06CatalogFoundations` adds seven tables, indexes and PostgreSQL constraints. It does not alter Phase03–05 tables or backfill data. Its `Down` operation is destructive after inserts and must not be used on retained data without approval. Validate with `dotnet ef migrations has-pending-model-changes` before signoff.

**Safety:** Before any explicit migration, verify the database name is exactly `argintaxpayer_test`, review generated SQL and confirm migration history, tables, constraints and rollback-only probes afterward. Do not perform production database operations.

**Acceptance:** Release build and five model metadata tests pass; the test database is correct; seven catalog tables and restrictions exist; duplicate and invalid values fail; restrictive foreign keys protect history; duplicate source checksums are rejected; and the snapshot/model comparison reports no pending changes.

**Owner approval:** Pending final local evidence and explicit signoff.

## Step 03 — Official Goods/Service Identifier Model (2026-10-16)

**Status:** Implementation committed; acceptance and runtime verification pending.

**Regulatory source:** `RC_IITP.IS V7.9` table 29 defines invoice `sstid` as a required 13-character numeric string. Application normalization accepts ASCII, Persian and Arabic-Indic digits and produces 13 ASCII digits while preserving leading zeroes. Structural validity does not establish authority existence or tax/VAT approval. Separators, control characters and unsupported numeral forms are rejected.

**Changed files:**

- `src/ArginTaxPayer.Domain/Catalog/OfficialGoodsServiceIdentifierPolicy.cs` — deterministic formatting, title validation, verification policy and kind mapping.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/OfficialIdentifierCandidateService.cs` — internal Level1 plus active-MFA candidate creation on staged releases; duplicate rejection and `Unverified` status.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogModelConfiguration.cs` — PostgreSQL check constraint for exactly 13 ASCII digits.
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016150000_ValidateOfficialGoodsServiceIdentifiers.cs` — additive PostgreSQL check with no data update.
- `tests/ArginTaxPayer.Tests/Phase06OfficialIdentifierTests.cs` — format, leading-zero, Unicode-digit, invalid-input, verification, kind-mapping and uniqueness tests.

**Privilege contract:** Level2 and customer users cannot author official identifiers. Candidates require an active Level1 session and enabled MFA. No public endpoint or self-approval is allowed. Authority verification, provenance, release publishing and historical editing remain later-step responsibilities.

**Migration safety:** Before applying the identifier constraint, check existing rows that do not match `^[0-9]{13}$`. Apply only after reviewing the test database target and generated SQL. No seed, destructive operation or write to prior-phase data is permitted.

**Expected results:** Exactly 13 ASCII digits are accepted; invalid lengths and characters are rejected; Accounting goods types map to fiscal Goods and service maps to fiscal Service; database checks and release-scoped uniqueness reject invalid or duplicate codes; candidates remain `Unverified`; only active Level1 users with MFA can create candidates; published releases remain immutable; and existing parties, taxpayers, customers, units and tax rates remain unchanged.

**Owner signoff:** Approved by owner 2026-10-16; log evidence of corrected migration not independently supplied.


### Step03 migration collision corrective action (2026-10-16)
**Observed operator error:** `dotnet ef database update 20261016150000_ValidateOfficialGoodsServiceIdentifiers` built successfully and applied `20261016082706_AddOfficialGoodsCodeFormat`, then failed with PostgreSQL SQLSTATE 42710 because `20261016150000_ValidateOfficialGoodsServiceIdentifiers` also attempted to add `ck_official_goods_code_format`.
**Verified repository:** Both migration files existed on the branch. The first creates the 13-digit check and owns its drop in Down. The later migration has been corrected to validate the existing named CHECK definition and leave it intact in both Up and Down. **Corrective commit:** `318b6ca953ee013beb57e3061be86532bc3c7870`.
**Safety:** No DROP, no renaming, no direct manual manipulation of `__EFMigrationsHistory`, no deletion of earlier migration. The second migration is verification-only. The failure does not prove the second migration was applied. Confirm migration history and constraint via read-only SQL on verified `argintaxpayer_test`, then retry the same target after `git pull --ff-only`.
**Acceptance evidence:** User-supplied log proves build success and duplicate-check failure; corrected migration's build/actual PostgreSQL execution have not yet been independently witnessed. Do not claim Step03 final approval before operator retest.


## Step 04 — Official Unit Codes and Default Catalog (2026-10-16)

**Step03 prerequisite:** Owner approved 2026-10-16. Original operator migration log exposed a duplicate CHECK; correction `318b6ca953ee013beb57e3061be86532bc3c7870` is already committed. Approval is user-reported; no runtime retest output was provided.

**Source inventory:** The owner's Library contains the genuine-looking four-page **`RC_UMGS.ST_V1.18`, Ordibehesht 1404**, with **102** published numbered measurement unit rows across pages 3–4. Source document `RC_IITP.IS V7.9` refers specifically to `RC_UMGS.ST` for invoice `mu`, max 8 numeric characters. **V1.18 is the verified available owner-provided document, not independently verified as the *currently latest* tax authority edition.** Do not claim live tax-authority approval. The PDF's original byte SHA256 is unavailable through current Project file materialization; the seed migration explicitly stores SHA256 of the normalized UTF-8 **transcription** and distinguishes it from original PDF checksum. Operator provenance review required before activating an official release.

**Delivered:**
1. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/Seeds/RC_UMGS_ST_V1_18.units.psv` — 102 records transcribed in row order, `row|code|Persian title`; read-only source-review reference. Example rows: `8|1627|عدد`, `22|1637|لیتر`, `33|164|کیلوگرم`, `102|16134|نفر-ماه`.
2. `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016180000_SeedOfficialUnitsV118.cs` — explicit **additive data-only EF migration** for staged source release `RC_UMGS.ST V1.18` and 102 units using deterministic row GUIDs and canonical transcription SHA256. Requires preexisting Phase06 foundation migration. Uses `ON CONFLICT DO NOTHING` followed by exact title/code/status checks and count=102 assertion. On mismatch throws and aborts the migration transaction without overwriting. Version state remains `Staged (1)`; no automatic `Active` or `Verified` claim. `Down` intentionally preserves reference/history rather than removing units already linked to items.
3. `src/ArginTaxPayer.Domain/Catalog/OfficialUnitCodePolicy.cs` — normalization for 1–8 numeric ASCII/Persian/Arabic-Indic `mu`, preserve leading zeroes; exact positive per-item rational conversion validation and date bounds per ArginAccounting Phase16.
4. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/OfficialUnitCandidateService.cs` — internal-only Level1+enabled MFA candidate create on Staged release, double authorization check in Serializable transaction, database unique check; no Level2/customer write or public route; modification of published rows never allowed in place.
5. `tests/ArginTaxPayer.Tests/Phase06OfficialUnitTests.cs` — verifies 102 rows, uniqueness/order, key codes and titles, code normalization/rejection and exact conversion policy.

**Architecture and restrictions:** Units belong to an official *release*, customer `BaseUnitId` FK references exact unit row, conversion ratios remain scoped to `CatalogItemUnitConversion.ItemId`. A global official code is not the same as an ArginAccounting company's internal UoM code. Unknown or mismatched official unit must never be silently substituted or treated as verified. The current dataset is only a staged reference; the source activation workflow is Step08. New manual candidates do not receive false official validation. Role checks are server-side in the internal create service; HTTP CRUD, operator audit, source synchronization and role-negative integration tests are Step07/08/12/15. No destructive updates to historical invoices or items.

**Important safety and implementation caveats:**
- This does **not** prove V1.18 is the latest official catalogue. If a newer `RC_UMGS.ST` exists, import it as a new version and review differences before activation.
- Historical frozen releases must not be changed. The staging-only create command does not modify an existing unit code, but source corrections require creating a new release.
- Cross-tenant/units selection and quantity dimension compatibility require contextual service-level checks in Steps09–12; numeric formatting alone cannot prove applicability. No arbitrary conversion (e.g. mass to time) is approved.
- The Step02/03 existing migration ordering (notably `20261016082706_AddOfficialGoodsCodeFormat` ordered before `20261016120000_AddPhase06CatalogFoundations`) may prevent clean-from-zero DB reconstruction; do not apply from scratch without confirming baseline or repairing migration ordering in a separate reviewed change. The existing approved shared test DB is the intended target.
- Migration code was committed but **not executed** by assistant; test DB not touched by assistant. Operator must review SQL and confirm target `argintaxpayer_test`. Never run automatic runtime migration or a production seed.

**Local operator commands (after source review):**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06OfficialUnitTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```
For database work, run `zsh scripts/dev-up.zsh` and follow the previously approved `npgsql_value` method in `docs/roadmap/18-phases.md`; confirm actual `SELECT current_database()` exactly `argintaxpayer_test`. Inspect migration SQL from `20261016150000_ValidateOfficialGoodsServiceIdentifiers` to `20261016180000_SeedOfficialUnitsV118`. After source review and approval, explicitly apply only through new target:
```bash
dotnet ef database update 20261016180000_SeedOfficialUnitsV118 --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"
```
Then verify `select count(*) from official_measurement_units where release_id='14051800-0000-4000-8000-000000000118';` returns exactly 102, `select code,title from official_measurement_units where release_id='14051800-0000-4000-8000-000000000118' and code in ('1627','1637','164','16134') order by code;`, `select state,edition,source_sha256 from catalog_reference_releases where id='14051800-0000-4000-8000-000000000118';` returns state 1/edition V1.18 and 64-hex transcription checksum. Re-run targeted EF migration update to validate idempotent no-duplicate behavior. Query `__EFMigrationsHistory` and prior aggregates for unintended changes.

**Acceptance gates:** 102/102 unique source rows; all sample unit codes mapped correctly; invalid and duplicate unit codes blocked; no changes to old releases; only Level1+MFA can create candidates; unit base FK and item-specific exact conversions; migration idempotency/source conflict safety; historical tables unaffected; no false activated/verified status; regression tests and clean Model Snapshot; operator/test-DB proof. **Operator tests/CI/macOS/Ubuntu:** not executed by assistant; Ubuntu full test deferred to Step16 unless a platform-specific issue appears.

**Owner signoff Step04:** Approved 2026-10-16. Operator reported passing macOS tests and migration on the test DB; no machine output independently received.


## Step 05 — VAT Classification, Exemption and Version-Effective Rules (2026-10-16)

**Status:** Code committed and design reviewed; local build/tests, test DB migration, CI and explicit owner acceptance pending. Step04 approved by owner with reported passing tests and PostgreSQL migration (raw outputs not attached).

**Regulatory basis:** User-provided `RC_IITP.IS V7.9` sections for `vra` and the VAT amount: invoice `vra` is decimal (up to 3 integer digits and 2 decimal digits); VAT for exempt goods/services is 0, but VAT=0 can also mean legally zero-rated invoice contexts (export, certain exchange or supply-chain patterns). Specific/special commodities may require special statutory rates. Corrections/references can preserve the reference invoice's tax rate. The publication is a rule schema, not a complete dated authority-signed VAT rate catalog. **Never assume a global 10%, 9%, or 0% default**; taxes depend on date, verified code, invoice pattern and legal basis.

**Domain decisions:**
- `VatTreatment`: Unresolved=0, Standard=1, Exempt=2, ZeroRated=3, Special=4. Exempt and ZeroRated both require explicitly documented rate 0 but remain legally distinct. Unresolved has null rate; Standard/Special require an explicit 0..999.99, at most 2-decimal rate. Zero numeric value alone does not prove exemption.
- `VatRule` includes typed release/rule UUID, stable classification code, legal basis, rate, effective UTC window `[from,to)`, source-verified and release-active flags, optional `ContextKey` for invoice pattern/special legal context. The `VatClassificationPolicy.Resolve` pure domain function selects by legal-effective instant and exact context. Missing rule, unverified/inactive release or unresolved treatment fails closed. More than one applicable rule returns Ambiguous instead of choosing an arbitrary rate. No calculation of invoice VAT/discount/prices is done in Phase06.
- Published VAT classifications may not be edited in place; new rates must be represented by new version/effective intervals. Future correction invoices must use the referenced invoice's immutable historical snapshot; this is an integration constraint for Phase07–08.
- ArginAccounting Phase16 `TaxProductProfileRef` remains a versioned projection/reference; do not inject editable tax rates, invoice totals, inventory valuation or accounting postings into its product master.
- **No government-provided rate catalog was seeded**. Legal provenance for actual rate classifications must be checked at Step06–08 before any release can be marked Verified/Active.

**Committed files:**
- `src/ArginTaxPayer.Domain/Catalog/VatClassificationPolicy.cs`: treatment taxonomy, legal rule validation, date/context resolution and fail-closed provenance.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogEntities.cs`: `VatClassificationEntry.Treatment` and `ContextKey` additions (legacy rows are not falsely auto-classified).
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogModelConfiguration.cs`: database mappings, numeric precision and treatment/rate check constraints.
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016190000_AddVatTreatmentAndContext.cs`: additive columns with explicit existing-data precondition, safe Down guard and updated constraints. If any VAT rows already exist, migration deliberately aborts rather than guessing their legal treatment; owner/operator must review backfill strategy. No existing Phase03–05 schema is modified.
- `tests/ArginTaxPayer.Tests/Phase06VatClassificationTests.cs`: tax treatment, 0 vs exemption, special rates, cross-date boundaries, overlaps, provenance and EF metadata.

**Design boundary and remaining obligations:** ContextKey match is intentionally exact, not general-to-specific fallback. Phase06 Step06/08 will implement release versioning/activation, source trust/audit and interval overlap prevention at storage/transaction level. Steps11/12 will validate a selected item's official ID/VAT rule against invoice-like context. Step14 hands off an immutable snapshot; Phase07 implements actual legal invoice validation and VAT arithmetic. Level1-only modification of official reference catalog remains a future API/authorization obligation; Step05 adds no public endpoints.

**Operator tests:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06VatClassificationTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```
For data changes, use existing `zsh scripts/dev-up.zsh` AutoSSH runner and canonical `.env.dev.local` Npgsql parser documented in `docs/roadmap/18-phases.md`. Require **actual** database verification `SELECT current_database() = 'argintaxpayer_test'`. Preflight `SELECT count(*) FROM vat_classification_entries;` **must equal zero**; otherwise stop and review the migration, do not force it or assign tax rates. Review generated migration SQL and apply explicitly:
```bash
dotnet ef migrations script 20261016180000_SeedOfficialUnitsV118 20261016190000_AddVatTreatmentAndContext --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --output /tmp/phase06-step05.sql
dotnet ef database update 20261016190000_AddVatTreatmentAndContext --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"
```
Verify `__EFMigrationsHistory`, `information_schema.columns` for `treatment`/`context_key`, `pg_constraint` for `ck_vat_treatment`, `ck_vat_treatment_rate`, `ck_vat_percentage`; verify existing unit data still counts 102 and seeded unit release is unchanged.

**Acceptance:**
1. Build, focused VAT tests and full regression suite pass; EF Model Snapshot reports no unintended drift.
2. Unknown classification or missing authoritative source cannot be auto-resolved into a tax rate; no 10%/9% hardcoding.
3. Legal Exempt/ZeroRated distinction preserved, zero rate mandatory only when legally asserted; special-rate and context support.
4. Temporal half-open boundaries resolved correctly, overlaps rejected as ambiguous; no retroactive history rewrite.
5. DB constraints reject inconsistent treatment/rate; reviewed migration only on `argintaxpayer_test`, and preexisting VAT rows fail safely.
6. Phase03–05 data and Step04 unit seed remain unchanged; no invoice issuance or posting.
7. No automatic public tax-authority verification claimed and no source rates seeded.
8. Owner explicitly signs off before Step06.

**Test evidence:** GitHub code and docs committed, build and test DB migration not run by assistant; CI not inspected; Ubuntu deferred to final phase, except if schema-specific platform issue arises.
**Owner signoff Step05:** Approved by owner 2026-10-16. Operator reported all tests and migration successful, but raw test logs were not attached.


## Step 06 — Versioned Reference Catalog (2026-10-16)

**Status:** Implementation committed to `phase/06-goods-services`; local test, test DB migration and owner acceptance pending. Step05 explicitly owner-approved with operator-reported successful tests and migration; no raw outputs were independently received.

### Architecture and ArginAccounting compatibility
- Canonical issuer/source edition and SHA256 checksum identify a reference release. Source import timestamp and legal effective time are separate. `CatalogReleasePolicy` describes strictly forward release lifecycle `Staged (1) -> Validated (2) -> Active (3) -> Superseded (4)`. It does not itself publish a release: privileged audited activation is Step08.
- Only Staged official reference rows are writable. PostgreSQL triggers deny `DELETE` of any official release/entry, `UPDATE` or `INSERT` of entries outside a staged release, and changing the reference row's release ID. Release identity/provenance cannot be modified after creation. Active release must have an activation timestamp; the timestamp cannot later change.
- Partial unique index restricts one `Active` release per `source_document`. This does **not** prevent separately sourced unit, goods/service and VAT releases from being active at once. Step08 must supersede previous active release before activating successor in the same transaction. Earlier activated releases remain queryable by their original `release_id`.
- `CatalogReferenceSnapshot` records release ID, issuer document, edition, original canonical SHA256, code, title, capture and legal-effective instants. Immutable record projection, not yet a persisted invoice snapshot or evidence of a legally validated authority source; invoice handoff integration occurs in Step14.
- VAT intervals are half-open `[effective_from_utc, effective_to_utc)`. Database trigger rejects overlaps within the same `release_id`, `code` and null-safe `context_key`, including concurrent writes. Trigger locks the parent release row to serialize sibling writes. The same code in a different release is permitted (historical versions). No silent rate overwrites.
- ArginAccounting master item keeps a versioned TaxProductProfileRef and historical quantity/unit snapshot; no in-place mutation of past legal mappings or tax rates. Customer item code is not a tax reference code.

### Files
1. `src/ArginTaxPayer.Domain/Catalog/CatalogReleasePolicy.cs`: release state policy, identity validation, UTC half-open validity and immutable snapshot contract.
2. `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016200000_EnforceReferenceVersionHistory.cs`: **database-only** additive migration creating protected triggers/functions and one partial active-release index. No data edits or new model columns. Raw trigger SQL is owned by migration and does not require an EF snapshot change.
3. `tests/ArginTaxPayer.Tests/Phase06ReferenceVersioningTests.cs`: lifecycle, writability, UTC boundary, overlaps, provenance, historical snapshot tests.
4. `docs/phases/phase-06.md`: formal owner signoff for Step05 and Step06 implementation/acceptance record.

### Required negative/integration verification
- INSERT or UPDATE for a reference entry on validated/active/superseded release fails at SQL level. DELETE fails even on staged release.
- Two conflicting effective-dated VAT rows with same release/code/context (including NULL) fail. Adjacent half-open intervals and identical code in different releases pass.
- Repeated active release from same document fails; active release without `activated_at_utc` fails; released source metadata cannot be edited.
- The 102 seeded `RC_UMGS.ST V1.18` units remain unchanged and staged until independently verified/activated in Step08.
- Snapshot sourced from active release retains source/version even after later supersession.
- Migration applies only to confirmed test DB `argintaxpayer_test`, does not change earlier Phase03–05 data, VAT treatment or unit rows. `Down` refuses while reference releases exist to protect historic data. Migration is not designed to run automatically at application startup.
- Future authoritative import, legal effective source review, Level1+MFA activation, audit trail and release rollback belong to Steps07–08. No public API/UI, actual tax submission or invoice writes in Step06.

### macOS acceptance commands
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06ReferenceVersioningTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```

**Test database gate:** Start `zsh scripts/dev-up.zsh`; load the private `.env.dev.local` via the roadmap-prescribed `npgsql_value` method; confirm actual `SELECT current_database()` exactly `argintaxpayer_test`. Generate/review:
```bash
dotnet ef migrations script 20261016190000_AddVatTreatmentAndContext 20261016200000_EnforceReferenceVersionHistory --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --output /tmp/phase06-step06.sql
```
Before applying, review active reference releases for duplicate active source documents; if they exist, the partial unique index will reject the migration and the data needs explicit operator review. Review table trigger conditions and backup test DB. Then explicitly run:
```bash
dotnet ef database update 20261016200000_EnforceReferenceVersionHistory --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"
```
Verify PostgreSQL `pg_trigger` four guards and VAT interval trigger, `pg_indexes` unique source index, migration history, stage unit release state, and sample intentional violations **within a rollback-only transaction**. Do not run destructive/manual maintenance statements against any production DB.

**Evidence:** Code/doc committed. .NET/CI/PostgreSQL integration not executed by assistant (local container has no `dotnet` binary). Migration has a genuine database runtime test requirement. Step06 **owner approved 2026-10-16**. Operator reported successful tests and Migration; logs not attached independently.


## Step 07 — Reference Import Pipeline (2026-10-16)

**Status:** Implementation committed; local build, unit/integration tests and owner acceptance pending. Step06 owner-confirmed successful on macOS + `argintaxpayer_test` on 2026-10-16; independently observed test logs not attached.

**Scope and risk controls:**
- `ReferenceImportParser` is a pure preview tool consuming **UTF-8 pipe-separated canonical import data (PSV)**, not PDF/Excel. Unit schema `code|Persian title`, two columns; goods/service candidate schema `13digitcode|title|Goods/Service`, three columns. The goods-service document key `RC_GS` is a **technical candidate namespace only, NOT a claim of an existing official government document identifier**. Operator must supply actual provenance and check government specifications before validating/activating.
- A bounded 2 MB payload / 20,000 row parser produces line-numbered issues, avoids partial publication, rejects invalid UTF-8, invalid official-code syntax, missing title, invalid kind, bad headers/columns, duplicate codes within release (goods and service namespace separated), non-HTTPS source provenance metadata, and excess rows. Source URL is **operator-supplied metadata, not proof of issuer authenticity**.
- SHA256 covers the **exact uploaded file bytes**. The source edition/document + checksum are uniquely indexed in PostgreSQL. The Step07 internal `ReferenceImportService.StageAsync` reparses **the original bytes** server-side to defeat tampered preview/digest payloads. Only authenticated active Level1 with enabled MFA may stage. Permissions are checked again inside a serializable transaction.
- Imported `CatalogReferenceRelease` is created **Staged (state=1)**, and official goods/service candidates remain `Unverified`. Existing matching document+SHA import returns existing release ID; conflicting reused checksum metadata is rejected. Transaction inserts release and all rows atomically; PostgreSQL reference triggers from Step06 enforce staged-only writes. No new migration, new table or EF snapshot change is intended.
- Step07 has **no public HTTP endpoint or administrative upload UI**. Authenticated API contracts and UI are in Steps12–13; audited source verification and publication in Step08. This step must not automatically mark rows as official. A URL with `https` is not a digital signature and must never be treated as an authority assertion.
- ArginAccounting compatibility is preserved: tenant ItemCode/SKU stay customer-owned; official goods/service code and unit catalog releases are shared references; no stock, pricing or accounting journal changes.

**Committed files:**
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/ReferenceImportParser.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/ReferenceImportService.cs`
- `tests/ArginTaxPayer.Tests/Phase06ReferenceImportTests.cs`
- `docs/phases/phase-06.md`

**Testing (operator macOS):**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06ReferenceImportTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```

**PostgreSQL verification:** No Step07 EF migration is introduced. Read-only check only after `zsh scripts/dev-up.zsh`, private `.env.dev.local` Npgsql extraction and `SELECT current_database() = 'argintaxpayer_test'`:
```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261016200000_EnforceReferenceVersionHistory';
SELECT source_document,edition,state,count(*)
FROM catalog_reference_releases GROUP BY source_document,edition,state
ORDER BY source_document,edition;
SELECT count(*) FROM official_measurement_units
WHERE release_id='14051800-0000-4000-8000-000000000118';
```
Expected 102 preserved official units, no previously active/validated state silently changed and no catalog HTTP endpoints inadvertently exposed. True DB staging, concurrent idempotency and Level2/Customer denied tests require a test-environment integration harness and are acceptance gaps until proven; the parser's pure unit tests alone do **not** establish database idempotency.

**Acceptance:**
1. Accurate preview counts and row-specific error list; malformed, duplicate, unsupported, unsafe and oversized upload rejected.
2. The exact same source file imported twice results in one staged release with consistent source hash and row counts; a same-checksum metadata conflict rejected.
3. No partial import on row errors or database constraint violations.
4. Level1+enabled MFA only for staging; anonymous, Level2, Customer denied.
5. Existing validated, active and superseded versions stay immutable and queryable; no UI or public API deployed at this step.
6. Correct V1.18 seeded 102 units remain untouched and not falsely activated.
7. Build/tests/EF snapshot checks successful, and no new migration needed.
8. Owner explicitly approves before Step08.

**Evidence:** GitHub code/docs committed. No .NET test runner or live PostgreSQL connected in assistant runtime. Owner must share focused Build and test result and explicitly authorize next step.
**Owner signoff Step07:** Approved by owner 2026-10-16; operator reported successful Step07 tests, raw test output not independently attached.


## Step 08 — Reference Activation and Audit (2026-10-16)

**Status:** Implementation committed, pending operator build/tests, PostgreSQL migration and explicit signoff. Step07 owner-approved on 2026-10-16 based on operator-reported successful tests; raw test outputs not independently received.

### Contracts and safety
- `CatalogReleaseActivationService` is an **internal only** command service, no public HTTP endpoint, no change to application routing. Requires an authenticated, active `Level1` identity with current MFA enabled; re-check inside serializable transaction. Level2/Customer/unauthed identities cannot act through this service.
- `ValidateAsync`: staged(1) -> validated(2) with nonempty evidence reference and reason. A release must contain at least one reference row. Any Goods/Service candidate still marked `Unverified` blocks validation; 13-digit formatting or an HTTPS reference alone does not imply official existence. Evidence is an **operator attestation**, NOT independently confirmed authority data.
- `ActivateAsync`: validated(2) -> active(3). In the same transaction, previous active release for the *same source document* becomes superseded(4), append-only `supersede` audit is written, new release activated with immutable `activated_at_utc` and `activate` audit written. Previous active updated *before* new active to satisfy PostgreSQL's immediate partial unique index. A transaction failure reverts all changes, including audit.
- `WithdrawAsync`: emergency logical withdrawal active(3) -> superseded(4), with explicit audit. Does NOT delete/rewrite any records or illegally mutate a superseded release back into Active. After withdrawal, the source may temporarily have no active release. Re-publishing previous bytes as a new release is not implemented due to unique document+SHA constraint; separately reviewed restore design is required for full prior-version reinstatement.
- Role decisions and audit are server-owned. SQL trigger `trg_phase06_audit_immutable` forbids UPDATE and DELETE on `catalog_release_audit`. FK RESTRICT preserves actor and release attribution. `catalog_release_audit` records release ID, Level1 actor, action, from/to state, evidence reference, reason, UTC timestamp. Operator must supply genuine review evidence; no authority communication or cryptographic verification occurs here.
- PostgreSQL Step06 triggers already prevent invalid transitions and historical data mutation; Step08 service does not edit official entry rows. ArginAccounting source version and historical snapshots remain stable. No changes to Phase03–05 tenant/buyer/taxpayer data.

### Changed files
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016210000_AddReferenceActivationAudit.cs`: additive SQL migration for append-only audit table and DB trigger, with empty-only rollback protection; no EF entity schema diff or Model Snapshot update.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogReleaseActivationService.cs`: atomic staged->validated->active->superseded and emergency withdrawal, Level1+MFA, audit insertion, conflict outcomes and preservation of historical state.
- `tests/ArginTaxPayer.Tests/Phase06ReferenceActivationTests.cs`: metadata/state-machine and contract tests. Real role/transaction/trigger/concurrent-activation tests **still need an authenticated test-DB integration harness**.
- `docs/phases/phase-06.md`: this acceptance record and Step07 approval.

### macOS tests
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06ReferenceActivationTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```

### PostgreSQL migration and verification
Explicit only on test DB `argintaxpayer_test`: start `zsh scripts/dev-up.zsh`, parse private `.env.dev.local` with documented `npgsql_value` function, and verify `SELECT current_database()`. Review generated SQL:
```bash
dotnet ef migrations script 20261016200000_EnforceReferenceVersionHistory 20261016210000_AddReferenceActivationAudit --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --output /tmp/phase06-step08.sql
```
Then, after independent DB name, SQL and backup checks, apply:
```bash
dotnet ef database update 20261016210000_AddReferenceActivationAudit --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"
```
Read-only verify:
```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory"
 WHERE "MigrationId"='20261016210000_AddReferenceActivationAudit';
SELECT column_name FROM information_schema.columns
 WHERE table_name='catalog_release_audit' ORDER BY ordinal_position;
SELECT tgname FROM pg_trigger WHERE tgname='trg_phase06_audit_immutable' AND NOT tgisinternal;
SELECT count(*) FROM official_measurement_units
 WHERE release_id='14051800-0000-4000-8000-000000000118';
```
Audit table should have nine columns; audit immutability trigger should exist; 102 Step04 units remain intact and staged. **Do not activate V1.18 just to test**: authentic issuer/version review must occur before any state change. Role/MFA, rollback-only transition probes, failure transaction audit atomicity, and concurrent activate tests must be performed in an isolated test fixture with its own disposable candidate releases, not against the owner-seeded 102-unit reference.

### Acceptance
1. Build, focused tests, full regression and EF Model Snapshot successful.
2. Migration applies to correct test DB with table/append-only protection; no old data changed.
3. All state transitions authorized Level1+MFA only; anonymous, Level2 and Customer denied.
4. Activation of a new release and superseding prior active version succeed atomically with both audit rows; racing admins cannot create two active versions.
5. Invalid transitions, empty/unverified releases and evidence-less transitions are rejected.
6. Withdrawal never deletes past releases or reactivates a superseded historical one; historical snapshots still resolve.
7. No false tax-authority verified/approved claims from format or source URL alone; Step04 102 units preserved.
8. Owner signoff before Step09.

**Observed evidence:** GitHub commits created; assistant did not run dotnet, PostgreSQL migrations or CI. Test integration transaction/race/MFA cases are not yet independently demonstrated.
**Owner signoff Step08:** Pending.

## Phase Closeout

Not started.
