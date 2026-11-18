# اصلاح و لغو اشتراک ادمین Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** افزودن امکان اصلاح مدت/سهمیه و لغو اشتراک هر کاربر توسط Level 1، با اثر مالی منفی قابل مشاهده در گزارش درآمد و تسویه.

**Architecture:** یک جدول append-only برای adjustmentهای اشتراک ایجاد می‌شود. سرویس مدیریتی با قفل و تراکنش، اشتراک و دفتر سهمیه را اصلاح یا لغو می‌کند و adjustment لغو را ثبت می‌کند. گزارش درآمد و پیش‌نمایش/ثبت تسویه، فروش‌ها و adjustmentها را به‌صورت خالص محاسبه می‌کنند.

**Tech Stack:** ASP.NET Core Minimal APIs، EF Core/Npgsql migrations، PostgreSQL، Next.js/React/TypeScript، xUnit.

**Spec:** `docs/superpowers/specs/2026-11-18-admin-subscription-adjustment-design.md`

## Global Constraints

- فقط Level 1 فعال با MFA می‌تواند اصلاح یا لغو انجام دهد.
- سوابق سفارش، دریافت و تخصیص درآمد حذف یا ویرایش نمی‌شوند.
- adjustmentهای مالی append-only و دارای operation key یکتا هستند.
- اصلاح مقدار اثر درآمدی ندارد؛ لغو باید adjustment منفی را در درآمد و تسویه وارد کند.

## Review Focus

- اشتراک زمانی و تعدادی هر دو مقدار جدید معتبر و قابل افزایش/کاهش داشته باشند؛ تست سرویس در Task 2.
- اصلاح همزمان یا revision قدیمی دوباره اعمال نشود؛ تست concurrency/idempotency در Task 2.
- لغو تکراری یا لغو اشتراک منقضی/لغوشده رد شود؛ تست سرویس در Task 2.
- adjustment منفی در جمع گزارش و سهم Level 1/Level 2 اعمال شود؛ تست read/settlement در Task 3.
- مشتری، Level 2 و Level 1 بدون MFA نتوانند API را اجرا کنند؛ تست endpoint در Task 4.

### Task 1: مدل و migration adjustment

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/<timestamp>_AddSubscriptionAdjustments.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionCatalogPersistence.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`

- [ ] **Step 1:** مدل `StoredSubscriptionAdjustment` با customer, subscription, actor, kind, operation key, previous/new values, negative gross/partner/root amounts, reason و timestamp اضافه کن.
- [ ] **Step 2:** migration جدول append-only با FK، check constraint برای نوع adjustment و trigger جلوگیری از update/delete بنویس.
- [ ] **Step 3:** DbSet و mapping EF را اضافه کن و migration را با `dotnet ef migrations list` بررسی کن.

### Task 2: سرویس تراکنشی اصلاح و لغو

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionAdjustmentService.cs`
- Create: `tests/ArginTaxPayer.Tests/SubscriptionAdjustmentServiceTests.cs`

**Interfaces:**
- Produces `ReadActiveAsync(token, customerId)` برای نمایش اشتراک فعال.
- Produces `AdjustAsync(token, customerId, subscriptionId, newDays, newUnits, expectedRevision, operationKey, reason)`.
- Produces `CancelAsync(token, customerId, subscriptionId, expectedRevision, operationKey, reason)`.

- [ ] **Step 1:** تست‌های failing برای authorization، مقدار معتبر پلن زمانی/تعدادی، revision، idempotency، اصلاح و لغو بنویس.
- [ ] **Step 2:** احراز Level 1 فعال با MFA و مالکیت customer را در سرویس پیاده کن.
- [ ] **Step 3:** اصلاح زمانی را با محاسبهٔ `expires_at_utc` از زمان فعلی و اصلاح تعدادی را با قفل credit account و ثبت grant/revoke ledger entry پیاده کن.
- [ ] **Step 4:** لغو را با state transition، آزادسازی سهمیهٔ باقی‌مانده و ثبت adjustment منفی در همان transaction پیاده کن.
- [ ] **Step 5:** تست‌ها را اجرا کن و انتظار PASS برای سناریوهای Task 2 داشته باش.

### Task 3: API و گزارش‌های مالی

**Files:**
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12...Endpoints.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/PartnerRevenueLedgerReadService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/PartnerBatchSettlementPreview.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/PartnerBatchSettlementService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/CustomerFinancialHistoryService.cs`

- [ ] **Step 1:** GET active subscription، POST adjust و POST cancel را با پاسخ‌های 401/403/404/409/422 اضافه کن.
- [ ] **Step 2:** گزارش درآمد را طوری گسترش بده که adjustmentهای لغو را به‌صورت ردیف منفی و در totalها نشان دهد.
- [ ] **Step 3:** preview و posting تسویه را طوری تغییر بده که adjustment منفی را در gross، partner share و root share لحاظ کند و adjustment قبلی دوباره مصرف نشود.
- [ ] **Step 4:** گزارش مالی کاربر را با وضعیت اصلاح/لغو سازگار کن.
- [ ] **Step 5:** تست read/settlement را اجرا کن و جمع خالص را با یک فروش و یک لغو بررسی کن.

### Task 4: پنل کاربران نرم‌افزار

**Files:**
- Modify: `apps/web/app/users/users-workspace.tsx`
- Modify: `apps/web/app/users/users-workspace.css`
- Create: `apps/web/app/api/customer-owners/[customerUserId]/subscription/route.ts`

- [ ] **Step 1:** داخل مودال کاربر Level 1 دکمه‌های «اصلاح پلن» و «لغو پلن» و وضعیت فعلی را اضافه کن.
- [ ] **Step 2:** فرم اصلاح را بر اساس نوع پلن به ورودی روز یا تعداد تبدیل کن و مقدار فعلی/جدید را واضح نمایش بده.
- [ ] **Step 3:** تأیید لغو، علت اجباری و نمایش اثر مالی منفی را اضافه کن.
- [ ] **Step 4:** خطاهای revision، پلن فعال نداشتن، دسترسی و ارتباط را به پیام فارسی تبدیل کن و پس از موفقیت پرونده را بازخوانی کن.
- [ ] **Step 5:** `npm --prefix apps/web run typecheck` را اجرا کن.

### Task 5: یکپارچه‌سازی و verification

- [ ] **Step 1:** `dotnet build src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj --no-restore` را اجرا کن.
- [ ] **Step 2:** `dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --no-restore` را اجرا کن.
- [ ] **Step 3:** `git diff --check` و مرور نهایی scope امنیتی و append-only بودن adjustmentها را انجام بده.
