# ArginTaxPayer — Phase 06: Goods and Services

**Status:** Steps01–15 explicitly owner-approved (Step15 includes operator-reported successful security, regression, HTTP Smoke and integration tests on 2026-10-17; raw logs not independently attached). Step16 Final Acceptance and Handoff is IN PROGRESS; no merge, tag or GitHub Release authorized yet.
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
| 03 | Official Identifier Model | Official goods/service identifier, origin and verification | Reject invalid/duplicate identifiers and unsupported official claims | 02 | Owner-approved earlier Phase06 progression; raw results not attached |
| 04 | Official Unit Codes | Official unit-code reference and internal-unit mapping | Reject unknown units and incompatible mappings | 03 | Owner-approved earlier Phase06 progression; 102-unit seed confirmed by operator |
| 05 | VAT Classification | Date- and source-dependent VAT classification, rates and exemptions | Do not assume fixed rates; test boundary dates | 03–04 | Owner-approved 2026-10-16; tests/migration operator-reported successful |
| 06 | Versioned Reference Catalog | Source versions, validity intervals, snapshots and immutable history | Recover prior versions and control overlap | 03–05 | Owner approved 2026-10-16; tests/migration reported successful |
| 07 | Reference Import Pipeline | Official-file staging, preview and validation | Idempotent repeated import and reported errors | 06 | Owner-approved 2026-10-16; focused tests operator-reported successful |
| 08 | Reference Activation and Audit | Controlled release with audit and logical rollback | Move active version without deleting history | 07 | Owner-approved 2026-10-16; tests/migration operator-reported successful |
| 09 | Customer Product Domain | Customer products, status and internal identifiers | Tenant isolation and protection of references | 02–06 | Owner-approved 2026-10-16; tests operator-reported successful |
| 10 | Customer Service Domain | Customer services and type rules | CRUD, ownership and type-mismatch tests | 09 | Owner-approved 2026-10-16; tests operator-reported successful |
| 11 | Tax Identifier Mapping | Historical mapping from customer items/services to official identifiers | Reject invalid or wrong-kind mappings | 08–10 | Owner-approved 2026-10-16; tests/migration operator-reported successful |
| 12 | Catalog API and Search | Search, filtering, pagination and concurrency support | Role, paging and stale-update tests | 09–11 | Owner-approved 2026-10-16; operator-reported successful |
| 13 | RTL Catalog UI | Lists, forms, selection search and localized errors | Authorized paths, validation and accessibility tests | 12 | Owner-approved 2026-10-17; dashboard/branding refinements accepted |
| 14 | Invoice Handoff Contracts | Immutable snapshot contract for Phases 07/08 without invoice issuance | Version contract and historical immutability tests | 06, 11–13 | Owner-approved 2026-10-17; tests operator-reported successful |
| 15 | Security and Regression | Isolation, race, migration and Phase03–05 regression tests | Prevent cross-customer and former-partner access | 01–14 | Owner-approved 2026-10-17; security, regression, HTTP Smoke and integration operator-reported successful |
| 16 | Final Acceptance and Handoff | Mac and Ubuntu aggregate tests, documentation and risk report | Evidence report, owner approval and separate merge/tag | 15 | In progress; final audits/documentation committed; owner signoff pending |

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
**Owner signoff Step08:** Approved by owner on 2026-10-16. Tests and PostgreSQL migration reported successful; raw outputs not independently received.


## Step 09 — Customer Product Domain (2026-10-16)

**Status:** Domain and internal scoped application service committed; macOS .NET tests and integration acceptance pending. Step08 explicitly owner-approved following operator-reported successful tests and PostgreSQL migration.

**ArginAccounting interoperability:**
- `CatalogItemKind.StockItem=1`, `NonStockItem=2`, `Service=3` correspond to ArginAccounting Item kinds. This step implements *goods only* (stock and nonstock). A goods item cannot silently change its kind, including Stock to NonStock; Service CRUD follows Step10.
- Customer's normalized `InternalCode` is separate from official fiscal `sstid`; normalization uses Unicode NFC + uppercase invariant and retains zeroes. Unique `(customer_user_id,internal_code)` existing PostgreSQL index guards case-normalized duplicates created via this service. Existing records use no intrusive backfill.
- `BaseUnitId` must reference a known official measurement unit. This domain deliberately permits already owner-reviewed `RC_UMGS.ST` staged units for test/development while official activation awaits source confirmation. **Unverified/staged unit is not treated as authority-approved at invoice handoff**. Invoice-ready validation must require approved release in Steps11–14.
- Product lifecycle: Active / Inactive / Blocked / Discontinued. Discontinue is a soft archive preserving stable ID, links and foreign-key history, with Revision optimistic concurrency. No hard deletes, no price/stock/COGS/VAT figures persisted to item master.
- ArginAccounting company ID and ArginTaxPayer customer account user ID remain separate IDs requiring Phase13 mapping.

**Service implementation:** `CustomerProductService` is **internal (no HTTP endpoints yet)** and exposes authenticated, customer-scoped Create, Get, paginated List and Update/Discontinue. Every query filters `CustomerUserId`. Write transactions are Serializable with server-side active account and commercial-owner checks repeated inside transaction. Root Level1 only sees customers with matching `CustomerCommercialOwner.RootLevel1UserId`; Level2 only currently assigned `PartnerLevel2UserId`; customers only their own account. Both admins require live enabled MFA. Cross-tenant writes and stale revisions fail closed. Create attempts with an unavailable base unit fail; unique conflicts are caught and returned as Conflict.

**Changed files:**
- `src/ArginTaxPayer.Domain/Catalog/CustomerProductPolicy.cs`: exact codes, title, item kind and archive invariants.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CustomerProductService.cs`: role-scoped application service with Serializable transactions and concurrency outcomes.
- `tests/ArginTaxPayer.Tests/Phase06CustomerProductTests.cs`: normalizer/kind/archival/EF schema metadata focused checks.
- `docs/phases/phase-06.md`: owner approval Step08 and Step09 record.

**Migration:** **No new EF migration.** Step02 already added `customer_catalog_items` with tenant unique index, revision, status, kind, base unit FK and restrictive historical relationships. Step09 only introduces pure domain rules, internal service and tests.

**Limitations and integration gates:**
- Focused tests establish validation and EF metadata, **not** authenticated DB transaction/race/cross-tenant behavior; further live local integration tests are required before owner approval.
- Internal scoped service is not a published API/UI; routes and RTL client arrive in Steps12–13. No new URL exposure or implicit fiscal party generation.
- Changing the selected base unit on a referenced item may have historical implications: downstream unit conversion/invoice snapshot guards are planned Steps11–14. Do not apply such edits to real invoiced products until snapshot contract is finished.
- The `CustomerProductService` uses release-verified or owner-reviewed staged unit eligibility for internal catalog work, but cannot claim regulatory unit verification. Do not send invoices based only on staged units.
- No price-list/SKU/barcode/category/purchase-sales stock policy changes are included in Step09; these ArginAccounting detailed fields remain future interoperability scope, not falsely claimed implemented.

**Operator macOS acceptance:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06CustomerProductTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```
No Step09 database update is required. Optional read-only smoke via `zsh scripts/dev-up.zsh`, verified `argintaxpayer_test`:
```sql
SELECT indexname FROM pg_indexes WHERE tablename='customer_catalog_items';
SELECT count(*) FROM official_measurement_units WHERE release_id='14051800-0000-4000-8000-000000000118';
```
Expected 102 unit rows preserved. Unit + model tests are insufficient alone for concurrent duplicate create, role-scope transfer, and DB rollback proof; collect those in an isolated test fixture when available.

**Step09 acceptance criteria:** build/tests pass; legal kinds 1/2 only; normalized customer codes unique per customer, not globally; current Level1/Level2/customer scopes and transfer restriction validated; base unit valid; stale revisions/duplicate codes rejected; discontinued items retained; no legacy Party/Taxpayer or external tax identifier overwritten; no new migration. **Owner signoff Step09:** Approved 2026-10-16; owner reports focused tests successful, logs not independently attached.


## Step 10 — Customer Service Domain (2026-10-16)

**Status:** Implemented on `phase/06-goods-services`; waiting on macOS tests and owner signoff. Step09 owner-approved after operator-reported successful tests; raw outputs not independently received.

**Design decisions and constraints:**
- Services share the existing `customer_catalog_items` aggregate with Products, but `Kind=3` (`CatalogItemKind.Service`) is mandatory. ArginAccounting Stock/NonStock types (1/2) remain restricted to the Step09 `CustomerProductService`. Service records have no warehouse quantity, FIFO/COGS or inventory movement. Service is not interchangeable with either goods kind.
- `CustomerServicePolicy` normalizes internal customer code (Unicode NFC + uppercase invariant), trims display title, preserves leading zeros, validates maximum lengths and base unit. Service and product codes share the **same customer-wide unique namespace** enforced by the existing unique index `(customer_user_id,internal_code)`; cross-customer reuse is allowed. InternalCode is never a fiscal identifier.
- `CustomerServiceCatalogService` supports authenticated Create, Get, bounded paginated List, Update and Discontinue. Every query is `CustomerUserId`-scoped **and** service-kind constrained. Changing to/from goods is not permitted by this service. Discontinue is a soft archival transition preserving UUID, FK references and revision.
- `Revision` optimistic concurrency is checked for mutations; the existing PostgreSQL index prevents duplicate codes, and Serializable transactions + rechecked session/scope protect writes from stale commercial ownership. Root Level1 access requires a current `CustomerCommercialOwner.RootLevel1UserId`, partner Level2 access requires current partner assignment, and both require enabled MFA. Customer can edit only its own catalog account. No old grant or taxpayer-profile shortcut.
- Base unit must exist in `official_measurement_units` and be selectable (Active or the owner-reviewed staged `RC_UMGS.ST` units in internal development mode); **Staged is not authority verification**, and future invoice readiness requires appropriate approved source in Steps11–14.
- All service/catalog data remains separate from any financial posting, VAT classification, official code mapping and invoice records. The complete ArginAccounting SKU/barcode/category/sales policy fields are not claimed as implemented in this step.

**Committed files:**
1. `src/ArginTaxPayer.Domain/Catalog/CustomerServicePolicy.cs`
2. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CustomerServiceCatalogService.cs`
3. `tests/ArginTaxPayer.Tests/Phase06CustomerServiceTests.cs`
4. `docs/phases/phase-06.md`

**Migration impact:** No new EF migration. `customer_catalog_items` in Step02 already supports goods and services (Kind 1,2,3), scoped unique internal codes, Revision and FK-restricted base units. Do not run `dotnet ef database update` just for Step10. No production DB changes or writes made by assistant.

**Required local commands:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06CustomerServiceTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```

**Negative/integration acceptance:** Reject missing/invalid base units, duplicate normalized internal codes (including collisions with products), unsupported kind conversion, service access to other customers, unauthorized former partner and lost-MFA admin, stale revision updates, writes after Discontinued, and invalid page/pageSize. Confirm real PostgreSQL transaction behavior and FK history protection with disposable test records. Focused pure/EF metadata tests do **not** yet establish these role/race/DB-integration outcomes. Existing Phase03–05 data and 102 seeded units must remain unchanged. No public HTTP endpoint or UI is exposed in this step; those belong to Steps12–13.

**Evidence:** GitHub code/tests committed; .NET, CI and live PostgreSQL integration not executed or inspected by assistant. Tests require operator run and explicit signoff.
**Owner signoff Step10:** Approved 2026-10-16; operator reported successful tests, raw logs not independently attached.


## Step 11 — Tax Identifier Mapping (2026-10-16)

**Status:** Implementation committed; local build, focused/full tests, PostgreSQL migration and owner acceptance pending. Step10 owner-approved after operator-reported successful tests.

**Purpose:** Map each customer-owned `CustomerCatalogItem` (goods/service) to a verified official `OfficialGoodsServiceEntry` using effective-dated historical rows, independent of `InternalCode` and company SKU. No invoice issuance or fiscal submission in this step.

**Rules:**
- Internal StockItem(1) / NonStockItem(2) may map only to official Goods(1); internal Service(3) only official Service(2). Missing, staged, superseded, unverified or wrong-kind identifiers are rejected; official format alone never guarantees authority recognition.
- Mapping requires official entry `VerificationStatus=2`, its reference release `State=3` (active) and recorded `validate` action evidence in append-only `catalog_release_audit`. The audit represents a Level1 operator review; it is **not an external digital government signature**. Actual government trust must be verified before authoritative invoice use. Releasing an internally unverified identifier is prohibited by Step08.
- History interval is UTC half-open `[EffectiveFromUtc, EffectiveToUtc)`; distinct non-overlapping windows are maintained per item. A previous open-ended window may be closed *once* by setting its end date and incrementing Revision without changing the existing official ID/start/item. Historical Delete and broader UPDATE prohibited at PostgreSQL trigger level. Old snapshots remain readable.
- Scoped `TaxIdentifierMappingService` offers `AssignAsync`, `CloseAsync`, and `HistoryAsync`; every operation re-checks active session and current customer/Level1/Level2 owner scope, with Level1/Level2 MFA required. Transactions use SERIALIZABLE, EF revision concurrency, FK restrictions, source evidence and date overlap checks. No public endpoint or customer-facing UI until Steps12–13.
- PostgreSQL trigger serializes writes to same customer item, blocks history deletes/broad rewrites, enforces verified+active official entry, matching goods/service kind, and rejects overlaps even under concurrent requests.
- Staged seed `RC_UMGS.ST V1.18` remains unchanged (102 units); it is a measurement-unit catalog and not a goods/service identifier source.

**Committed files:**
1. `src/ArginTaxPayer.Domain/Catalog/TaxIdentifierMappingPolicy.cs` — type compatibility, rule verification and temporal checks.
2. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/TaxIdentifierMappingService.cs` — authenticated scoped create/close/history internal application service, fail-closed provenance.
3. `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016220000_ProtectOfficialTaxMappings.cs` — additive SQL trigger/function enforcing active verified source, immutability and overlap exclusion. No new EF columns or snapshot change. Rollback prohibited while any historical mapping exists.
4. `tests/ArginTaxPayer.Tests/Phase06TaxIdentifierMappingTests.cs` — validity windows, incompatible types, trust failures and EF metadata.
5. `docs/phases/phase-06.md` — Step10 approval and Step11 acceptance.

**Local operator tests:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06TaxIdentifierMappingTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```
Confirm AutoSSH via `zsh scripts/dev-up.zsh`, load DB connection privately from `.env.dev.local` using roadmap's approved parser, and verify actual `SELECT current_database();` is exactly `argintaxpayer_test`. Before migration, inspect existing mapping count and foreign-key data quality; ensure test backup available. Review SQL:
```bash
dotnet ef migrations script 20261016210000_AddReferenceActivationAudit 20261016220000_ProtectOfficialTaxMappings --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --output /tmp/phase06-step11.sql
```
Explicit test-DB application only after review:
```bash
dotnet ef database update 20261016220000_ProtectOfficialTaxMappings --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"
```
Read-only postchecks:
```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory"
 WHERE "MigrationId"='20261016220000_ProtectOfficialTaxMappings';
SELECT tgname FROM pg_trigger
 WHERE tgname='trg_phase06_tax_mapping_guard' AND NOT tgisinternal;
SELECT count(*) FROM catalog_official_mappings;
SELECT count(*) FROM official_measurement_units
 WHERE release_id='14051800-0000-4000-8000-000000000118';
```

**Acceptance gaps:** Current focused tests are pure/domain and EF metadata tests. Live verified-source mapping tests, Level1/Level2/Customer boundary tests, historical closure, rollback-only wrong-kind/overlap/trigger negative tests and concurrent mapping insertion on **disposable test fixtures** remain required integration evidence before calling Step11 fully verified. Do not activate or mutate actual regulatory releases solely for testing. Source documentation/authority proof must not be inferred from a URL.

**Owner signoff Step11:** Approved 2026-10-16. Operator reported successful tests and migration; raw outputs not independently attached.


## Step 12 — Catalog API and Search (2026-10-16)

**Status:** Code, route registration, focused tests committed; operator build/runtime acceptance pending. Step11 explicitly owner-approved based on operator's test/Migration success confirmation.

**Routes and security:** For the phase's current development host, these routes are registered only when `ASPNETCORE_ENVIRONMENT=Development` and `identityDevelopmentEnabled` (connection configured). Every handler additionally verifies that remote TCP peer is loopback (otherwise 404), and the internal services authenticate a Bearer session and check current customer ownership/roles. No production catalog endpoints or public admin upload/publish route are exposed. This follows Phase03–05 development API policy.

**Catalog endpoints (base `/api/dev/catalog`):**
- `GET /official?page=1&pageSize=20&query=...&kind=1`: active+verified goods/service reference directory; returns code, title, kind, release ID. Inactive/unverified records excluded.
- `GET /customers/{customerId}/products?page=1&pageSize=20&query=...` and `GET /customers/{customerId}/services?... `: separate scoped lists, code/title matching with PostgreSQL case-insensitive ILIKE and stable `InternalCode, Id` order. `pageSize` range 1..100, `page` 1..100000; `HasMore` computed via `size+1`, no unbounded export.
- `POST /customers/{customerId}/products`, `GET /customers/{customerId}/products/{id}`, `PUT /customers/{customerId}/products/{id}`: Step09 service; body `InternalCode,Title,Kind,BaseUnitId,ExpectedRevision,Discontinue`. Create refuses stale revision/discontinue.
- Parallel `/services` routes for Step10 service; body `InternalCode,Title,BaseUnitId,ExpectedRevision,Discontinue` with fixed Kind 3.
- `GET /customers/{customerId}/items/{itemId}/tax-mappings`, `POST /customers/{customerId}/items/{itemId}/tax-mappings` and `PUT /customers/{customerId}/tax-mappings/{mappingId}/close?expectedRevision=...&closedAtUtc=...` expose Step11 strict historic mapping service. No direct mapping DELETE.
- Existing concurrency via `expectedRevision` is returned as 409 Conflict, invalid input 400, missing resource 404, no session 401, forbidden ownership 403, and successful creation 201. Do not equate code-format validity with verified authority data.

**Changes:**
1. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogSearchService.cs` — tenant-scoped product/service searchable, paginated result and active verified official directory (internal search).
2. `src/ArginTaxPayer.Api/Catalog/CatalogDevelopmentEndpoints.cs` — loopback-only minimal API routes and DTO/status mapping.
3. `src/ArginTaxPayer.Api/Program.cs` — DI registration and conditional development route map.
4. `tests/ArginTaxPayer.Tests/Phase06CatalogApiSearchTests.cs` — pagination/DTO contract tests.
5. `docs/phases/phase-06.md` — Step11 signoff and Step12 record.

**Migration:** None. All persistence tables and triggers already exist from Steps02–11. No `dotnet ef database update` for Step12.

**Local macOS:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06CatalogApiSearchTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
On running local Development API, use actual active sessions (Level1, Level2 assigned to customer, customer self and different customer) to verify `GET /api/dev/catalog/customers/{id}/products?page=1&pageSize=20` returns 200 for allowed session, 403 for wrong tenant, 401 for missing authorization, and 404 for nonloopback; verify `GET /api/dev/catalog/official?page=1&pageSize=20` excludes staged or unverified entries. Test create/PUT with disposable test products/services and valid staged source-unit ID, then `PUT` stale ExpectedRevision returns 409. Run no regulatory release activation for route testing.

**Security limitations:** No production exposure, no CORS policy changes, no client-side access control shortcuts. Initial official directory currently requires active session with a recognized role, not a scoped business entitlement; Phase15 must verify authorization policy for partner/customer-facing official data. Plain wildcard ILIKE uses parameterized EF query, with a 120-character query bound; search can be optimized/indexed in later phases if needed. Case-folded internal code uniqueness is ensured on writes via Step09/10 normalization. Endpoint integration tests (including loopback/IP and real Bearer role isolation) **not executed** by assistant and must be verified on operator environment. Unit tests do not prove HTTP 401/403 behavior.

**Owner acceptance gates:** Build+focused+regression tests, EF snapshot clean, no new migration, loopback-only Development routes, role/tenant checks and 401/403/404/409 statuses verified with distinct sessions, page/search stable, official results versioned/verified, prior Phases03–05 regression unaffected, Step04 102 units intact.

**Owner signoff Step12:** Approved 2026-10-16 by owner; focused/API checks reported successful. Actual last HTTP response not independently captured.


## Step 13 — RTL Catalog UI (2026-10-16)

**Status:** Persian RTL Next.js catalog UI and same-origin BFF committed to phase branch; TypeScript/build/test/browser acceptance pending. Step12 explicitly owner approved; operator previously identified API at 127.0.0.1:15089, Next.js at 127.0.0.1:3000, and autossh PostgreSQL at 127.0.0.1:15432.

**Routes and user experience:**
- `GET /catalog` is a server-rendered authenticated page, redirects to /identity without a valid session, and to / when development UI is disabled. Its workspace uses `dir=rtl`, `lang=fa`, localized errors/success messages, mobile responsive forms/grids, keyboard focus, bidi-isolated internal codes and Persian date display.
- Separate Goods, Services and Official Identifier tabs. Goods/Services have paged scoped code/title search, list, create, edit, soft archive; Goods type 1 vs 2, Service exclusively type 3, Revision for 409/stale edit detection. Official tab searches only active+verified reference results returned by the Step12 API.
- Selected item has mapping history, official identifier selection by code/title and kind, effective start date and one-time mapping interval close with Revision. No tax invoice creation or edit to authority reference rows.
- Customer/account scope defaults to authenticated user ID. Authorized admins can enter a customer UUID they currently own; all authorization is re-evaluated server side. Frontend selection is not a permission.
- **Base unit selector:** Step13 adds a read-only `GET /api/dev/catalog/units` endpoint (and matching same-origin Next BFF allowlist) with bounded code/title search and explicit `isActive` flag. The UI lets the user select the base unit rather than entering UUID manually. Owner-reviewed Staged `RC_UMGS.ST` units are distinctly labeled as draft, **never authority-verified**.
- BFF route `/api/catalog/[...segments]` is Next.js server-only (Development UI gate), allowlists exact intended Step12 catalog resource shapes, enforces same-origin on writes, forwards auth with HttpOnly cookie from server only, checks payload/response bounds, uses loopback origin `identityApiOrigin()`, and never exposes tokens through localStorage/JS. Direct browser API on :15089 remains loopback-limited. It does **not** add production catalog routes.

**Changed files:**
1. `apps/web/app/catalog/page.tsx`
2. `apps/web/app/catalog/workspace.tsx`
3. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogSearchService.cs` and `src/ArginTaxPayer.Api/Catalog/CatalogDevelopmentEndpoints.cs` — read-only unit search with existing role and loopback restrictions.
4. `apps/web/app/api/catalog/[...segments]/route.ts`
5. `apps/web/app/globals.css`
6. `apps/web/tests/catalog-ui.test.mjs`
7. `docs/phases/phase-06.md`

**No EF migration**; Step13 is UI-only and BFF-only. PostgreSQL writes occur only when an authorized operator uses the existing Step12 development API to create/update a disposable record.

**macOS acceptance commands:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
cd apps/web
npm run typecheck
npm test
npm run build
cd ../..
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
Browser: verify the searchable unit picker and that staged units carry a draft label; log in at `http://127.0.0.1:3000/identity`, open `http://127.0.0.1:3000/catalog`. Verify browser RTL layout (desktop/mobile), customer-scoped goods/services, empty/error/loading, search/paging, code/UUID LTR, create/edit/archive on **disposable test rows**, expected 401/no cookie and 403/wrong customer via same-origin BFF, stale Revision 409, official staged entries hidden, correct historical mapping displays, session expiration redirects after navigation. Confirm `http://127.0.0.1:15089/health/live` returns 200 before diagnosing catalog proxy. Do not publish/activate a regulatory release just to test UI.

**Acceptance caveat:** Web `npm test` static tests prove wiring, not browser accessibility/real role isolation. Production UI disabled. No CI/macOS results were captured by assistant; browser and live BFF integration require operator confirmation.

**Owner signoff Step13:** Approved by owner 2026-10-17.


### Step13 dashboard-alignment correction — owner feedback (2026-10-16)

**Reason:** The initial standalone `/catalog` page and in-page entry form were inconsistent with the established compact Users and Buyer/Seller console UI. The owner explicitly requested the same right-hand sidebar/navigation, compact dense table styles, modal create/edit, and searchable unit dropdown matching the ArginAccounting usage pattern.

**Implemented:**
- Moved `/catalog` route into `apps/web/app/(console)/catalog/page.tsx` to reuse the authenticated `ConsoleLayout`, common RTL `ConsoleShell`, breadcrumbs, navigation and sidebar. Removed the duplicate standalone `apps/web/app/catalog/page.tsx` route. The URL stays `/catalog`.
- Added `کالاها و خدمات` to `apps/web/app/components/console-shell.tsx` navigation for admin and customer roles; retained all prior sidebar items including `حساب و امنیت`.
- Product/service creation and revision-checked editing now appear in the same `argin-modal-backdrop` / `argin-modal` pattern as buyer/seller registration, not as a persistent form under the grid. Backdrop click, Escape (while not submitting), initial focus, focus loop, and cancel/close controls are included. Existing per-customer server authorization remains unchanged.
- Added standalone `apps/web/app/catalog/unit-combobox.tsx`: debounced (250ms) authenticated `GET /api/catalog/units` search, dropdown options labeled Persian title + official unit code, active/draft distinction, ArrowUp/ArrowDown/Enter/Escape selection, no manual UUID entry requirement. Read-only units endpoint was already introduced in the Step13 original scope. No changes to the official unit database.
- Added compact grid/panels, small controls, modal two-column spacing and dropdown overlay styles scoped to `.console-catalog-page` in `apps/web/app/globals.css`. Updated `apps/web/tests/catalog-ui.test.mjs` for console routing, navigation, modal and searchable dropdown.
- All existing dashboard areas, cookies and BFF fail-closed authorization remain as before. The unit selector does **not** assert authority validation for `Staged` units.

**Database / EF Migration:** NONE. This correction changes Next.js routing, frontend components, CSS and static tests only. Do not run a new `dotnet ef database update` for Step13.

**Required macOS verification:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
cd apps/web
npm run typecheck
npm test
npm run build
cd ../..
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
Open `http://127.0.0.1:3000/catalog` while signed in. Confirm navigation in right sidebar, console headings and compact grid; open new product/service form in a modal; click unit dropdown, search for `عدد` or `1627`, select via mouse/keyboard, save a *disposable* customer item and reopen for edit; verify 401/403 for missing/wrong customer session and stale revision 409. Check mobile viewport and tab/focus handling. Stop short of official publication or real invoicing.

**Owner signoff Step13:** Approved by owner 2026-10-17; this corrective change is still awaiting the operator's `npm run typecheck`, tests, build and browser acceptance. No assistant-run macOS test/DB check is claimed.


### Step13 owner refinements — split editor, scoped account name, loading, Jalali picker (2026-10-16)

**Status:** Committed in Step13; operator Next.js typecheck/build/browser acceptance pending. This is a revision of Step13, not an additional phase step.

- Reworked `apps/web/app/catalog/workspace.tsx`: desktop two-column grid with inline item create/edit form at right and compact product/service table at left (RTL); stacked vertically on smaller windows. No create/edit modal. The official-identifier tab keeps its full-width list.
- Reused the existing `ConsoleLoader` spinner as a bounded table overlay, including request-version guards so older customer/page search responses cannot overwrite current rows.
- Removed free-text customer UUID entry. Customer selection uses human-readable `companyName`/`personName` from the existing `/api/customer-owners/accounts` directory (same endpoint as other management pages); the logged-in user is the default and its first/last/company name is loaded from `/api/software-profile/me`. If the user profile lacks a name, display a neutral account label rather than revealing UUID. No alternate customer becomes the target until explicitly chosen.
- The chosen `customerUserId` is used in **both list and mutation route paths**, and customer switch clears stale rows, selected mapping, edited item and form values. The **server's existing scope** still determines permission: account menus are UI convenience, not Level1/Level2 grants. Admin Level2 can manage its currently assigned customer accounts only; an admin Level2 role by itself is **not** a Customer catalog owner. Do not silently redefine this domain or store goods under a partner user ID.
- Migrated mapping effective start/end inputs from native `datetime-local` to the actual `PersianDatePicker` adapted from `ArginAccounting/apps/desktop/src/components/forms/persian-date-picker.tsx`. The picker displays Jalali but its value is ISO Gregorian `YYYY-MM-DD`; Step11 UTC window request uses midnight UTC to avoid local timezone drift. This is **date-level** precision for UI; Step11 supports UTC instants, so sub-day windows still require a future time-enabled contract if needed.
- CSS scoped to catalog's established console panel and original UI Persian date styles. Related UI contract tests updated; no data migration, no new EF entity, no tax authority validation changes.

**Verification to run locally:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
cd apps/web
npm run typecheck
npm test
npm run build
cd ../..
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
Open `http://127.0.0.1:3000/catalog`; verify right inline form + left table at large widths, spinner while data fetches, named account dropdown, current user default, switching to a different assigned customer alters list and destination of a disposable newly created product, and switching back restores original scope; ensure wrong tenant still 403. In mapping tab test Jalali date selection and Gregorian ISO payload of the matching day, with no accidental posting to a real fiscal authority. Check TypeScript build before accepting Step13.

**Migration:** NONE. Pure Next.js/React/CSS changes to existing catalog endpoints.
**Owner signoff Step13:** Remains pending.


## Step 14 — Invoice Handoff Contracts (2026-10-17)

**Status:** Implemented and committed; operator local tests / owner signoff pending. Step13 accepted by owner without separately supplied raw build/browser logs. **No invoice issuance, live tax authority submission, signing, accounting posting, VAT rate inference or new data mutation in this step.**

### Scope and contract
- `src/ArginTaxPayer.Domain/Catalog/InvoiceCatalogHandoff.cs` defines versioned `InvoiceCatalogHandoff` (ContractVersion=1). The immutable C# positional record captures customer/item ID and item revision, customer internal code/name/kind, base unit ID, official unit code/title + release ID/document/edition/SHA256, effective-dated mapping ID/revision/from/to, official goods/service entry code/title/kind + source release ID/document/edition/SHA256, effective date and UTC capture date. `InvoiceVatResolution.Unresolved` explicitly indicates that VAT was **not** computed/cleared; phase07/08 consumers must resolve VAT policy and other invoice fields independently.
- `InvoiceCatalogHandoffPolicy` validates half-open UTC intervals `[from,to)`, type compatibility (StockItem/NonStockItem -> official Goods; Service -> official Service), item active, official ID Verified and its release Active, official unit approved+its release Active, and positive source-review evidence recorded for **both** releases. A 13-digit number or a source HTTPS URL alone is not proof of official correctness. Unknowns fail closed.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/InvoiceCatalogHandoffService.cs` offers a **read-only, internal-only** `CaptureAsync(accessToken,customerUserId,itemId,effectiveAtUtc)`. It authenticates current session, verifies current ownership/role (Level1/Level2 MFA and current commercial ownership; customer self), rechecks access under PostgreSQL RepeatableRead for a consistent source read, fetches exactly one effective mapping, verifies active reviewed official and unit releases, and returns a **detached immutable value**, not mutable EF entity references.
- A snapshot from this service must be *persisted/copied* into the future invoice line when an invoice is created; do not requery live item name, official mapping or VAT reference when rendering an existing invoice. The service intentionally does not store invoice snapshots in phase06 and does not return a ready-to-issue invoice.
- Historic mappings remain readable via Step11 history and source release references. **A fresh handoff is intentionally blocked if its current source release has since been superseded**. Previously captured invoice snapshots MUST remain unchanged and readable in future invoice storage. Replay/historic invoice rendering is a separate read of stored snapshot, NOT fresh `CaptureAsync` against current catalog.
- Current unit seed of 102 official measurement units stays staged: **fresh invoice handoff is expected to return UnitNotApproved** until genuine review and activation. Do not activate the staged seed solely to make this test green.
- This is an internal application contract; no browser/customer HTTP route, invoice table, migration or EDI/sale operation is added. Phase07/08 work will explicitly design invoice snapshot persistence, issuer, buyer, amounts, VAT resolution, immutable invoice numbering, signed submissions and legal date/time invariants.

### Changed files
1. `src/ArginTaxPayer.Domain/Catalog/InvoiceCatalogHandoff.cs` — versioned immutable DTO, error decisions and preconditions.
2. `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/InvoiceCatalogHandoffService.cs` — authenticated consistent read-only snapshot boundary, fail-closed provenance checks.
3. `tests/ArginTaxPayer.Tests/Phase06InvoiceHandoffTests.cs` — types, temporal boundaries, inactive/unverified/staged releases, VAT unresolved, DTO consistency.
4. `docs/phases/phase-06.md` — owner approval Step13 and Step14 work.

### Operator macOS tests
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06InvoiceHandoffTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```
No EF migration in Step14. Use `zsh scripts/dev-up.zsh`/`zsh scripts/dev-status.zsh` only if test DB integration is required. Never write to production or activate official units merely to fabricate sample data.

### Acceptance gates
- Local build, focused tests and full regression pass; no EF snapshot change.
- Goods/Service mismatches and missing/ambiguous/stale mapping blocked. Half-open UTC boundary verified.
- Customer wrong tenant and former Level2 partner cannot capture; MFA required for admins. Read-only RepeatableRead returns consistent mapping/version when an update races. These are **required integration acceptance tests**, not proven by current pure unit tests.
- Staged, withdrawn or unreviewed official/unit releases fail closed; no authority verification inferred from shape or source URL.
- Frozen contract contains all intended item/official/unit provenance, UTC effective time and explicit unresolved VAT; consumers cannot treat it as a complete invoice.
- Previously recorded invoice snapshots are not mutated by later changes (future invoice persistence responsibility); Phase03–05 scope regression unaffected.
- No new migration, no invoice API/numbering/financial transactions, and Step04 102 units retained.
**Owner signoff Step14:** Approved 2026-10-17; operator reported focused/full tests successful. No raw outputs attached.


## Step 15 — Security and Regression (2026-10-17)

**Status:** Tests and focused hardening committed to `phase/06-goods-services`; awaiting local build, operator session-based test evidence and owner signoff. Step14 approved by owner; no assistant-run test/CI evidence is claimed.

### Security hardening
1. `CustomerProductService.ListAsync` now rejects page offsets exceeding `int.MaxValue`, aligning with bounded `CustomerServiceCatalogService` and `CatalogSearchService`. Prevents multiplication overflow and invalid SQL OFFSET during large requests. This does not change valid pagination.
2. `CatalogSearchService.OfficialAsync` and `UnitsAsync` now share `CanReadReferencesAsync`: a live active user + current recognized Customer role, or live Level1/Level2 role **and enabled MFA**. Neither browser-controlled role strings nor stale partner memberships establish access. Tenant product/service reads remain restricted by current commercial owner or self Customer status.
3. No customer-level data is returned by `OfficialCatalogHit`; products/services continue filtering `CustomerUserId` in DB. The public reference directory remains a shared authenticated resource rather than a customer document. The source/identity gate does not imply external tax-authority verification.
4. Existing PostgreSQL trigger protections for audit immutability, reference state history, and non-overlapping mapping windows remain unchanged. No security-related schema migration was introduced.
5. Existing Next.js catalog proxy remains development-only, exact route allowlisted, cookie-to-Bearer forwarded server side with Origin check on writes, and read-only data search limits. A separate source-contract regression test records this boundary.

### Added tests and review scripts
- `tests/ArginTaxPayer.Tests/Phase06SecurityRegressionTests.cs`: pure/domain and EF metadata constraints for pagination bounds, non-reversible state machine, provenance fail-closed, historical UTC windows, tenant unique code index and restrictive FKs, baseline identity permission grants.
- `apps/web/tests/catalog-security.test.mjs`: asserts Next BFF same-origin, server-only session, protected route registration, customer-scoped query contracts. Static source checks only, **not an HTTP security proof**.
- `scripts/phase06-step15-auth-smoke.py`: **read-only real HTTP negative/positive smoke** with four *distinct actual* test sessions and two customer UUIDs. Requires explicit `PHASE06_ROOT_TOKEN`, `PHASE06_PARTNER_TOKEN`, `PHASE06_CUSTOMER_TOKEN`, `PHASE06_OTHER_TOKEN`, `PHASE06_CUSTOMER_ID`, `PHASE06_OTHER_CUSTOMER_ID`. No tokens printed, no state mutation. Requires operator fixture where Level1 and Level2 currently own first customer and unrelated user is not assigned; checks 401/403/200, scoped response rows, invalid paging/kind.
- `scripts/phase06-step15-db-guards.sh`: read-only `psql` catalog guard inspection, strictly requiring `PGDATABASE=argintaxpayer_test`, localhost tunnel `PGHOST=127.0.0.1`/`PGPORT=15432`, and other private `PG*` credentials. Read-only transaction queries existing trigger names/indexes, reference unit seed and history counts. Never applies DB migrations.
- `docs/phases/phase-06.md`: Step14 signoff and this Step15 checklist.

### Execute on macOS
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06SecurityRegressionTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
cd apps/web
npm run typecheck
npm test
npm run build
cd ../..
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```

**Read-only role smoke** (ONLY after four distinct disposable test sessions + two real test customer IDs are available; avoid storing tokens in shell history, Git or chat):
```bash
python3 scripts/phase06-step15-auth-smoke.py
```
The script refuses to run without the required environment inputs. No default credentials, no guessed fixture user IDs and no mutations. Use `python3 -m py_compile scripts/phase06-step15-auth-smoke.py` for independent syntax check. For the DB inspection, supply private PostgreSQL connection environment and run `bash scripts/phase06-step15-db-guards.sh` only after confirming it references the intended isolated test database.

### Remaining integration gates — NOT demonstrated by static/unit tests
1. Customer A, Customer B and Level2 assigned to A: test A self vs B denial; assigned Level2 vs unrelated A/B; Level1 owner vs unowned target; verify item results and mapping history cannot leak. The read-only smoke covers a subset with real session tokens.
2. **Ownership reassignment:** using disposable test customers and documented admin workflow, reassign A from Level2 partner P to partner Q, then confirm P immediately receives 403 for new reads, writes, mapping history and invoice handoff; Q granted only its current lawful scope. Repeat with a currently open browser/session (no stale privilege).
3. **MFA/session revocation:** disable MFA on isolated admin fixture, revoke active sessions, verify Level1/Level2 privileged access denied; restore fixture per owner policy. This must not be performed on the owner's real accounts.
4. **Concurrency:** disposable item/mapping records only; two simultaneous creates with identical normalized internal code should produce exactly one success and one conflict; stale Revision update denied; concurrent official mapping effective intervals must not both persist overlapping ranges.
5. **DB guard behavior:** in isolated, rollback-only PostgreSQL fixtures, verify `trg_phase06_tax_mapping_guard` denies UPDATE of identifier/source and DELETE of history, allows one permitted window close; `trg_phase06_audit_immutable` rejects audit UPDATE/DELETE. Confirm migration history/102 staged seed untouched.
6. Full Phase03–05 identity/party/owner regression and absence of public production catalog routes; confirm build and Next.js UI tests as above.

**Migration:** None. There were no added columns, entities, indexes or triggers. Do **not** run `dotnet ef database update` for Step15. No live authority activation, invoice issuance or production data changes. Successful read-only inspection does not by itself prove race-condition safety.

**Acceptance:** Operator submits focused/full .NET, Next.js and smoke results, verifies negative role cases/ownership transfer/concurrency/trigger behavior in disposable fixtures and explicitly approves Step15 before starting Step16.
**Owner signoff Step15:** Approved by owner 2026-10-17. Owner explicitly reports security/regression/HTTP Smoke/integration checks successful; raw logs have not been independently captured.


## Step 16 — Final Acceptance and Handoff (2026-10-17)

**State:** In progress. Step15 expressly owner-approved after operator-reported complete test coverage; separate macOS and Ubuntu **final aggregate run** and written owner signoff pending. No Git merge, tag or published Release has been performed or authorized during this step.

### Scope lock and handoff inventory
- Freeze the Phase06 sixteen-step roadmap; do not append new implementation steps under the guise of closeout.
- Deliver versioned goods/service reference catalog and official measurement units, dated VAT policy, staged/reference version import, Level1/MFA controlled release activation and append-only audit, tenant-owned products/services, historical fiscal-ID mapping, scoped Development catalog API, responsive Persian RTL dashboard, searchable unit picker and Jalali dates, transparent Argin branding, immutable Invoice Catalog Handoff DTO, and Step15 security/regression artifacts.
- Preserve bounded scope. **NO real tax-invoice issuance**, PDF/export, digital signing, tax-authority connectivity, billing/settlement or production catalog endpoints. Invoices in phases 07–08 must resolve tax context (VAT currently `Unresolved`) and persist a copy of the immutable catalog snapshot; never use a mutable live mapping to render an already issued invoice.
- The 102-unit `RC_UMGS.ST V1.18` reference seed remains staged until reviewed and activated by proper authority workflow; Step16 checks **must not activate it solely to satisfy an expected result**. Validity of official source is not inferred from HTTPS or a 13-digit format.

### Deliverables committed
- `scripts/phase06-step16-audit.py`: read-only cross-platform preflight verifies branch, clean checkout, exactly 16 plan rows, Step15 approval, Step16 not closed, presence of critical source/tests/identity/UI assets, and known Phase06 migration files; `--json` prints a secret-free machine-readable report. **It does not** run database tests or approve a Release.
- `docs/phases/phase-06.md`: authoritative single-file status and final acceptance matrix; no new migration, product schema or fiscal data mutation.

### Acceptance matrix
| Gate | Test / evidence | Current status |
| --- | --- | --- |
| Previous steps signed off | Steps01–15 recorded, Step15 owner explicitly confirms all categories passed | Owner-reported PASS |
| macOS .NET | `dotnet build -c Release`, full `dotnet test`, EF no-pending-model-changes | PENDING final run |
| macOS Next.js | `npm ci`, typecheck, full node tests and `npm run build` | PENDING final run |
| macOS live smoke | `dev-up.zsh` and `dev-status.zsh`; local health 200, authorized catalog navigation and RTL date/unit/owner UX | PENDING final check |
| PostgreSQL integrity | Confirm exact `argintaxpayer_test`, 102 official units, correct Phase06 migrations, required trigger presence, no destructive row change | PENDING read-only final check |
| Ubuntu 24 verify | `bash scripts/ubuntu-dev.sh verify` using **private test DB config**; API/UI live readiness separately checked | PENDING |
| Security handoff | Step15 operator report includes scoped customer/partner, MFA, sessions, race/trigger checks | Owner-reported PASS; raw evidence not stored |
| Repository audit | `python3 scripts/phase06-step16-audit.py --json` | PENDING operator execution |
| Documentation/risk register | Explicit boundary, versioned source, staged unit warning, recovery notes, deferred invoice VAT and actual authentication checks | Delivered, owner review pending |
| Git publication | Branch review, merge into `main`, annotated phase tag and GitHub Release | **NOT EXECUTED — requires final owner signoff** |

### Final macOS operator commands
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
git status --short
python3 scripts/phase06-step16-audit.py
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
(
  cd apps/web
  npm ci --no-audit --no-fund
  npm run typecheck
  npm test
  npm run build
)
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
curl --noproxy '*' -i http://127.0.0.1:15089/health/live
```
Do not publish real API credentials. Browser operator check: `http://127.0.0.1:3000/identity`, `/dashboard`, `/catalog`; verify account names/ownership, two-column form+table, search unit picker, upward-opening Jalali calendar, transparent logo/favicon, and unauthenticated 401/authorized 200/foreign tenant 403 on the appropriate test routes. **Do not re-run mutating fixtures against owner/production catalogs.** Step15's role smoke uses disposable sessions and is read-only.

### Ubuntu VPS test (separate environment)
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
python3 scripts/phase06-step16-audit.py
bash scripts/ubuntu-dev.sh verify
bash scripts/ubuntu-dev.sh status
```
`ubuntu-dev.sh verify` runs build and tests but does not start the services; status may correctly say both services are stopped. If final live HTTP is required and no test API/web processes are active, with the approved private `~/.argintaxpayer-test/ubuntu.env`, run `bash scripts/ubuntu-dev.sh up` followed by `bash scripts/ubuntu-dev.sh status`; verify remote environment remains VPS loopback-only and do not confuse `verify` with live browser login. After operator checks, `bash scripts/ubuntu-dev.sh down` stops test launchers. No production deployment is implied.

### Risks / deferred commitments
1. Activation audit reflects documented operator review, **not cryptographic confirmation by tax authority**. Avoid activating unverified goods identifiers or staged units.
2. The current Step08 emergency withdrawal does not automatically restore a previously superseded release; follow a reviewed reimport/restore design before production use.
3. Step12 catalog HTTP and Step13 UI are deliberately Development-only. Production exposure requires separate API security, auth/session and deployment gate in a future phase.
4. Step14 InvoiceCatalogHandoff is an **in-memory read-only contract** and invoice snapshot persistence is deferred to Phases07–08. No VAT or tax-invoice correctness claim until rules and authority integration are implemented.
5. Migration Down operations for append-only audit/mapping references intentionally refuse destructive rollback when historical records exist. Recovery means forward repair, reviewed change and validated backup, not deleting history.
6. UI relies on selected customer identity plus server ownership checks; account list choices grant no privileges.
7. No isolated proof of full production performance/penetration or signed regulatory receipt is supplied by Phase06.

### Closeout conditions
- Operator sends final macOS and Ubuntu results, including any failures and observed DB/HTTP/browser findings.
- Resolve all release-blocking defects; no unfinished `Step16` checklist or unknown migration drift.
- Owner explicitly authorizes Phase06 completion. **Only then** prepare and perform separately requested main branch merge, annotated tag, GitHub Release and immutable closeout record; do not assume these actions occurred from a successful test run alone.

**Owner signoff Step16:** PENDING. **Final phase state:** OPEN; awaiting final operator evidence and release authorization.

## Phase Closeout

Not started.


### Step13 visual refinement — screenshot review (2026-10-16)

**State:** Updated on phase branch; macOS build/browser signoff remains pending.
- The screenshot showed the input form crossing into the table because the obsolete `.catalog-form-dialog` modal styling still forced `width:calc(100vw - 28px)` in a much narrower CSS Grid cell. Replaced it with `width/max-width:100%`, `min-width:0`, border-box sizing and grid-specific containment. Table now uses compact fixed column allocation and wraps long identifiers/actions within its own panel. Mobile stacks both panels.
- Account selector now excludes active accounts with empty, whitespace-only or missing `companyName` and `personName`. The logged-in account remains the default regardless of its own optional profile display name; no unrelated UUID/nameless customer rows are offered.
- ArginAccounting-derived Persian date picker popover displays above its input via `bottom:calc(100% + 7px)` and `top:auto`, preserving month navigation and ISO contract.
- `apps/web/tests/catalog-ui.test.mjs` includes source regressions for layout containment, named customers, and upward calendar.
- No .NET, database, or EF schema changes; **no Migration required**.

**Verification:** `cd apps/web && npm run typecheck && npm test && npm run build`, then `zsh scripts/dev-up.zsh`, open `http://127.0.0.1:3000/catalog`. At desktop viewport verify neither panel overlaps, controls fit within form, account dropdown only displays named customers, and Jalali calendar opens upward. Step13 remains pending owner's approval.


### Step13 visual identity branding (2026-10-17)

**Owner request:** Apply the supplied green Argin logo as app and favicon imagery across the web login and dashboard, preserving existing functional flows.
- Saved a 128x128 optimized PNG derived from the owner's supplied image at `apps/web/public/brand/argin-logo.png`. The asset is a faithful downsample and uses a white image background as supplied; the repository does not yet include a vector master or higher-resolution brand kit.
- Mirrored the same logo at `apps/web/app/icon.png`, `apps/web/public/favicon.png` and `apps/web/public/apple-touch-icon.png`. The central metadata `apps/web/app/layout.tsx` explicitly sets the shortcut, standard browser icon and Apple icon (versioned URL to avoid stale favicon caches). No external image CDN.
- Updated `apps/web/app/identity/page.tsx` with a priority-loaded brand image in place of the old Persian glyph; updated `apps/web/app/components/console-shell.tsx` to use the same icon in the right sidebar and header account trigger. No changes to route guards, sessions, accounting or invoicing.
- Scoped `argin-brand-logo` sizing and border styling in `apps/web/app/globals.css`. Added `apps/web/tests/branding.test.mjs` to verify files exist and the intended components reference them.
- **Migration:** none. Changes are static PNG, Next.js metadata, UI and tests.
- **macOS operator verification:** `git fetch origin && git switch phase/06-goods-services && git pull --ff-only`; `cd apps/web && npm run typecheck && npm test && npm run build`. Restart `zsh scripts/dev-up.zsh` if needed; inspect `/identity`, `/dashboard` and `/catalog`, then refresh browser favicon cache if the previous icon remains.
- **Approval:** Step13 remains pending operator build/browser acceptance; no assistant-run macOS compile/CI claim.


### Step13 transparent logo and dashboard hero replacement (2026-10-17)

**Owner request:** Keep the uploaded dashboard screenshot untouched; change program assets so the owner-supplied Argin logo has a transparent *exterior* across all application branding and replaces the obsolete large "آ" icon in the dashboard welcome card.

**Implementation:** Converted the owner-supplied image into a transparent PNG (exterior alpha, white internal glyph preserved), with an optimized 128×128 icon asset. Replaced the binary files at `apps/web/public/brand/argin-logo.png`, `apps/web/app/icon.png`, `apps/web/public/favicon.png`, `apps/web/public/apple-touch-icon.png`. The existing Next Image usages in login, sidebar and account header now share the new transparent source file. Replaced `app-welcome-graphic` glyph in `apps/web/app/(console)/dashboard/page.tsx` with `Image` and updated CSS to suppress the old teal glyph tile, card-icon background, logo borders and opaque backing. Updated favicon URL cache-busting metadata and `apps/web/tests/branding.test.mjs` for alpha, all paths and hero asset contract.

**Scope:** Only web PNG assets, layout metadata, React and CSS; no API, identity logic or EF schema changed. **No migration required.**

**Operator validation:** `git fetch origin && git switch phase/06-goods-services && git pull --ff-only`, then `cd apps/web && npm run typecheck && npm test && npm run build`. Restart the dev services if needed, inspect `/identity` and `/dashboard` and browser tab favicon; hard-refresh/favicon cache may delay propagation. Step13 remains pending final browser acceptance.
