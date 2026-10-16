# ArginTaxPayer — Phase 06: Goods and Services

**Status:** Step 01 approved by owner conditional on ArginAccounting interoperability; Phase16 contract review completed. Step 02 OWNER APPROVED 2026-10-16 (operator confirmation; logs not independently received). Step 03 implementation committed; pending macOS PostgreSQL/schema verification and owner approval. Steps 04–16 not started.
**Branch:** `phase/06-goods-services`
**Baseline:** `main` after Phase05 merge `6a3de74692403029b68c2061251601752eebd518`.
**Roadmap:** `docs/roadmap/18-phases.md` Phase 06.
**Policy:** This is the single authoritative evolving phase plan. Step count and scope are frozen pending dated owner-approved amendment.

## Objective and Architecture
Build a versioned official goods/services reference catalog (tax identifiers, official unit codes, VAT classification), tenant/customer-owned product and service catalogs, audited mappings, searchable scoped APIs and Persian RTL management UI. Layers: Domain rules; Application commands/queries; Infrastructure EF Core/PostgreSQL/import; ASP.NET Core API; Next.js UI. Preserve Phase03 identity, Phase04 customer/partner ownership and Phase05 parties; customer ownership governs authorization, not legacy taxpayer-level grants. Reference data is shared read-only to tenants; tenant catalogs are private. Keep official identifiers separate from internal product IDs. Require provenance, effective dates and source-version attribution; no assumed universal VAT rate. No live authority connectivity, invoice issuance, signing, billing or financial postings in this phase.

## Fixed 16-Step Plan
| Step | Title | Deliverable | Acceptance | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Architecture and Contracts | مدل دامنه، ADR، API، مجوزها و مرز فازها | بازبینی قرارداد و عدم تداخل با فازهای قبلی | Phase05 | Owner approved 2026-10-16, ArginAccounting compatibility amendment |
| 02 | Persistence and Migrations | جداول مرجع و کاتالوگ اختصاصی، EF migration افزایشی | DB test-only، قیود و rollback review | 01 | Owner approved 2026-10-16; test logs not independently captured |
| 03 | Official Identifier Model | شناسه رسمی کالا/خدمت و منشأ/اعتبار آن | شناسه نامعتبر/تکراری و ادعای رسمی بدون منبع رد شود | 02 | Implemented; awaiting operator tests and owner signoff |
| 04 | Official Unit Codes | مرجع کدهای رسمی واحد و نگاشت به واحد داخلی | واحد ناشناخته و نگاشت ناسازگار رد شود | 03 | Pending |
| 05 | VAT Classification | طبقه‌بندی VAT و نرخ/معافیت وابسته به تاریخ و مرجع | عدم فرض نرخ ثابت و تست تاریخ‌های مرزی | 03–04 | Pending |
| 06 | Versioned Reference Catalog | نسخه منبع، بازه اعتبار، snapshot و عدم تغییر تاریخچه | نسخه‌های پیشین قابل بازیابی؛ همپوشانی کنترل شود | 03–05 | Pending |
| 07 | Reference Import Pipeline | ورود فایل رسمی با staging، preview و اعتبارسنجی | Import تکراری idempotent و خطاها گزارش شوند | 06 | Pending |
| 08 | Reference Activation and Audit | انتشار کنترل‌شده نسخه و audit/rollback منطقی | نسخه فعال بدون حذف تاریخی جابه‌جا شود | 07 | Pending |
| 09 | Customer Product Domain | کالاهای اختصاصی، وضعیت و شناسه داخلی | تفکیک مشتری و جلوگیری از حذف ارجاعات | 02–06 | Pending |
| 10 | Customer Service Domain | خدمات اختصاصی و قواعد نوع خدمت | تست CRUD، مالکیت و ناسازگاری نوع | 09 | Pending |
| 11 | Tax Identifier Mapping | اتصال کالا/خدمت مشتری به شناسه رسمی با تاریخچه | نگاشت نامعتبر یا متعلق به نوع اشتباه رد شود | 08–10 | Pending |
| 12 | Catalog API and Search | API جست‌وجو، فیلتر، صفحه‌بندی، ETag/concurrency | تست نقش‌ها، paging و stale updates | 09–11 | Pending |
| 13 | Persian RTL Catalog UI | فهرست، فرم، جست‌وجوی انتخابی و خطاهای فارسی | تست مسیرهای مجاز، اعتبارسنجی و دسترس‌پذیری | 12 | Pending |
| 14 | Invoice Handoff Contracts | قرارداد snapshot برای فاز ۷/۸ بدون صدور فاکتور | تست قرارداد نسخه و عدم تغییر اطلاعات تاریخی | 06,11–13 | Pending |
| 15 | Security and Regression | تست isolation، race، migration و رگرسیون فازهای ۳–۵ | منع cross-customer و former-partner access | 01–14 | Pending |
| 16 | Final Acceptance and Handoff | تست تجمیعی Mac، Ubuntu، مستند و گزارش ریسک | گزارش شواهد، تأیید مالک؛ Merge/tag جداگانه | 15 | Pending |

## Engineering and Safety Invariants
1. Additive, reviewed migrations only; confirm `current_database() = 'argintaxpayer_test'` before any DB change; no automatic migrations or production DB writes.
2. Never invent or claim tax-authority validation. Official source imports require traceable provenance and version; unknown classification must be explicitly unresolved.
3. Historic catalog versions, mappings and future invoice snapshots are immutable/auditable; no retroactive tax-rate overwrite.
4. Server-side authorization and database query scoping for Root, Level2 and customer; preserve prospective commercial ownership transfer semantics.
5. No secrets, real certificates, private keys or production dumps in the public repository.
6. Each step: focused automated tests, negative cases, paths, commit SHA, migration impact, CI/Mac/Ubuntu evidence and explicit owner approval recorded in this file. No next step before approval.
7. Step 16 final Mac regression and Ubuntu `bash scripts/ubuntu-dev.sh verify`, then loopback-only `up/status/down`; early Ubuntu checks only when schema/infrastructure/platform risk requires.
8. Local runner: `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh`, `zsh scripts/dev-down.zsh`. Runner manages owner-approved AutoSSH `127.0.0.1:15432` tunnel to test PostgreSQL. No manual plain SSH tunnel.
9. Main merge, tag, release and deployment are separately approved gates after phase acceptance.

## Step Execution Record
Step 01 approved by owner 2026-10-16; Step02 approved by owner 2026-10-16. Step03 implementation pending acceptance; Steps04–16 not started. For each approved step append: design decisions, changed paths, tests and outputs, DB check, risks, commit, owner approval and date.


## Step 01 — Architecture and Contracts (2026-10-16)

**Status:** Design and contract baseline recorded; awaiting explicit owner signoff. No runtime functionality was implemented. No claims of completed builds, tests, CI or DB verification.
**Reviewed baseline files:** `docs/roadmap/18-phases.md`, `docs/phases/phase-04.md`, `docs/phases/phase-05.md`, `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Parties/Party.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerProfile.cs`, `src/ArginTaxPayer.Api/Program.cs`, `src/ArginTaxPayer.Api/Parties/PartyDevelopmentEndpoints.cs`.
**Existing architecture confirmed:** EF DbContext maintains distinct Party, CustomerCommercialOwner, TaxpayerProfile and customer memberships. TaxpayerProfile.CustomerUserId can be null for unresolved legacy data. Existing party API is explicitly loopback-only Development; no production authorization maturity may be assumed from these endpoints.

### Design decisions (Phase06 ADR inline)
1. **Reference data vs customer catalog:** Official reference rows are globally readable *only to authenticated authorized tenants* and editable through privileged, audited administrative import/activation. Customer items are scoped by immutable `CustomerUserId` (account ownership). Do not key catalog ownership by Party, fiscal-memory ID or Level2 partner ID. A customer's many taxpayers can consume its catalog subject to future invoice/entitlement authorization.
2. **Separate identities:** `CatalogItemId` is a stable UUID, customer `InternalCode` is not an official tax identifier, and `OfficialGoodsServiceCode` is a source-versioned regulatory identifier. Never infer an official match from item name or party identifiers. Product and Service are explicit discriminated kinds; do not silently convert kind on edit.
3. **Reference model:** `OfficialCatalogSource` (issuer, source URI/reference, fetched/imported timestamp, content checksum, declared edition, legal effective interval, trust/verification status); `OfficialCatalogRelease` (source, version, state Draft/Validated/Active/Superseded/Rejected, activated-at, actor, checksum); `OfficialGoodsServiceEntry` (release, official code, kind, title, valid interval, source row); `OfficialUnitEntry` (release, official code, title/precision where evidenced); `VatClassificationEntry` (release, classification, documented legal basis, applicability/exception, effective interval, nullable rate). These are planned conceptual entities, not existing EF classes or asserted authority schema.
4. **Tenant model:** `CustomerCatalogItem` (UUID, CustomerUserId, Product|Service, normalized customer code, localized name, active/archived status, revision, UTC timestamps); `CustomerItemOfficialMapping` (customer item, official entry/release, official unit if applicable, valid-from/to, evidence, actor and audit). Explicit mapping required; unknown/unverified official code is never represented as authority-approved. Pricing/stock/invoice financial computation is outside Phase06.
5. **Versions and effective time:** Reference releases are immutable once activated. Store UTC instants and explicit official effective periods independently from import time. Use half-open `[from,to)` intervals. Rate applicability is resolved by legal effective time and relevant invoice date in future phases, not `DateTime.Now`, and no universal 10%/other VAT assumption is allowed. Missing or ambiguous classification returns `unresolved` and blocks confirmed official mapping; it is not automatically zero-rated. An archived customer item remains historically addressable.
6. **Constraints and concurrency:** Plan unique `(CustomerUserId, normalized InternalCode)` for active items, unique `(releaseId, kind, officialCode)` for official entries, FK RESTRICT for history, revision-based optimistic concurrency, deterministic imports keyed by source+release checksum and explicit conflict handling. Do not silently reuse old codes after archive without a reviewed policy. Review optional unit applicability and duplicates before Step02 physical schema.
7. **Authorization:** Level1 can administer its own root customers under existing scope; Level2 can manage only *currently* commercially owned customer catalogs using live `CustomerCommercialOwner` joins; customer may manage only its own catalog subject to existing session and approved self-service capability. Former partner loses access immediately on ownership transfer. Reference release activation is Level1 privileged operation requiring audit and appropriate MFA; Level2/customer cannot edit global reference data. Super Admin rights, if relevant, require explicit existing policy rather than implied bypass.
8. **Safe API surface:** Do not expose new unaudited production routes. Phase12 adds actual API handlers, with explicit Dev loopback scope until independent production authorization review. Reuse bearer-session resolution, ownership policy and per-resource access checks; all writes validate command ownership in transaction. Distinguish 400 invalid, 401 unauthenticated, 403 forbidden, 404 not found, 409 version/conflict and 422 unresolved official classification (when semantically applicable), with stable error codes and no secret leakage.
9. **Invoice handoff:** Future invoice line snapshot must carry `catalogItemId`, kind, customer-visible name/code, official identifier, official release/source version, unit code/version, VAT classification/legal-effective-date provenance, and resolved-or-unresolved outcome. Snapshot must be immutable once invoice is finalized; Phase06 does NOT create invoices or calculate invoice totals.
10. **Import and provenance gate:** Import only verifiable, appropriately licensed official datasets, retaining provenance and checksum. An uploaded file is *unverified* until a privileged operator checks the issuing source, format/version, duplicates and classification; no fabricated authority connectivity. Re-import same content is idempotent, new content creates a release; rollback means changing active pointer with audit, not deleting prior records.

### Proposed API contracts (design only; not implemented)
Base routes in authenticated, loopback-only Development environment initially:
- `GET /api/dev/reference/goods-services?query=&kind=&releaseId=&page=&pageSize=` and `GET /api/dev/reference/units`, `GET /api/dev/reference/vat-classifications`: scoped reference read with version and applicability metadata.
- `POST /api/dev/reference/releases/import-preview`, `POST /api/dev/reference/releases/{releaseId}/validate`, `POST /api/dev/reference/releases/{releaseId}/activate`: privileged operator-only staging/validation/activation; audit and idempotency.
- `GET /api/dev/customers/{customerId}/catalog-items?kind=&query=&status=&page=&pageSize=`; `POST /api/dev/customers/{customerId}/catalog-items`; `GET /api/dev/customers/{customerId}/catalog-items/{itemId}`; `PUT /api/dev/customers/{customerId}/catalog-items/{itemId}`; `POST /api/dev/customers/{customerId}/catalog-items/{itemId}/archive`.
- `POST /api/dev/customers/{customerId}/catalog-items/{itemId}/official-mappings`; `GET /api/dev/customers/{customerId}/catalog-items/{itemId}/official-mappings`. Requests include `expectedRevision` where mutable; responses include current revision and regulatory provenance.

Illustrative DTO shapes (field contract, not compiled code):
```text
CatalogItemWrite { kind, internalCode, displayName, expectedRevision? }
OfficialMappingWrite { officialEntryId, officialReleaseId, unitEntryId?,
                       effectiveFromUtc, effectiveToUtc?, evidenceReference,
                       expectedRevision }
CatalogItemRead { id, customerUserId, kind, internalCode, displayName,
                  status, revision, officialMappingStatus }
OfficialReferenceRead { releaseId, sourceId, code, kind, title,
                        effectiveFromUtc, effectiveToUtc?, trustStatus }
```
Pagination: bounded page size (proposal 1..100), deterministic sort/id tie-breaker. No unbounded enumeration. No cross-customer query parameter bypass.

### Role and operation matrix (design target)
| Operation | Level1 | Level2 | Customer |
|---|---|---|---|
| Read official reference | Authenticated / scoped | Authenticated / scoped | Authenticated / scoped |
| Import/validate/activate reference release | Privileged Level1 with MFA + audit | Denied | Denied |
| List/read own customer catalog | Own root customers | Currently assigned customers only | Own account only |
| Create/edit/archive customer catalog item | Own root customers | Currently assigned customers only, subject to existing policy | Own account, gated by capability |
| Create item-to-official mapping | Own root customers | Currently assigned customers only | Own account, gated by capability |
| Read historical catalog/mapping data | Own scoped customer | Current scoped customer only | Own account only |
| Read catalog of unrelated or former partner customer | Denied | Denied | Denied |

No taxpayer-grant-based Level2 authorization. Root administrative oversight must use explicit existing root ownership policies; no unexplained global leakage.

### Step01 acceptance checklist
- [x] Read the official Phase06 roadmap, Phase04/05 compatibility rules and existing relevant source files.
- [x] Explicitly define domain entities, identifiers, version semantics, authorization matrix and API DTO/route contract.
- [x] Establish non-destructive migration handoff and reference/provenance gating for Steps02–08.
- [x] Define negative-path and cross-phase regression expectations.
- [ ] Operator/owner review of reference-issuer datasets, code formats and any legal/source licensing questions before production ingestion.
- [ ] Owner approval to close Step01 and authorize Step02.

### Step01 design-level verification matrix (NOT executed)
| Case | Expected result | Owner test stage |
|---|---|---|
| Duplicate internal code within same customer | Conflict | Step09/12 |
| Identical internal codes for two different customers | Allowed, isolated | Step09/15 |
| Unknown/unverified official code or VAT class | Unresolved; never falsely approved/zero-rated | Step03/05/11 |
| Duplicate reference import with identical checksum | Idempotent; no duplicate active release | Step07/08 |
| Reference release changes | Historical reference and mapping remain stable | Step06/08/14 |
| Level2 requests former customer's catalog after transfer | Forbidden/not found, no data leak | Step12/15 |
| Customer changes another customer's item by guessed GUID | Denied | Step12/15 |
| Stale expectedRevision writes | Conflict without partial mutation | Step09/10/12 |
| Cross-kind service/product official mapping | Rejected | Step11 |
| Unverified migration DB target | Abort without changes | Step02 |

**Files changed in Step01:** `docs/phases/phase-06.md` only.
**Database/migration:** None; no SQL executed; no existing data modified.
**macOS:** No build/unit/API execution; design-only step.
**Ubuntu:** Deferred to Step16; no infrastructure or schema changes.
**CI:** Not checked.
**Residual design risks:** Public official data availability/licensing and version identification, lawful VAT classification/exemptions, exact regulated unit-code source and compatibility, future customer self-service entitlement feature gating, potential existing product terminology elsewhere in repository. These must be resolved at the corresponding implementation step without inventing values.
**Owner acceptance:** Pending.


## Owner-approved scope clarification — Official measurement-unit seed and Level1-only reference-code administration (2026-10-16)

**Applies within existing fixed sixteen steps (no renumbering):** Step02 designs additive reference tables, idempotent seed/import status and uniqueness checks; Step03 implements official goods/service identifier code validation and Level1-only reference-code changes; Step04 populates the complete official measurement-unit catalog; Step06 retains immutable source/release versions; Steps07–08 implement provenance-gated import and publishing; Step12 enforces authorization on all mutating APIs; Step13 offers scoped read-only reference selection and Level1-only administration UI; Step15 verifies security and replay safety.

**Documented source:** Owner-provided `RC_IITP.IS V7.9` (1405) specifies invoice field `mu` as numeric-string max 8 and references the *separate* `RC_UMGS.ST` official units-of-measure catalog from the tax authority. `RC_IITP.IS` itself is NOT an authoritative full units table. A publicly found secondary copy `RC_UMGS.ST_V1.4` is dated Ordibehesht 1402; treat as an archival candidate, **not** as verified current official release. Do not claim that it is up-to-date. Seed **all** rows only after obtaining and verifying the latest official source file/version (or owner-supplied authenticated copy), row count and checksum. No guessed/incomplete seed and no silent legacy fallback. Example unit labels/codes appearing in secondary published tables, not independently current-verified: عدد=1627, لیتر=1637, متر مربع=1645.

**Three code namespaces MUST remain separate:**
1. `OfficialUnitCode`: regulatory measurement unit referenced by invoice `mu`. Canonical text (preserves zeros), numeric ASCII, maximum 8 digits per owner-provided specification; explicit release/version and activation state. Customer/Level2 cannot create or change.
2. `OfficialGoodsServiceCode`: official tax goods/service identifier referenced by invoice `sstid`, independent of unit. New/custom official code candidates may be entered/edited only by Level1, and remain `Unverified` until official provenance checks; never claim official validity solely because an admin entered it.
3. `CustomerCatalogItem.InternalCode`: customer's local SKU/item code, NOT an official code; management remains scoped to the customer workflow under the Phase06 authorization matrix. A request to restrict *all* customer-internal codes would be a separate owner-approved policy change, because it would affect customer catalog CRUD.

**Unit-seed acceptance policy:**
- Checked-in machine-readable seed source with declared document identifier, version, issuer, source URL or owner-supplied verified document fingerprint, SHA256 checksum, row count, normalization policy, acquisition date; no secrets.
- Explicit database migration/seeding command for `argintaxpayer_test` only after checking `SELECT current_database()`; no migration or seed at startup. Idempotent upsert or versioned staging under transaction, no duplicate codes within a release; no destructive overwrite of referenced released rows.
- After seeding, compare imported count and each `(code,name)` against verified source; forbid empty/partial seed falsely marked Active. Existing valid rows and invoice snapshots unchanged.
- Level1-only APIs for staging/custom entries, approving, altering or deactivating reference units/official product-code candidates; enforce existing Level1 session and MFA where required server-side, not only disabled UI controls; include actor, reason, revision, timestamp, before/after audit. Official releases immutable: correction creates a new revision/release rather than rewriting historical code.
- Level2/customer have read/search/select only. `403` for attempted write, `401` without authentication, `409` for duplicate or stale revision; unknown/unverified unit mapping cannot be used as verified official invoice code.
- Step15 negative tests: Level2/customer POST/PUT/PATCH/DELETE rejected even with forged or guessed IDs; direct/indirect mutation denied; concurrent Level1 updates CAS-safe; idempotent rerun equal row count; edition changes preserve historic semantics.
- **No DB action executed in this amendment.** Actual seed and CRUD implementation are Step02–04/12–13 deliverables, each gated by owner approval and source-file verification. Do not advance to Step02 until Step01 is explicitly signed off.


## ArginAccounting interoperability baseline and Step02 completion record (2026-10-16)

### Step01 conditional signoff and architecture correction
Owner approved Step01 provided compatibility with ArginAccounting. Reviewed owner-provided *ArginAccounting Phase16 — Products & Services* specification. Binding contract:
- `ItemKind` uses three compatible categories: `STOCK_ITEM`, `NON_STOCK_ITEM`, `SERVICE` (persisted values 1, 2, 3). Fiscal reference `OfficialGoodsServiceEntry.Kind` is a **separate** two-state goods/service classification; stock vs non-stock must never leak as a tax classification.
- Every customer item including SERVICE has a base measurement unit; per-item conversion is exact rational numerator/denominator with positive denominator and explicit rounding policy, not floating point. Do not imply that a carton has universal fixed size. Effective-date conversion history must remain stable.
- ArginAccounting's `CompanyId` scopes accounting Item. Here `CustomerUserId` scopes customer master data: integration in Phase13 requires an explicit company/taxpayer-to-customer mapping, never equality of these UUIDs. Tenant item code is stable per customer. Financial balances, FIFO, COGS, purchase/sales prices, warehouse inventory and AccountRole posting do not belong to this phase.
- Item states map conceptually to ACTIVE/INACTIVE/BLOCKED/DISCONTINUED. A tax profile is a versioned reference, not a mutable embedded tax response. Preserve invoice-line snapshot immutability for Phase07–08/13.
- This is the **compatibility contract**, not a claim that ArginTaxPayer already supports ArginAccounting inventory, barcode or category features. Those remain integration/other-phase concerns.

### Step02 — Persistence and Migrations
**Status:** Implementation committed to `phase/06-goods-services`; awaiting locally executed tests, safe database application and owner acceptance.

**Changed files:**
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogEntities.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogModelConfiguration.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016120000_AddPhase06CatalogFoundations.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`
- `tests/ArginTaxPayer.Tests/Phase06CatalogPersistenceTests.cs`

**Entities / tables:** `CatalogReferenceRelease` / `catalog_reference_releases`; `OfficialMeasurementUnit` / `official_measurement_units`; `OfficialGoodsServiceEntry` / `official_goods_service_entries`; `VatClassificationEntry` / `vat_classification_entries`; `CustomerCatalogItem` / `customer_catalog_items`; `CatalogItemUnitConversion` / `catalog_item_unit_conversions`; `CatalogOfficialMapping` / `catalog_official_mappings`.

**Integrity:** unique (source document, SHA256) releases; unique official code per release; per-customer unique internal code; revision-concurrency; explicit customer identity FK; base unit FK; strict RESTRICT history FKs; positive exact ratios; UTC effective intervals; nullable VAT rate and legal-basis metadata. Domain operations (official source verification, overlapping-period guard, kind mapping, Level1 authorization, data seed) are **not** implemented here and remain the designated Steps03–13; schema checks alone do not enforce those business rules.

**Migration:** `20261016120000_AddPhase06CatalogFoundations` adds seven tables, indexes and PostgreSQL constraints; does not alter Phase03–05 tables or backfill existing data. `Down` drops all seven tables and is destructive after first insert; never use on retained data without separate approval. Step04 official seed awaits a verified complete `RC_UMGS.ST` release; no fabricated default codes seeded in Step02. EF snapshot is aligned through the shared mapping configuration rather than auto-generated inline entity configuration; validate using `dotnet ef migrations has-pending-model-changes` or equivalent before signoff.

**Developer macOS verification (run from updated branch):**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~Phase06CatalogPersistenceTests
```
Use canonical environment launchers only when UI/DB smoke needed:
```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
Before ANY explicit migration, verify `SELECT current_database()` against the test connection with the owner-approved Npgsql parsing/psql approach in the roadmap, and confirm precisely `argintaxpayer_test`. Review generated SQL and pending EF migrations before applying to the test database. After explicit application, verify `__EFMigrationsHistory`, seven tables, check/unique/FK restrictions and rollback-only integrity probes. No production database operation. Run `zsh scripts/dev-down.zsh` when finished.

**Targeted acceptance matrix:**
1. Release build and all five model metadata tests pass.
2. The DB name is exactly `argintaxpayer_test` and migration runs only after explicit authorization.
3. Seven catalog tables and FK/unique/check constraints exist; no Phase03–05 tables modified.
4. Same tenant duplicate internal code fails; another tenant may reuse code; invalid unit strings and denominator zero fail.
5. FK RESTRICT prevents destroying referenced unit/release/item history.
6. Duplicate release checksum cannot create second source release.
7. Snapshot/model comparison has no unexpected pending changes.
8. Tests confirm no auth routes, invoicing, VAT defaults, inventory postings or official seed were prematurely activated.

**Test evidence:** GitHub code changes committed; .NET build **not executed by assistant**, tests **not executed**, PostgreSQL test DB **not connected/migrated**, macOS operator acceptance **pending**, Ubuntu **deferred** pending schema-specific verification and final phase acceptance, CI **not checked**. This is not a passed test claim.

**Known limitations and follow-up:** Reference issuer verification and the complete official unit import are Step03/04/07/08; Level1 write authorization Step12/13; no customer catalog API yet; per-company ArginAccounting integration and historical conversion business rules require explicit later checks. Evaluate any database/provider model snapshot differences during local tests; fix before calling Step02 owner-approved.

**Owner approval Step02:** Pending local test evidence and explicit signoff.


## Step 03 — Official Goods/Service Identifier Model (2026-10-16)

**Status:** Implementation committed, acceptance and runtime verification pending. Step02 owner approved per chat (no original test output independently captured).
**Compatibility:** `ArginAccounting` Phase16 separates customer/company item code and a versioned TaxProductProfileRef; ArginTaxPayer likewise preserves CustomerCatalogItem.InternalCode independently of official `sstid`. ArginAccounting item kind STOCK_ITEM/NON_STOCK_ITEM both map to regulatory Goods; SERVICE maps to regulatory Service. This is not a claim that the systems share company IDs, code namespaces or inventories.
**Regulatory source:** Owner-provided `RC_IITP.IS V7.9` table 29 describes invoice `sstid` as a required 13-character numeric-string field. Application normalizes ASCII, Persian and Arabic-Indic digits to 13 ASCII digits, keeping leading zeroes. Structural validity is NOT existence in the official authority catalog, nor tax/VAT approval. Invalid code, separators, control characters and unsupported digit forms are rejected.

**Changed files:**
- `src/ArginTaxPayer.Domain/Catalog/OfficialGoodsServiceIdentifierPolicy.cs` — deterministic, culture-independent formatting, title validation, explicit verification policy and compatible kind mapping.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/OfficialIdentifierCandidateService.cs` — internal (not HTTP mapped) Level1+active MFA-only creation of candidate identifiers on staged reference releases, scoped session checks again within Serializable transaction, reject duplicates, always mark candidate Unverified.
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogModelConfiguration.cs` — check constraint on exactly 13 ASCII digits.
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016150000_ValidateOfficialGoodsServiceIdentifiers.cs` — additive PostgreSQL CHECK on existing table; no data update, and fails safely if existing invalid rows.
- `tests/ArginTaxPayer.Tests/Phase06OfficialIdentifierTests.cs` — format, leading zeros, Unicode digit conversion, invalid input, verification gates, ArginAccounting kind mapping, uniqueness metadata.
- `docs/phases/phase-06.md` — status, test and acceptance record.

**Privilege contract:** Level2 and customer must not author official identifiers. Candidate service uses current IdentityUser activity, live Level1 role and enabled MFA; no public endpoint or candidate self-approval. Real authority verification, source provenance, release publishing and historical editing/auditing live in Steps06–08/12. No code may be marked Verified merely through admin entry. The service only supports *creation* of a candidate; changes to existing official records require immutable release/revision workflow in later steps. No product catalog CRUD in this step.

**Data/migration:** `20261016150000_ValidateOfficialGoodsServiceIdentifiers` adds `ck_official_goods_code_format` on `official_goods_service_entries.code`. It only introduces a constraint; no writes to prior phase tables, no seed, no destructive operation. It requires an explicitly reviewed migration and checked current DB `argintaxpayer_test`. Before applying, check any existing rows that do not match `^[0-9]{13}# ArginTaxPayer — Phase 06: Goods and Services

**Status:** Step 01 approved by owner conditional on ArginAccounting interoperability; Phase16 contract review completed. Step 02 OWNER APPROVED 2026-10-16 (operator confirmation; logs not independently received). Step 03 implementation committed; pending macOS PostgreSQL/schema verification and owner approval. Steps 04–16 not started.
**Branch:** `phase/06-goods-services`
**Baseline:** `main` after Phase05 merge `6a3de74692403029b68c2061251601752eebd518`.
**Roadmap:** `docs/roadmap/18-phases.md` Phase 06.
**Policy:** This is the single authoritative evolving phase plan. Step count and scope are frozen pending dated owner-approved amendment.

## Objective and Architecture
Build a versioned official goods/services reference catalog (tax identifiers, official unit codes, VAT classification), tenant/customer-owned product and service catalogs, audited mappings, searchable scoped APIs and Persian RTL management UI. Layers: Domain rules; Application commands/queries; Infrastructure EF Core/PostgreSQL/import; ASP.NET Core API; Next.js UI. Preserve Phase03 identity, Phase04 customer/partner ownership and Phase05 parties; customer ownership governs authorization, not legacy taxpayer-level grants. Reference data is shared read-only to tenants; tenant catalogs are private. Keep official identifiers separate from internal product IDs. Require provenance, effective dates and source-version attribution; no assumed universal VAT rate. No live authority connectivity, invoice issuance, signing, billing or financial postings in this phase.

## Fixed 16-Step Plan
| Step | Title | Deliverable | Acceptance | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Architecture and Contracts | مدل دامنه، ADR، API، مجوزها و مرز فازها | بازبینی قرارداد و عدم تداخل با فازهای قبلی | Phase05 | Owner approved 2026-10-16, ArginAccounting compatibility amendment |
| 02 | Persistence and Migrations | جداول مرجع و کاتالوگ اختصاصی، EF migration افزایشی | DB test-only، قیود و rollback review | 01 | Owner approved 2026-10-16; test logs not independently captured |
| 03 | Official Identifier Model | شناسه رسمی کالا/خدمت و منشأ/اعتبار آن | شناسه نامعتبر/تکراری و ادعای رسمی بدون منبع رد شود | 02 | Implemented; awaiting operator tests and owner signoff |
| 04 | Official Unit Codes | مرجع کدهای رسمی واحد و نگاشت به واحد داخلی | واحد ناشناخته و نگاشت ناسازگار رد شود | 03 | Pending |
| 05 | VAT Classification | طبقه‌بندی VAT و نرخ/معافیت وابسته به تاریخ و مرجع | عدم فرض نرخ ثابت و تست تاریخ‌های مرزی | 03–04 | Pending |
| 06 | Versioned Reference Catalog | نسخه منبع، بازه اعتبار، snapshot و عدم تغییر تاریخچه | نسخه‌های پیشین قابل بازیابی؛ همپوشانی کنترل شود | 03–05 | Pending |
| 07 | Reference Import Pipeline | ورود فایل رسمی با staging، preview و اعتبارسنجی | Import تکراری idempotent و خطاها گزارش شوند | 06 | Pending |
| 08 | Reference Activation and Audit | انتشار کنترل‌شده نسخه و audit/rollback منطقی | نسخه فعال بدون حذف تاریخی جابه‌جا شود | 07 | Pending |
| 09 | Customer Product Domain | کالاهای اختصاصی، وضعیت و شناسه داخلی | تفکیک مشتری و جلوگیری از حذف ارجاعات | 02–06 | Pending |
| 10 | Customer Service Domain | خدمات اختصاصی و قواعد نوع خدمت | تست CRUD، مالکیت و ناسازگاری نوع | 09 | Pending |
| 11 | Tax Identifier Mapping | اتصال کالا/خدمت مشتری به شناسه رسمی با تاریخچه | نگاشت نامعتبر یا متعلق به نوع اشتباه رد شود | 08–10 | Pending |
| 12 | Catalog API and Search | API جست‌وجو، فیلتر، صفحه‌بندی، ETag/concurrency | تست نقش‌ها، paging و stale updates | 09–11 | Pending |
| 13 | Persian RTL Catalog UI | فهرست، فرم، جست‌وجوی انتخابی و خطاهای فارسی | تست مسیرهای مجاز، اعتبارسنجی و دسترس‌پذیری | 12 | Pending |
| 14 | Invoice Handoff Contracts | قرارداد snapshot برای فاز ۷/۸ بدون صدور فاکتور | تست قرارداد نسخه و عدم تغییر اطلاعات تاریخی | 06,11–13 | Pending |
| 15 | Security and Regression | تست isolation، race، migration و رگرسیون فازهای ۳–۵ | منع cross-customer و former-partner access | 01–14 | Pending |
| 16 | Final Acceptance and Handoff | تست تجمیعی Mac، Ubuntu، مستند و گزارش ریسک | گزارش شواهد، تأیید مالک؛ Merge/tag جداگانه | 15 | Pending |

## Engineering and Safety Invariants
1. Additive, reviewed migrations only; confirm `current_database() = 'argintaxpayer_test'` before any DB change; no automatic migrations or production DB writes.
2. Never invent or claim tax-authority validation. Official source imports require traceable provenance and version; unknown classification must be explicitly unresolved.
3. Historic catalog versions, mappings and future invoice snapshots are immutable/auditable; no retroactive tax-rate overwrite.
4. Server-side authorization and database query scoping for Root, Level2 and customer; preserve prospective commercial ownership transfer semantics.
5. No secrets, real certificates, private keys or production dumps in the public repository.
6. Each step: focused automated tests, negative cases, paths, commit SHA, migration impact, CI/Mac/Ubuntu evidence and explicit owner approval recorded in this file. No next step before approval.
7. Step 16 final Mac regression and Ubuntu `bash scripts/ubuntu-dev.sh verify`, then loopback-only `up/status/down`; early Ubuntu checks only when schema/infrastructure/platform risk requires.
8. Local runner: `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh`, `zsh scripts/dev-down.zsh`. Runner manages owner-approved AutoSSH `127.0.0.1:15432` tunnel to test PostgreSQL. No manual plain SSH tunnel.
9. Main merge, tag, release and deployment are separately approved gates after phase acceptance.

## Step Execution Record
Step 01 approved by owner 2026-10-16; Step02 approved by owner 2026-10-16. Step03 implementation pending acceptance; Steps04–16 not started. For each approved step append: design decisions, changed paths, tests and outputs, DB check, risks, commit, owner approval and date.


## Step 01 — Architecture and Contracts (2026-10-16)

**Status:** Design and contract baseline recorded; awaiting explicit owner signoff. No runtime functionality was implemented. No claims of completed builds, tests, CI or DB verification.
**Reviewed baseline files:** `docs/roadmap/18-phases.md`, `docs/phases/phase-04.md`, `docs/phases/phase-05.md`, `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Parties/Party.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerProfile.cs`, `src/ArginTaxPayer.Api/Program.cs`, `src/ArginTaxPayer.Api/Parties/PartyDevelopmentEndpoints.cs`.
**Existing architecture confirmed:** EF DbContext maintains distinct Party, CustomerCommercialOwner, TaxpayerProfile and customer memberships. TaxpayerProfile.CustomerUserId can be null for unresolved legacy data. Existing party API is explicitly loopback-only Development; no production authorization maturity may be assumed from these endpoints.

### Design decisions (Phase06 ADR inline)
1. **Reference data vs customer catalog:** Official reference rows are globally readable *only to authenticated authorized tenants* and editable through privileged, audited administrative import/activation. Customer items are scoped by immutable `CustomerUserId` (account ownership). Do not key catalog ownership by Party, fiscal-memory ID or Level2 partner ID. A customer's many taxpayers can consume its catalog subject to future invoice/entitlement authorization.
2. **Separate identities:** `CatalogItemId` is a stable UUID, customer `InternalCode` is not an official tax identifier, and `OfficialGoodsServiceCode` is a source-versioned regulatory identifier. Never infer an official match from item name or party identifiers. Product and Service are explicit discriminated kinds; do not silently convert kind on edit.
3. **Reference model:** `OfficialCatalogSource` (issuer, source URI/reference, fetched/imported timestamp, content checksum, declared edition, legal effective interval, trust/verification status); `OfficialCatalogRelease` (source, version, state Draft/Validated/Active/Superseded/Rejected, activated-at, actor, checksum); `OfficialGoodsServiceEntry` (release, official code, kind, title, valid interval, source row); `OfficialUnitEntry` (release, official code, title/precision where evidenced); `VatClassificationEntry` (release, classification, documented legal basis, applicability/exception, effective interval, nullable rate). These are planned conceptual entities, not existing EF classes or asserted authority schema.
4. **Tenant model:** `CustomerCatalogItem` (UUID, CustomerUserId, Product|Service, normalized customer code, localized name, active/archived status, revision, UTC timestamps); `CustomerItemOfficialMapping` (customer item, official entry/release, official unit if applicable, valid-from/to, evidence, actor and audit). Explicit mapping required; unknown/unverified official code is never represented as authority-approved. Pricing/stock/invoice financial computation is outside Phase06.
5. **Versions and effective time:** Reference releases are immutable once activated. Store UTC instants and explicit official effective periods independently from import time. Use half-open `[from,to)` intervals. Rate applicability is resolved by legal effective time and relevant invoice date in future phases, not `DateTime.Now`, and no universal 10%/other VAT assumption is allowed. Missing or ambiguous classification returns `unresolved` and blocks confirmed official mapping; it is not automatically zero-rated. An archived customer item remains historically addressable.
6. **Constraints and concurrency:** Plan unique `(CustomerUserId, normalized InternalCode)` for active items, unique `(releaseId, kind, officialCode)` for official entries, FK RESTRICT for history, revision-based optimistic concurrency, deterministic imports keyed by source+release checksum and explicit conflict handling. Do not silently reuse old codes after archive without a reviewed policy. Review optional unit applicability and duplicates before Step02 physical schema.
7. **Authorization:** Level1 can administer its own root customers under existing scope; Level2 can manage only *currently* commercially owned customer catalogs using live `CustomerCommercialOwner` joins; customer may manage only its own catalog subject to existing session and approved self-service capability. Former partner loses access immediately on ownership transfer. Reference release activation is Level1 privileged operation requiring audit and appropriate MFA; Level2/customer cannot edit global reference data. Super Admin rights, if relevant, require explicit existing policy rather than implied bypass.
8. **Safe API surface:** Do not expose new unaudited production routes. Phase12 adds actual API handlers, with explicit Dev loopback scope until independent production authorization review. Reuse bearer-session resolution, ownership policy and per-resource access checks; all writes validate command ownership in transaction. Distinguish 400 invalid, 401 unauthenticated, 403 forbidden, 404 not found, 409 version/conflict and 422 unresolved official classification (when semantically applicable), with stable error codes and no secret leakage.
9. **Invoice handoff:** Future invoice line snapshot must carry `catalogItemId`, kind, customer-visible name/code, official identifier, official release/source version, unit code/version, VAT classification/legal-effective-date provenance, and resolved-or-unresolved outcome. Snapshot must be immutable once invoice is finalized; Phase06 does NOT create invoices or calculate invoice totals.
10. **Import and provenance gate:** Import only verifiable, appropriately licensed official datasets, retaining provenance and checksum. An uploaded file is *unverified* until a privileged operator checks the issuing source, format/version, duplicates and classification; no fabricated authority connectivity. Re-import same content is idempotent, new content creates a release; rollback means changing active pointer with audit, not deleting prior records.

### Proposed API contracts (design only; not implemented)
Base routes in authenticated, loopback-only Development environment initially:
- `GET /api/dev/reference/goods-services?query=&kind=&releaseId=&page=&pageSize=` and `GET /api/dev/reference/units`, `GET /api/dev/reference/vat-classifications`: scoped reference read with version and applicability metadata.
- `POST /api/dev/reference/releases/import-preview`, `POST /api/dev/reference/releases/{releaseId}/validate`, `POST /api/dev/reference/releases/{releaseId}/activate`: privileged operator-only staging/validation/activation; audit and idempotency.
- `GET /api/dev/customers/{customerId}/catalog-items?kind=&query=&status=&page=&pageSize=`; `POST /api/dev/customers/{customerId}/catalog-items`; `GET /api/dev/customers/{customerId}/catalog-items/{itemId}`; `PUT /api/dev/customers/{customerId}/catalog-items/{itemId}`; `POST /api/dev/customers/{customerId}/catalog-items/{itemId}/archive`.
- `POST /api/dev/customers/{customerId}/catalog-items/{itemId}/official-mappings`; `GET /api/dev/customers/{customerId}/catalog-items/{itemId}/official-mappings`. Requests include `expectedRevision` where mutable; responses include current revision and regulatory provenance.

Illustrative DTO shapes (field contract, not compiled code):
```text
CatalogItemWrite { kind, internalCode, displayName, expectedRevision? }
OfficialMappingWrite { officialEntryId, officialReleaseId, unitEntryId?,
                       effectiveFromUtc, effectiveToUtc?, evidenceReference,
                       expectedRevision }
CatalogItemRead { id, customerUserId, kind, internalCode, displayName,
                  status, revision, officialMappingStatus }
OfficialReferenceRead { releaseId, sourceId, code, kind, title,
                        effectiveFromUtc, effectiveToUtc?, trustStatus }
```
Pagination: bounded page size (proposal 1..100), deterministic sort/id tie-breaker. No unbounded enumeration. No cross-customer query parameter bypass.

### Role and operation matrix (design target)
| Operation | Level1 | Level2 | Customer |
|---|---|---|---|
| Read official reference | Authenticated / scoped | Authenticated / scoped | Authenticated / scoped |
| Import/validate/activate reference release | Privileged Level1 with MFA + audit | Denied | Denied |
| List/read own customer catalog | Own root customers | Currently assigned customers only | Own account only |
| Create/edit/archive customer catalog item | Own root customers | Currently assigned customers only, subject to existing policy | Own account, gated by capability |
| Create item-to-official mapping | Own root customers | Currently assigned customers only | Own account, gated by capability |
| Read historical catalog/mapping data | Own scoped customer | Current scoped customer only | Own account only |
| Read catalog of unrelated or former partner customer | Denied | Denied | Denied |

No taxpayer-grant-based Level2 authorization. Root administrative oversight must use explicit existing root ownership policies; no unexplained global leakage.

### Step01 acceptance checklist
- [x] Read the official Phase06 roadmap, Phase04/05 compatibility rules and existing relevant source files.
- [x] Explicitly define domain entities, identifiers, version semantics, authorization matrix and API DTO/route contract.
- [x] Establish non-destructive migration handoff and reference/provenance gating for Steps02–08.
- [x] Define negative-path and cross-phase regression expectations.
- [ ] Operator/owner review of reference-issuer datasets, code formats and any legal/source licensing questions before production ingestion.
- [ ] Owner approval to close Step01 and authorize Step02.

### Step01 design-level verification matrix (NOT executed)
| Case | Expected result | Owner test stage |
|---|---|---|
| Duplicate internal code within same customer | Conflict | Step09/12 |
| Identical internal codes for two different customers | Allowed, isolated | Step09/15 |
| Unknown/unverified official code or VAT class | Unresolved; never falsely approved/zero-rated | Step03/05/11 |
| Duplicate reference import with identical checksum | Idempotent; no duplicate active release | Step07/08 |
| Reference release changes | Historical reference and mapping remain stable | Step06/08/14 |
| Level2 requests former customer's catalog after transfer | Forbidden/not found, no data leak | Step12/15 |
| Customer changes another customer's item by guessed GUID | Denied | Step12/15 |
| Stale expectedRevision writes | Conflict without partial mutation | Step09/10/12 |
| Cross-kind service/product official mapping | Rejected | Step11 |
| Unverified migration DB target | Abort without changes | Step02 |

**Files changed in Step01:** `docs/phases/phase-06.md` only.
**Database/migration:** None; no SQL executed; no existing data modified.
**macOS:** No build/unit/API execution; design-only step.
**Ubuntu:** Deferred to Step16; no infrastructure or schema changes.
**CI:** Not checked.
**Residual design risks:** Public official data availability/licensing and version identification, lawful VAT classification/exemptions, exact regulated unit-code source and compatibility, future customer self-service entitlement feature gating, potential existing product terminology elsewhere in repository. These must be resolved at the corresponding implementation step without inventing values.
**Owner acceptance:** Pending.


## Owner-approved scope clarification — Official measurement-unit seed and Level1-only reference-code administration (2026-10-16)

**Applies within existing fixed sixteen steps (no renumbering):** Step02 designs additive reference tables, idempotent seed/import status and uniqueness checks; Step03 implements official goods/service identifier code validation and Level1-only reference-code changes; Step04 populates the complete official measurement-unit catalog; Step06 retains immutable source/release versions; Steps07–08 implement provenance-gated import and publishing; Step12 enforces authorization on all mutating APIs; Step13 offers scoped read-only reference selection and Level1-only administration UI; Step15 verifies security and replay safety.

**Documented source:** Owner-provided `RC_IITP.IS V7.9` (1405) specifies invoice field `mu` as numeric-string max 8 and references the *separate* `RC_UMGS.ST` official units-of-measure catalog from the tax authority. `RC_IITP.IS` itself is NOT an authoritative full units table. A publicly found secondary copy `RC_UMGS.ST_V1.4` is dated Ordibehesht 1402; treat as an archival candidate, **not** as verified current official release. Do not claim that it is up-to-date. Seed **all** rows only after obtaining and verifying the latest official source file/version (or owner-supplied authenticated copy), row count and checksum. No guessed/incomplete seed and no silent legacy fallback. Example unit labels/codes appearing in secondary published tables, not independently current-verified: عدد=1627, لیتر=1637, متر مربع=1645.

**Three code namespaces MUST remain separate:**
1. `OfficialUnitCode`: regulatory measurement unit referenced by invoice `mu`. Canonical text (preserves zeros), numeric ASCII, maximum 8 digits per owner-provided specification; explicit release/version and activation state. Customer/Level2 cannot create or change.
2. `OfficialGoodsServiceCode`: official tax goods/service identifier referenced by invoice `sstid`, independent of unit. New/custom official code candidates may be entered/edited only by Level1, and remain `Unverified` until official provenance checks; never claim official validity solely because an admin entered it.
3. `CustomerCatalogItem.InternalCode`: customer's local SKU/item code, NOT an official code; management remains scoped to the customer workflow under the Phase06 authorization matrix. A request to restrict *all* customer-internal codes would be a separate owner-approved policy change, because it would affect customer catalog CRUD.

**Unit-seed acceptance policy:**
- Checked-in machine-readable seed source with declared document identifier, version, issuer, source URL or owner-supplied verified document fingerprint, SHA256 checksum, row count, normalization policy, acquisition date; no secrets.
- Explicit database migration/seeding command for `argintaxpayer_test` only after checking `SELECT current_database()`; no migration or seed at startup. Idempotent upsert or versioned staging under transaction, no duplicate codes within a release; no destructive overwrite of referenced released rows.
- After seeding, compare imported count and each `(code,name)` against verified source; forbid empty/partial seed falsely marked Active. Existing valid rows and invoice snapshots unchanged.
- Level1-only APIs for staging/custom entries, approving, altering or deactivating reference units/official product-code candidates; enforce existing Level1 session and MFA where required server-side, not only disabled UI controls; include actor, reason, revision, timestamp, before/after audit. Official releases immutable: correction creates a new revision/release rather than rewriting historical code.
- Level2/customer have read/search/select only. `403` for attempted write, `401` without authentication, `409` for duplicate or stale revision; unknown/unverified unit mapping cannot be used as verified official invoice code.
- Step15 negative tests: Level2/customer POST/PUT/PATCH/DELETE rejected even with forged or guessed IDs; direct/indirect mutation denied; concurrent Level1 updates CAS-safe; idempotent rerun equal row count; edition changes preserve historic semantics.
- **No DB action executed in this amendment.** Actual seed and CRUD implementation are Step02–04/12–13 deliverables, each gated by owner approval and source-file verification. Do not advance to Step02 until Step01 is explicitly signed off.


## ArginAccounting interoperability baseline and Step02 completion record (2026-10-16)

### Step01 conditional signoff and architecture correction
Owner approved Step01 provided compatibility with ArginAccounting. Reviewed owner-provided *ArginAccounting Phase16 — Products & Services* specification. Binding contract:
- `ItemKind` uses three compatible categories: `STOCK_ITEM`, `NON_STOCK_ITEM`, `SERVICE` (persisted values 1, 2, 3). Fiscal reference `OfficialGoodsServiceEntry.Kind` is a **separate** two-state goods/service classification; stock vs non-stock must never leak as a tax classification.
- Every customer item including SERVICE has a base measurement unit; per-item conversion is exact rational numerator/denominator with positive denominator and explicit rounding policy, not floating point. Do not imply that a carton has universal fixed size. Effective-date conversion history must remain stable.
- ArginAccounting's `CompanyId` scopes accounting Item. Here `CustomerUserId` scopes customer master data: integration in Phase13 requires an explicit company/taxpayer-to-customer mapping, never equality of these UUIDs. Tenant item code is stable per customer. Financial balances, FIFO, COGS, purchase/sales prices, warehouse inventory and AccountRole posting do not belong to this phase.
- Item states map conceptually to ACTIVE/INACTIVE/BLOCKED/DISCONTINUED. A tax profile is a versioned reference, not a mutable embedded tax response. Preserve invoice-line snapshot immutability for Phase07–08/13.
- This is the **compatibility contract**, not a claim that ArginTaxPayer already supports ArginAccounting inventory, barcode or category features. Those remain integration/other-phase concerns.

### Step02 — Persistence and Migrations
**Status:** Implementation committed to `phase/06-goods-services`; awaiting locally executed tests, safe database application and owner acceptance.

**Changed files:**
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogEntities.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogModelConfiguration.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261016120000_AddPhase06CatalogFoundations.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`
- `tests/ArginTaxPayer.Tests/Phase06CatalogPersistenceTests.cs`

**Entities / tables:** `CatalogReferenceRelease` / `catalog_reference_releases`; `OfficialMeasurementUnit` / `official_measurement_units`; `OfficialGoodsServiceEntry` / `official_goods_service_entries`; `VatClassificationEntry` / `vat_classification_entries`; `CustomerCatalogItem` / `customer_catalog_items`; `CatalogItemUnitConversion` / `catalog_item_unit_conversions`; `CatalogOfficialMapping` / `catalog_official_mappings`.

**Integrity:** unique (source document, SHA256) releases; unique official code per release; per-customer unique internal code; revision-concurrency; explicit customer identity FK; base unit FK; strict RESTRICT history FKs; positive exact ratios; UTC effective intervals; nullable VAT rate and legal-basis metadata. Domain operations (official source verification, overlapping-period guard, kind mapping, Level1 authorization, data seed) are **not** implemented here and remain the designated Steps03–13; schema checks alone do not enforce those business rules.

**Migration:** `20261016120000_AddPhase06CatalogFoundations` adds seven tables, indexes and PostgreSQL constraints; does not alter Phase03–05 tables or backfill existing data. `Down` drops all seven tables and is destructive after first insert; never use on retained data without separate approval. Step04 official seed awaits a verified complete `RC_UMGS.ST` release; no fabricated default codes seeded in Step02. EF snapshot is aligned through the shared mapping configuration rather than auto-generated inline entity configuration; validate using `dotnet ef migrations has-pending-model-changes` or equivalent before signoff.

**Developer macOS verification (run from updated branch):**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~Phase06CatalogPersistenceTests
```
Use canonical environment launchers only when UI/DB smoke needed:
```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
Before ANY explicit migration, verify `SELECT current_database()` against the test connection with the owner-approved Npgsql parsing/psql approach in the roadmap, and confirm precisely `argintaxpayer_test`. Review generated SQL and pending EF migrations before applying to the test database. After explicit application, verify `__EFMigrationsHistory`, seven tables, check/unique/FK restrictions and rollback-only integrity probes. No production database operation. Run `zsh scripts/dev-down.zsh` when finished.

**Targeted acceptance matrix:**
1. Release build and all five model metadata tests pass.
2. The DB name is exactly `argintaxpayer_test` and migration runs only after explicit authorization.
3. Seven catalog tables and FK/unique/check constraints exist; no Phase03–05 tables modified.
4. Same tenant duplicate internal code fails; another tenant may reuse code; invalid unit strings and denominator zero fail.
5. FK RESTRICT prevents destroying referenced unit/release/item history.
6. Duplicate release checksum cannot create second source release.
7. Snapshot/model comparison has no unexpected pending changes.
8. Tests confirm no auth routes, invoicing, VAT defaults, inventory postings or official seed were prematurely activated.

**Test evidence:** GitHub code changes committed; .NET build **not executed by assistant**, tests **not executed**, PostgreSQL test DB **not connected/migrated**, macOS operator acceptance **pending**, Ubuntu **deferred** pending schema-specific verification and final phase acceptance, CI **not checked**. This is not a passed test claim.

**Known limitations and follow-up:** Reference issuer verification and the complete official unit import are Step03/04/07/08; Level1 write authorization Step12/13; no customer catalog API yet; per-company ArginAccounting integration and historical conversion business rules require explicit later checks. Evaluate any database/provider model snapshot differences during local tests; fix before calling Step02 owner-approved.

**Owner approval Step02:** Pending local test evidence and explicit signoff.

. Do NOT silently repair them or apply against production.

**Operator macOS commands:**
```bash
git fetch origin
git switch phase/06-goods-services
git pull --ff-only
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~Phase06OfficialIdentifierTests|FullyQualifiedName~Phase06CatalogPersistenceTests"
dotnet ef migrations has-pending-model-changes --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```
For database migration, use the existing `zsh scripts/dev-up.zsh` AutoSSH launcher and the canonical `npgsql_value` extraction + psql verification in `docs/roadmap/18-phases.md`. Confirm `SELECT current_database() = 'argintaxpayer_test'`, then inspect existing records and generated SQL. Apply with `dotnet ef database update --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"` ONLY after explicit DB check. Verify `__EFMigrationsHistory` and constraint in `pg_constraint`.

**Test matrix and expected result:**
1. Exactly 13 ASCII digits accepted; Persian/Arabic-Indic digits normalize deterministically; leading zeroes preserved.
2. 12/14 digits, letters, punctuation, control characters, and unsupported numeral forms rejected.
3. Kind compatibility maps both Accounting goods types to fiscal Goods, service to Service; mismatches rejected.
4. Database CHECK and release-scoped unique index reject invalid or duplicate codes.
5. A manually entered staged candidate is Unverified; staged release alone does not imply official recognition.
6. Only current active Level1 with enabled MFA may create candidate; Level2/customer/anonymous denied (service-level DB integration proof pending).
7. Published/superseded releases are not mutated; create command requires staged release.
8. Existing parties, taxpayers, customers, historic entries, official units and tax rates untouched; no data seed.

**Actual evidence:** GitHub commits saved. Automated build/tests/CI and real PostgreSQL integration tests **not executed/observed by assistant**. Ubuntu verification: defer full acceptance to Step16, but because a PostgreSQL CHECK was added, focused migration validation in the shared test DB is required before Step03 approval. No live authority communication or official code validation performed. 
**Open risks:** Live Level1 authorization needs integration negative-path proof; authoritative official dataset verification and audit/release history are upcoming steps. Model snapshot uses `CatalogModelConfiguration.Apply`; operator must confirm no model drift.
**Owner signoff:** Pending.

## Phase Closeout
Not started.
