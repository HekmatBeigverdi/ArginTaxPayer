# ArginTaxPayer — فاز ۰۳: هویت و کنترل دسترسی

<div dir="rtl">

**وضعیت:** برنامه‌ریزی‌شده؛ منتظر تأیید گام ۱  
**شاخه:** `phase/03-identity-authorization`  
**مبنای فنی:** فاز ۲ و `docs/roadmap/18-phases.md`  
**تاریخ آغاز:** ۲۰۲۶-۱۰-۱۲

## هدف و قواعد ثابت
پیاده‌سازی هویت کاربران، ورود امن، MFA، نشست‌ها، نقش‌های سطح ۱ و ۲ و مشتری، و مجوزهای سمت سرور. مدیریت واقعی پرونده مؤدیان و انتساب آن‌ها در فاز ۴ است. این فاز **۱۰ گام ثابت** دارد؛ افزایش/تغییر محدوده تنها با ثبت اصلاحیه و تأیید مالک. مستندات اجرایی، تست، Commit و تأیید **صرفاً در همین فایل** به‌روزرسانی می‌شود؛ فایل مجزا برای هر گام ایجاد نشود.

**امنیت:** ثبت‌نام عمومی نباید سطح مدیر بسازد؛ نقش سطح ۱ فقط از مسیر کنترل‌شده Bootstrap، سطح ۲ فقط از مسیر مجاز سطح ۱؛ رمزها فقط Hash امن، توکن‌ها و کدهای بازیابی در لاگ ممنوع، نشست و Refresh Token قابل لغو؛ مجوزها و Scope سمت سرور اعمال شوند؛ مسیر انتخاب Tenant بدون احراز هویت ممنوع. مهاجرت فقط در پایگاه تست `argintaxpayer_test` با اقدام صریح؛ هیچ Migration خودکار هنگام Startup.

## گام‌ها و معیار تأیید

| گام | تحویل | تست پذیرش اختصاصی | وضعیت |
|---|---|---|---|
| ۰۱ | مدل User/Identity، قراردادها، EF Migration و Seed امن مدیر اولیه | ساخت مدل، یکتایی شناسه، عدم ایجاد مدیر عمومی، migration تست | Planned |
| ۰۲ | ثبت‌نام کنترل‌شده، ورود، Hash رمز، اعتبارسنجی | رمز غلط/کاربر غیرفعال رد، ثبت‌نام تکراری رد | Planned |
| ۰۳ | Access/Refresh Token، چرخش و لغو نشست، خروج | Refresh یک‌بارمصرف، توکن منقضی و Logout رد | Planned |
| ۰۴ | MFA و کدهای بازیابی یک‌بارمصرف، فرایند بازیابی امن | MFA اجباری برای مدیران، جلوگیری از Replay و نشت Secret | Planned |
| ۰۵ | نقش‌ها و Permissionهای Level 1/Level 2/Customer | جلوگیری از ارتقای نقش توسط کاربر، آزمون مجوزها | Planned |
| ۰۶ | Policyهای منابع و Scope سمت سرور، قرارداد اتصال به فاز ۴ | درخواست بین Scopeها 403/404، عدم اتکا به UI | Planned |
| ۰۷ | محدودسازی ورود، لغو نشست‌ها، ممیزی و Redaction | Rate limit، رویدادهای امنیتی بدون داده حساس | Planned |
| ۰۸ | UI فارسی RTL ورود، MFA، حساب، خطاها و مسیرهای محافظت‌شده | ورود/خروج، خطا، ریسپانسیو، عدم نمایش داده محافظت‌شده | Planned |
| ۰۹ | تست یکپارچه و منفی هویت/نقش/نشست | تست‌های API، UI و Scope isolation؛ CI سبز | Planned |
| ۱۰ | بازبینی امنیت و Migration، CI، مستندات نهایی و تحویل | Build/Test macOS+Ubuntu، بدون Secret، گزارش Release Gate | Planned |

## دستورهای کوتاه تست هر گام

پیش‌نیاز یک‌باره: .NET SDK مطابق `global.json`، Node/npm مطابق فاز ۲، PostgreSQL تست فقط در گام‌های Migration/Integration. این دستورها از **ریشه پروژه** اجرا شوند. در گام‌هایی که دیتابیس لازم نیست، تست‌های آفلاین اجرا می‌شوند.

**macOS**
```bash
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```

**Ubuntu VPS** (پس از اطمینان از تمیز بودن Working Tree؛ از بازنویسی تغییرات محلی خودداری کنید)
```bash
cd ~/projects/ArginTaxPayer
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```

**تست‌های اختصاصی:** در هر گام، تست‌های مربوط به ردیف همان گام به مجموعه خودکار افزوده می‌شود و خروجی واقعی macOS و Ubuntu ثبت می‌گردد. تست Migration فقط با اتصال تأییدشده به `argintaxpayer_test` اجرا می‌شود؛ دستورات دقیق Migration بعد از تعیین Migration واقعی در گام ۱ ثبت خواهد شد. هیچ تستی با صرف نوشتن کد «موفق» محسوب نمی‌شود.

## ثبت اجرای گام (در همین فایل)
برای هر گام پس از پیاده‌سازی این اطلاعات را زیر همین بخش درج کنید: **گام / خلاصه تغییرات / فایل‌های اصلی / Commit SHA / Migration (دارد یا ندارد) / macOS (Pass/Fail + شواهد) / Ubuntu (Pass/Fail + شواهد) / CI / تأیید مالک / وضعیت**. تا پیش از دریافت نتایج، وضعیت Planned یا Implemented باقی می‌ماند. دستور تأیید: «گام N تأیید است».

## تحویل نهایی
پس از تأیید گام ۱۰: بازبینی نهایی CI، تغییرات DB و امنیت؛ سپس Merge/Tag/Release فقط با مجوز صریح مالک. فاز ۴ با سند مستقل آغاز می‌شود.

</div>
