# ArginTaxPayer — Phase 06: Goods and Services

**Status:** Step 01 architecture/contracts documented and committed; owner acceptance pending. Steps 02–16 not started.
**Branch:** `phase/06-goods-services`
**Baseline:** `main` after Phase05 merge `6a3de74692403029b68c2061251601752eebd518`.
**Roadmap:** `docs/roadmap/18-phases.md` Phase 06.
**Policy:** This is the single authoritative evolving phase plan. Step count and scope are frozen pending dated owner-approved amendment.

## Objective and Architecture
Build a versioned official goods/services reference catalog (tax identifiers, official unit codes, VAT classification), tenant/customer-owned product and service catalogs, audited mappings, searchable scoped APIs and Persian RTL management UI. Layers: Domain rules; Application commands/queries; Infrastructure EF Core/PostgreSQL/import; ASP.NET Core API; Next.js UI. Preserve Phase03 identity, Phase04 customer/partner ownership and Phase05 parties; customer ownership governs authorization, not legacy taxpayer-level grants. Reference data is shared read-only to tenants; tenant catalogs are private. Keep official identifiers separate from internal product IDs. Require provenance, effective dates and source-version attribution; no assumed universal VAT rate. No live authority connectivity, invoice issuance, signing, billing or financial postings in this phase.

## Fixed 16-Step Plan
| Step | Title | Deliverable | Acceptance | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Architecture and Contracts | مدل دامنه، ADR، API، مجوزها و مرز فازها | بازبینی قرارداد و عدم تداخل با فازهای قبلی | Phase05 | Implemented (documentation); owner approval pending |
| 02 | Persistence and Migrations | جداول مرجع و کاتالوگ اختصاصی، EF migration افزایشی | DB test-only، قیود و rollback review | 01 | Pending |
| 03 | Official Identifier Model | شناسه رسمی کالا/خدمت و منشأ/اعتبار آن | شناسه نامعتبر/تکراری و ادعای رسمی بدون منبع رد شود | 02 | Pending |
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
Step 01 authorized by owner 2026-10-16; architecture contract documented below. Steps 02–16 pending. For each approved step append: design decisions, changed paths, tests and outputs, DB check, risks, commit, owner approval and date.


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

## Phase Closeout
Not started.
