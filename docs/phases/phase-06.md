# ArginTaxPayer — Phase 06: Goods and Services

**Status:** Planned; Step 01 not started; awaiting owner approval to begin.
**Branch:** `phase/06-goods-services`
**Baseline:** `main` after Phase05 merge `6a3de74692403029b68c2061251601752eebd518`.
**Roadmap:** `docs/roadmap/18-phases.md` Phase 06.
**Policy:** This is the single authoritative evolving phase plan. Step count and scope are frozen pending dated owner-approved amendment.

## Objective and Architecture
Build a versioned official goods/services reference catalog (tax identifiers, official unit codes, VAT classification), tenant/customer-owned product and service catalogs, audited mappings, searchable scoped APIs and Persian RTL management UI. Layers: Domain rules; Application commands/queries; Infrastructure EF Core/PostgreSQL/import; ASP.NET Core API; Next.js UI. Preserve Phase03 identity, Phase04 customer/partner ownership and Phase05 parties; customer ownership governs authorization, not legacy taxpayer-level grants. Reference data is shared read-only to tenants; tenant catalogs are private. Keep official identifiers separate from internal product IDs. Require provenance, effective dates and source-version attribution; no assumed universal VAT rate. No live authority connectivity, invoice issuance, signing, billing or financial postings in this phase.

## Fixed 16-Step Plan
| Step | Title | Deliverable | Acceptance | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Architecture and Contracts | مدل دامنه، ADR، API، مجوزها و مرز فازها | بازبینی قرارداد و عدم تداخل با فازهای قبلی | Phase05 | Pending |
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
All steps pending. Step 01 begins only after owner approval of this fixed plan. For each approved step append: design decisions, changed paths, tests and outputs, DB check, risks, commit, owner approval and date.

## Phase Closeout
Not started.
