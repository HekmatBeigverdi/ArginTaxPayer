# ArginTaxPayer — Phase 09: Cryptographic Keys & Digital Signing — Key-Based Path

**وضعیت:** برنامه ۱۸ گامی با دستور آغاز گام۱ مبنای اجرا شد؛ گام۱ به تأیید مالک رسید؛ طراحی گام۲ آماده بررسی است و هنوز تأیید نشده.
**تاریخ بررسی:** 2026-10-20
**Repository:** HekmatBeigverdi/ArginTaxPayer
**Base:** main — `b8f307ed06041d902aed9d8432d3b082e1cd714a`
**Previous release:** `phase-08-v1.0.0`
**Branch:** `phase/09-cryptographic-keys-signing`
**تعداد ثابت مصوب:** ۱۸ گام، شامل پذیرش نهایی و انتشار مشروط. پس از تصویب، تغییر تعداد، ترتیب یا محدوده نیازمند تأیید صریح مالک است. زیرکار و رفع نقص داخل گام مرتبط ثبت می‌شود، نه با افزودن گام جدید.
**Goal:** نگهداری امن و ایزوله کلید، تولید و ورود کلید خصوصی قبلی، دریافت کلید عمومی و امضای محلی برای مسیر بدون گواهی مستقل؛ بدون اتصال یا ارسال واقعی صورتحساب.
**روش اجرا:** اجرای مستقیم گام‌به‌گام با مهارت executing-plans پس از تصویب؛ توقف در پایان هر گام تا تأیید صریح مالک. این سند هم طراحی و هم برنامه و دفتر شواهد فاز است؛ فایل موازی برای گزارش هر گام ساخته نمی‌شود.

## تصمیم اصلاح محدوده — درخواست مالک، 2026-10-20

اولویت قطعی این فاز **روش مبتنی بر کلید، بدون گواهی امضای مستقل** است. این بازنگری بنا بر درخواست صریح مالک برای تسریع فاز، پیشنهاد قبلی ۲۴ گامی را با ۱۸ گام جایگزین می‌کند؛ برنامه هنوز برای تصویب ارائه می‌شود. پس از تصویب این نسخه، تعداد و ترتیب ۱۸ گام ثابت است.

- مسیر اصلی: تولید کلید جدید یا ورود private.txt قبلی، مشتق‌سازی/خروجی عمومی، تطابق با public key ارائه‌شده، نگهداری امن و امضای آفلاین مطابق SDK legacy منتخب.
- «بدون گواهی» همچنان امضای رمزنگاری دارد. تولید CSR، ورود/اعتبارسنجی گواهی، chain، trust anchors، OCSP/CRL و adapter عملیاتی SDK گواهی‌محور از پیاده‌سازی و معیار پایان این فاز خارج و تا تصمیم جداگانه مالک معوق‌اند؛ شماره فاز جدیدی خودسرانه تعیین نمی‌شود.
- هیچ CSR یا گواهی برای generation/import/sign مسیر اصلی اجباری نیست. UI فقط مسیر بدون گواهی را فعال می‌کند؛ مسیر با گواهی حداکثر برچسب غیرفعال «در آینده» دارد.
- هر دو نسل SDK و اسنادشان به عنوان مرجع حفظ می‌شوند، اما مطالعه نسل جدید در حد تعیین مرز تفاوت است، نه پیاده‌سازی آن. RS256 سند v1.6 به مسیر قدیمی تعمیم داده نمی‌شود؛ الگوریتم، padding و serialization همان مسیر باید از SDK/راهنمای legacy اثبات شود.
- نسخه source قدیمی 0.0.30 با binary قدیمی 0.0.34 یکسان فرض نمی‌شود؛ گام ۱ یک مبنای legacy دقیق و آزمون‌پذیر انتخاب می‌کند. پشتیبانی عملیاتی امروز مسیر قدیمی در فاز ۱۰ با منابع معتبر و تست مجاز تأیید خواهد شد؛ اگر منبع معتبر ناسازگاری را نشان دهد، توقف و تصمیم مالک لازم است، نه تغییر خودکار به گواهی.
- کنترل‌های امنیتی custody، encryption، backup، recovery، isolation، authorization و audit برای تسریع حذف نمی‌شوند. export کلید خصوصی فعلاً غیرفعال است؛ فقط public artifact دانلود می‌شود. پیاده‌سازی export خصوصی نیز تا تصمیم جداگانه مالک معوق است.

## 1. نتیجه بررسی واقعی مبنا

- `main` هنگام بررسی روی SHA فوق بود؛ انتشار GitHub فاز ۸ وجود داشت و همان بدهی‌های اعلام‌شده را ثبت کرده بود. متن ابتدای `phase-08.md` هنوز انتشار را pending می‌داند؛ وضعیت واقعی Release مقدم است. اصلاح تاریخچه فاز ۸ خودکار انجام نمی‌شود.
- نقشه راه `docs/roadmap/18-phases.md` فاز ۹ را تولید کلید/CSR، گواهی، امضا/رمزگذاری و rotation قرار می‌دهد؛ اتصال در فاز ۱۰، صف در ۱۱، entitlement در ۱۲ است.
- مستندات اصلی فازهای ۶، ۷ و ۸، `docs/references/tax-authority-sdk-catalog.md`، ADR 0007، سند fiscal-memory-connection-onboarding و قراردادهای جاری بررسی شدند. سند onboarding هنوز بخشی از شواهد SDK را unavailable معرفی می‌کند؛ کاتالوگ و منابع بازیابی‌شده جدیدتر باید در گام ۱ مبنا قرار گیرند.
- زیرساخت موجود: .NET SDK `9.0.202` با latestPatch، معماری Domain/Application/Infrastructure/Api، EF Core/PostgreSQL، Next.js `15.5.27` و React `19.1.7`؛ پین دقیق package-lock حفظ می‌شود.
- `CertificateCustodyPolicy.cs`: فقط metadata و قرارداد `IPrivateKeyCustody`؛ custody mode فعلی ExternalVaultReference است. `FiscalCertificateMetadata` و جدول `fiscal_certificate_metadata` دارای FK محدودکننده و unique گواهی جاری هر حافظه است. fingerprint فعلی بدون تصمیم مهاجرت نباید به fingerprint کلید تغییر معنا دهد.
- `FiscalMemoryManagementService`: ثبت اولین حافظه PendingVerification؛ اجازه فعال‌سازی یا جایگزینی حافظه عملیاتی نمی‌دهد. وضعیت اتصال از ثبت metadata استنتاج نمی‌شود.
- `FiscalConnectionPolicyContracts.cs`: Level2 می‌تواند درخواست استثنای سالانه بدهد؛ تأیید جایگزینی مؤدی/حافظه به Level1 اختصاص دارد. این محدودیت حفظ می‌شود.
- `ISecretProvider` و `EnvironmentSecretProvider` موجودند؛ دومی bootstrap است و vault تولیدی نیست. `IdentityMfaCrypto` از AES-GCM استفاده می‌کند، اما کلید MFA و پیاده‌سازی آن برای custody مالیاتی بازاستفاده نمی‌شوند.
- اسکریپت `phase08-step19-verify.sh` به شاخه فاز ۸ قفل است؛ برای فاز ۹ مستقیماً قابل استفاده نیست. `ubuntu-dev.sh verify` دیتابیس تست را بررسی می‌کند و PG integration را فعال می‌کند؛ پایان verify به معنی اجرای سرویس یا ورود موفق مرورگر نیست.
- این بررسی build، اجرای تست یا صحت schema زنده را اثبات نمی‌کند. هیچ اتصال DB، migration یا تماس سامانه مؤدیان انجام نشده است.

## 2. منابع و دروازه تعیین مشخصات

| منبع | آنچه در این بررسی خوانده شد | کاربرد و محدودیت |
|---|---|---|
| TAX-CONNECT-16، RC_TICS.IS_v1.6، شهریور ۱۴۰۴ | صفحات ۵ و ۹–۱۲ از PDF اصلی کاربر | ص۵ کلید/گواهی ۲۰۴۸ بیتی؛ ص۱۱–۱۲ JWS با RS256، RSASSA-PKCS1-v1_5 و SHA-256، x5c و sigT؛ این مشخصات برای توکن همان نسخه است، نه تعمیم به همه انواع امضا |
| TAX-SDK-GUIDE-NEW، فروردین ۱۴۰۵ | صفحات ۴–۷ از PDF اصلی کاربر | PKCS#8، گواهی، Pkcs8SignatoryFactory و TaxApiFactory؛ راهنما 2.0.31 و بسته موجود 2.0.32 نیازمند تطبیق |
| TAX-SDK-GUIDE-OLD، فروردین ۱۴۰۴ | صفحات ۴–۷ از PDF اصلی کاربر | TaxApiService، Pkcs8SignatoryConfig، ITransferSignatory/IContentSignatory؛ مسیر قدیمی و shared mutable singleton نباید به مسیر جدید مخلوط شود |
| SDK-SOURCE-OLD/NEW و SDK-NUGET-OLD/NEW | کاتالوگ مخزن و SHAهای ثبت‌شده | ممیزی بایت‌های ZIP و binary در این مرحله انجام نشده؛ پیش‌شرط گام ۱، با نگهداری خصوصی و حذف نمونه‌های PEM از خروجی و Git |
| ADR 0007 و roadmap و onboarding | نسخه ثبت‌شده در SHA مبنا | محدودیت custody با رضایت صریح، CA بیرونی، مرز فازها و تأیید Level1 |
| مستندات Microsoft CertificateRequest | مستندات API تولید PKCS#10 | ابزار استاندارد .NET؛ پروفایل CSR مرجع صدور را تعیین نمی‌کند |

این تصمیم محدوده اجرایی این فاز را نسبت به roadmap و اولویت گواهی در ADR 0007 محدود می‌کند؛ سایر تصمیمات امنیتی آنها محفوظ‌اند.

SHA-256 منابع مرجع در `docs/references/tax-authority-sdk-catalog.md` موجود است؛ در گام ۱ تطبیق با بایت اصلی انجام و نتیجه در همین سند ثبت می‌شود. به منابع وب غیررسمی برای تعیین الزام اتکا نشده است. جست‌وجوی عمومی، نسخه رسمی جاری قابل اتکایی برای اثبات استمرار مسیر قدیمی یا پروفایل CSR مرجع صدور فراهم نکرد؛ پس «آخرین نسخه عملیاتی» ادعا نمی‌شود.

**قانون منبع:** برای هر قابلیت، شناسه/نسخه/صفحه رسمی، مسیر و SHA فایل SDK، الگوریتم و padding و encoding و بردار آزمون مشخص شود. نبود اصل منبع یا تعارض حل‌نشده، همان قابلیت را مسدود می‌کند. هیچ fallback الگوریتمی خودکار وجود ندارد. سیاست ص۵ درباره API قدیمی «تا اطلاع ثانوی» است و دلیل فعال بودن امروز آن نیست. پروفایل CSR و گواهی معوق است و این فاز آن را فعال نمی‌کند.

## 3. گزینه‌ها و معماری پیشنهادی

۱. **پیشنهاد: signer تحت custody صریح کاربر در سرویس جدا از منطق تجاری، با vault/KMS و قرارداد قابل تعویض.** مناسب امضای آتی سمت سرور و هم‌راستا با قرارداد موجود؛ نیازمند مرز دسترسی عملیاتی، backup مستقل و رضایت آگاهانه است.
۲. signer کاملاً محلی/توکن سخت‌افزاری: کاهش custody سرور، اما agent محلی، نصب و bridge جدید لازم دارد؛ در این فاز فقط مرز سازگاری طراحی می‌شود و ساخت agent/پشتیبانی همه توکن‌ها خارج محدوده است.
۳. کلید رمز‌شده مستقیم در DB با master key محیطی: برای تولید توصیه نمی‌شود؛ با قرارداد vault-reference فعلی نیز اختلاف دارد. از کلید ثابت مشترک، secret در appsettings و reuse کلید MFA استفاده نمی‌شود.

تصویب برنامه به معنی انتخاب vendor یا مجوز انتقال کلید واقعی نیست. گام ۲ طرح provider واقعی و منع export خصوصی را برای تصویب در خروجی همان گام مشخص می‌کند. قبل از آماده بودن provider امن، فقط کلیدهای ephemeral تست مجازند؛ custody واقعی fail closed می‌ماند. در UI انتخاب آگاهانه «نگهداری امن در سرویس امضای آرگین» پیش از generation/import اجباری است.

### مرز مسئولیت و مسیر داده

- **Domain/Signing:** policy، lifecycle، نسخه کلید و پروفایل رسمی؛ بدون bytes خصوصی، HTTP یا SDK DTO.
- **Application/Signing:** authorization، use case و idempotency؛ `SigningScope(CustomerUserId, TaxpayerId, FiscalMemoryId)` از actor و DB معتبر ساخته می‌شود. شناسه‌های ارسالی کاربر به تنهایی scope نمی‌سازند.
- **Infrastructure/Signing:** primitiveهای .NET، parser محدود کلید، پروفایل امضای legacy نسخه‌دار و SDK adapter؛ هیچ singleton mutable بین مؤدیان.
- **Infrastructure/Security:** vault adapter و envelope protection؛ تنها مرز دارای دسترسی موقت به secret. Application فقط KeyVersionId را به signer مجاز می‌دهد، نه PEM یا vault handle دلخواه.
- **Persistence:** metadata، نسخه و تاریخچه، approval، audit و وضعیت عملیات؛ تنها opaque vault reference، هرگز private bytes. هماهنگی DB/vault با operation ID، staging و reconciliation برای جلوگیری از orphan؛ تراکنش توزیع‌شده فرض نمی‌شود.
- **Api + Next BFF:** DTO عمومی محدود، session سمت سرور، allowlist مسیر، Origin/CSRF، upload محدود و no-store. هیچ endpoint دریافت private key به شکل JSON یا arbitrary signing oracle وجود ندارد.
- **UI:** modal فشرده فارسی RTL با انتخاب کاربر نرم‌افزار و مؤدی قابل جست‌وجو، loader/skeleton، تاریخ شمسی نمایشی و UTC در قرارداد؛ fingerprint، وضعیت کلید و تفکیک وضعیت محلی از اتصال رسمی.

### قراردادهای پیشنهادی؛ تثبیت دقیق در گام ۲

`IKeyCustodyService.GenerateAsync(scope, profileId, operationId, ct)` و `ImportAsync(scope, SensitiveKeyInput, operationId, ct)` فقط KeyVersionId و public metadata برمی‌گردانند. SensitiveKeyInput نباید record قابل serialization یا ToString افشاگر باشد.
`IDigitalSigningService.SignAsync(AuthorizedSigningRequest, ct)` خروجی امضا و key/profile version دارد؛ purpose، input size و scope محدودند؛ پروفایل الگوریتم ورودی آزاد کاربر نیست.
`IKeyProtectionProvider` پشت custody، `ISigningAuditSink` و `IKeyOperationRepository` پشت application قرار می‌گیرند. قرارداد قبلی `IPrivateKeyCustody` از طریق adapter سازگار می‌شود، نه حذف ناگهانی.

مسیرهای پیشنهادی زیر خانواده `/api/dev/taxpayers/{taxpayerId}/signing/` و BFF متناظر: generate، import، public-artifacts، key-validation، versions، rotation-requests، reset-requests و history. Development exposure فعلی حفظ می‌شود؛ بازکردن production API نیازمند تصمیم انتشار است. خروجی امضای wire-facing صرفاً برای مصرف سرویس داخلی است؛ endpoint امضای متن دلخواه برای مرورگر ساخته نمی‌شود.

### مدل metadata پیشنهادی

`SigningKeyVersion`: مشتری/مؤدی/حافظه، الگوریتم و source profile، public-key fingerprint، opaque reference، version، status، revision و UTC.
Certificate metadata موجود حفظ می‌شود؛ موجودیت جدید certificate/chain در این فاز ساخته نمی‌شود.
`KeyOperation`: operation/idempotency ID، request digest غیرمحرمانه، state، vault staging reference و نتیجه قابل replay بدون secret.
`KeyChangeRequest`: نوع تغییر، reason، requester/approver، revision، expiry، مصرف یک‌باره و تاریخچه.
`SigningAuditEvent`: actor، scope، action، outcome، revision، correlation و fingerprint؛ هیچ payload خصوصی یا signed authentication token.
موجودیت‌های موجود ابتدا reuse می‌شوند؛ تغییر معنا یا backfill fingerprint قدیمی بدون بررسی ممنوع. تاریخچه حذف نمی‌شود؛ unique کلید انتخاب‌شده برای امضای جدید باید در DB و تراکنش enforce شود. جایگزینی گواهی با جایگزینی کلید یا حافظه یکسان نیست.

## 4. سیاست امنیت و عملیات

- **Encryption at rest:** پیشنهاد برنامه، نه الزام سازمان: envelope encryption با DEK تصادفی ۲۵۶بیتی برای هر نسخه و AES-256-GCM، nonce ۹۶بیتی یکتا و tag ۱۲۸بیتی؛ AAD شامل customer/taxpayer/memory/key-version و format version. DEK توسط KEK نسخه‌دار در KMS/vault wrap می‌شود. KEK خارج Git/DB/backup داده و جدا از MFA است. provider باید حفاظت کلید در حالت ذخیره، least privilege و audit داشته باشد؛ نبود provider/کلید معتبر عملیات را متوقف می‌کند.
- **پردازش secret:** تولید در boundary امن؛ import فقط upload احرازشده TLS بدون body logging یا temp plaintext. raw key/رمز فقط ورودی گذرا است؛ هرگز echo نمی‌شود. bufferهای قابل کنترل پاک و handleها dispose می‌شوند؛ پاک‌شدن قطعی همه نسخه‌های managed memory ادعا نمی‌شود. dump/telemetry و crash reporting باید redacted/محدود باشند.
- **Export:** خروجی private key در این فاز خاموش و endpoint آن ممنوع است؛ فقط public key دانلود می‌شود. export رمز‌شده خصوصی با reauth و audit نیازمند تصمیم و محدوده جداگانه آینده است.
- **Backup/Recovery:** ciphertext/metadata و vault key backup جدا، restore آزمایشی با کلید synthetic در محیط ایزوله؛ manifest نسخه‌ها و fingerprint؛ بدون KEK recovery معتبر، بازگشایی ممکن نیست و راهکار کلید جدید/ابطال از مرجع لازم است. RPO/RTO عددی باید در گام ۱۲ بر اساس deployment واقعی پیشنهاد و تصویب شوند، قبل از ادعای آمادگی عملیاتی.
- **Rotation:** KEK rewrap با تعویض کلید امضا تفاوت دارد. rotation KEK resumable و قابل rollback نسخه‌ای است؛ کلید امضای جدید تا اثبات ثبت/اتصال در فاز ۱۰ مجاز به جایگزینی حافظه عملیاتی نیست. از حذف KEK قدیم تا restore تمام backupهای مشمول خودداری می‌شود. compromised key فوراً local signing را مسدود می‌کند؛ ابطال رسمی خودکار ادعا نمی‌شود.
- **Certificate:** ورود/chain/trust/revocation و پروفایل CA معوق‌اند؛ هیچ‌کدام شرط امضای legacy نیستند و صحت metadata موجود به معنی اعتماد گواهی نیست.
- **Authorization:** Customer برای داده خود؛ Level2 فقط مشتریان فعلی خود؛ Level1 طبق scope مدیریتی موجود metadata و approval. metadata access مساوی حق sign/import/export نیست. هیچ مدیر به طور پیش‌فرض read/export secret ندارد. درخواست/تأیید تغییر از هم جدا، MFA/reauth و حق مشخص؛ مالکیت مجدداً هنگام commit و sign بررسی می‌شود.
- **Audit:** موفق/ناموفق/رد مجوز بدون body، immutable و durable؛ تغییر حساس بدون ثبت audit commit نمی‌شود. audit unavailable موجب توقف عملیات حساس است. history مشتری/Level2 redacted و scoped؛ log امنیتی سراسری Level1.
- **Concurrency:** ExpectedRevision، unique index، idempotency scoped، قفل/تراکنش و retry محدود؛ هیچ قفل صرفاً in-process برای تضمین چند instance کافی نیست. عملیات همزمان rotate/sign، transfer/sign و replay approval پوشش دارد.

### تغییر یا بازتنظیم حافظه

در این فاز: درخواست علت‌دار، بررسی scoped، تأیید مجاز و تاریخچه، مسدودسازی کلید compromised و آماده‌سازی replacement محلی. Level2 می‌تواند تغییر کلید در محدوده مشتری خود را طبق مجوز مصوب بررسی کند؛ این مجوز اجازه تغییر حافظه/مؤدی قفل‌شده سالانه نمی‌دهد. طبق قرارداد فعلی، آن استثنا فقط Level1 و در فاز ۱۲ enforce می‌شود. در فاز ۹ هیچ تأییدی وضعیت Connected/Active واقعی نمی‌سازد، annual binding را reset نمی‌کند یا سند تاریخی را بازنویسی نمی‌کند. نبود اتصال و entitlement معتبر به معنی PendingExternalVerification/BlockedByFuturePhase است، نه موفقیت صوری.

## 5. جدول ثابت پیشنهادی ۱۸ گام

گام۱ تأیید شده؛ گام۲ آماده بررسی است؛ گام‌های۳–۱۸ شروع نشده‌اند. پایان هر گام نیازمند تأیید صریح مالک است. وابستگی ترتیبی حفظ می‌شود؛ کارهای گواهی‌محور از این جدول حذف شده‌اند.

| گام | خروجی | معیار پذیرش |
|---|---|---|
| 01 | منابع و تثبیت baseline مسیر بدون گواهی | انتخاب نسخه دقیق legacy، بررسی اصل منابع و SHA؛ الگوریتم/padding/encoding مستند؛ source 0.0.30 و binary 0.0.34 مخلوط نشوند |
| 02 | معماری، custody و قراردادهای دسترسی | provider امن و رضایت، state machine و threat model؛ بدون پیش‌نیاز CSR/certificate؛ export خصوصی خاموش |
| 03 | مدل داده و طرح migration | کلید مستقل از گواهی؛ scope و version، CAS/unique/FK؛ SQL برای بررسی، بدون اجرا |
| 04 | ذخیره‌سازی عملیات، audit و idempotency | فقط پس از تأیید مستقل migration روی test DB؛ crash/retry و orphan vault/DB قابل بازیابی؛ audit durable |
| 05 | حفاظت کلید و vault/provider | envelope/AAD، missing KEK و tamper و tenant swap رد؛ private در DB نیست؛ fake فقط تست |
| 06 | تولید کلید و خروجی عمومی | کلید مطابق profile legacy تأییدشده؛ public artifact قابل دریافت؛ بدون CSR، گواهی یا private response |
| 07 | ورود کلید خصوصی قبلی | private.txt/PEM و قالب مصوب؛ malformed/weak/oversize رد؛ password گذرا فقط اگر قالب رمزدار پشتیبانی شود؛ بدون temp/log plaintext |
| 08 | تطابق private/public و هویت محلی | derive public و مقایسه canonical + challenge؛ fingerprint پایدار؛ نبود public ثبت‌شده وضعیت unverified بدهد؛ ادعای اتصال نشود |
| 09 | امضای محلی و سازگاری SDK بدون گواهی | بردارهای legacy، canonicalization و verify مثبت/منفی؛ بدون certificate، HTTP مالیاتی یا singleton مشترک مؤدیان |
| 10 | سرویس کاربردی امضا و snapshot | scope و purpose داخلی، snapshot/hash/key-version پایدار؛ draft تغییرکرده نیازمند امضای مجدد؛ بدون dispatch/Sent |
| 11 | نسخه‌بندی و چرخه عمر کلید | منع sign با compromised/revoked؛ rotate/sign اتمیک؛ replacement محلی حافظه عملیاتی را تغییر ندهد |
| 12 | پشتیبان‌گیری، recovery و KEK rotation | restore synthetic، rewrap/resume، failure بدون KEK، orphan reconciliation و runbook با RPO/RTO مصوب |
| 13 | بازبینی/بازتنظیم کنترل‌شده | reason، revision، scoped approval و مصرف یک‌باره؛ استثنای annual فقط Level1؛ اتصال/entitlement آینده دور زده نشود |
| 14 | API/BFF امن | generation/import/public artifacts/history؛ 401/403/404، CSRF، limits و no-store؛ بدون private-read/export یا generic sign endpoint |
| 15 | UI فارسی مسیر بدون گواهی | تولید یا ورود کلید، public download، fingerprint و history؛ RTL فشرده/search/loader؛ CSR/گواهی اجباری یا فعال نباشد |
| 16 | Security/Authorization/Concurrency/Regression | HTTP و PG ایزوله، transfer/session/MFA revoke، races و leak capture؛ regression فازهای ۶–۸؛ export خصوصی denied |
| 17 | پذیرش macOS و Ubuntu روی یک SHA | build/test/browser/DB guard و restore شواهددار؛ SHA برابر، clean tree و config خارجی؛ بدون سورس محیطی |
| 18 | تحویل و پایان فاز؛ انتشار مشروط | تمام signoffها و بدهی‌ها؛ تحویل قرارداد legacy به فاز۱۰؛ گواهی معیار پایان نیست؛ Merge/Tag/Release فقط با تأیید جدا |

## 6. نقشه فایل و آزمون گام‌ها

مسیرها زیر پروژه‌های src/ArginTaxPayer.* هستند و در اجرای گام پس از خواندن ساختار موجود تثبیت می‌شوند. نام آزمون‌های .NET پیشنهادی با پیشوند Phase09 و پسوند Tests هستند؛ گزارش هر گام فقط همین فایل است.

| گام | فایل/مسئولیت | آزمون پیشنهادی |
|---|---|---|
| 01 | همین سند؛ scripts/phase09-verify.sh | SourceProfile/DBGuard |
| 02 | Domain/Signing؛ Application/Signing/Contracts | Policy/Authorization |
| 03 | Infrastructure/Persistence/Signing؛ DbContext؛ Migrations | PersistenceModel |
| 04 | Infrastructure/Persistence/Signing | OperationIntegration/Audit |
| 05 | Infrastructure/Security/KeyProtection | KeyProtection |
| 06 | Infrastructure/Signing/KeyGenerationService.cs | KeyGeneration |
| 07 | Infrastructure/Signing/PrivateKeyImporter.cs | PrivateKeyImport |
| 08 | Infrastructure/Signing/KeyPairMatcher.cs | KeyPairMatching |
| 09 | Infrastructure/Signing/DigitalSigningService.cs؛ Profiles/Legacy | LegacySignatureVector/LegacySdkContract |
| 10 | Application/Signing/InvoiceSigningService.cs | InvoiceSigningSnapshot |
| 11 | Application/Signing/KeyLifecycleService.cs | KeyLifecycle/RotationConcurrency |
| 12 | Infrastructure/Security/KeyProtection؛ recovery verifier | KeyRecovery |
| 13 | Application/Signing/KeyChangeReviewService.cs | KeyChangeApproval |
| 14 | Api/Signing؛ apps/web/app/api/signing | SigningHttpSecurity؛ phase09-signing-proxy.test.mjs |
| 15 | apps/web/app/(console)/signing؛ app/signing | phase09-signing-ui.test.mjs + browser |
| 16 | tests/ArginTaxPayer.Tests/Phase09*؛ web tests | Security/Isolation/Concurrency/Regression |
| 17 | scripts/phase09-verify.sh؛ همین سند | Full + operator acceptance |
| 18 | همین سند؛ Git/GitHub پس از مجوز | Scope/diff/tag/release verification |

## 7. قرارداد انجام هر گام و تست macOS

هر گام: ۱) آزمون رفتاری متناسب، ۲) اجرای آن و مشاهده failure مورد انتظار، ۳) پیاده‌سازی حداقلی، ۴) اجرای آزمون و regression مرتبط، ۵) review diff برای secret و scope، ۶) commit قابل بازبینی، ۷) ثبت شواهد و دستورات macOS در همین سند، ۸) توقف برای تأیید مالک. گام‌های صرفاً سندی به build اجباری بی‌فایده نیاز ندارند؛ acceptance آنها review و checksum است. تولید migration با اجرای آن یک مجوز نیست.

در گام ۱ اسکریپت واحد `scripts/phase09-verify.sh` ساخته می‌شود؛ **در گام۱ ایجاد شد**. قرارداد آن: pure برای unit/source بدون DB؛ focused برای تست‌های گام با اطمینان از کشف حداقل یک تست؛ integration فقط پس از parse connection و تأیید `SELECT current_database() = 'argintaxpayer_test'`؛ full برای کل regression و web. unset بودن PG integration باید از خروجی معلوم باشد؛ skip به عنوان PASS گزارش نمی‌شود. هیچ mode migration اعمال نمی‌کند. clean و SHA پیش/پس ثبت می‌شوند؛ تست نباید secret config را چاپ کند.

الگوی دستورات macOS که انتهای هر گام با فیلتر واقعی و پیش‌نیاز دقیق تکمیل خواهد شد:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
git status --short
git rev-parse HEAD
# پس از ایجاد اسکریپت در گام ۱:
bash scripts/phase09-verify.sh pure
# فقط برای گام دارای تست integration و پس از تأیید DB/migration:
bash scripts/phase09-verify.sh integration
```

baseline کامل مستقل از اسکریپت جدید، پس از بارگذاری امن تنظیمات تست و کنترل مقصد DB:

```bash
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build
(
  cd apps/web
  npm ci --no-audit --no-fund
  npm test
  npm run typecheck
  npm run build
)
```

این دستورات اکنون اجرا نشده‌اند. full dotnet test بدون بررسی configuration برای DB اجرا نشود؛ اتصال integration نباید از dev DB به ارث برسد. شماره test count، skipped و نتیجه هر دسته ثبت شود. build موفق امنیت HTTP، chain اعتماد یا اتصال مالیاتی را اثبات نمی‌کند.
برای UI در گام مربوط، `zsh scripts/dev-up.zsh` و `zsh scripts/dev-status.zsh` تنها بعد از بررسی config مقصد تست اجرا می‌شوند؛ این سرویس‌ها ممکن است تنظیمات dev موجود داشته باشند. browser acceptance با کاربران disposable روی test DB انجام می‌شود؛ توکن‌ها در chat/command history ذخیره نمی‌شوند.

### پذیرش دو پلتفرم در گام ۱۷

از macOS ابتدا اتصال پایدار مصوب مالک:

```ini
AUTOSSH_GATETIME=0 autossh -M 0 argin-vps
```

سپس در Ubuntu:

```bash
tmux new -A -s argin
cd /home/argindev/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
git rev-parse HEAD
# پس از بررسی guard و تنظیمات argintaxpayer_test:
bash scripts/phase09-verify.sh full
bash scripts/ubuntu-dev.sh verify
bash scripts/ubuntu-dev.sh status
git status --short
```

در macOS همان SHA کامل commit ثبت و full اجرا می‌شود. اگر working tree اوبونتو تغییر داشت، آن تغییرات بدون تصمیم مالک stash/reset/commit یا merge نمی‌شوند؛ بدهی تطبیق فاز ۸ خارج محدوده باقی می‌ماند و در صورت مانع بودن، پذیرش با checkout تمیز ایزوله همان SHA و config خارجی انجام می‌شود. paths، build caches، OS trust store و key provider config نباید سورس را تغییر دهند. generated `.next`/`.next-dev`/next-env.d.ts طبق ignore فعلی بررسی شوند. مسیر بدون گواهی به chain policy یا OS certificate store وابسته نیست. امضای deterministic فقط در profile قطعی با الگوریتم deterministic مقایسه bytes می‌شود؛ در سایر موارد verify و محتوای معنایی معیار است.

## 8. خطرها و مالک آزمون

| خطر | کنترل | گام |
|---|---|---|
| سرقت DB/backup یا tenant swap | vault مستقل، AAD و least privilege؛ residual risk host/vault صریح | 02/05/12 |
| PEM مخرب، password غلط، فایل بزرگ | parser محدود، عدم echo/temp plaintext و rate limit | 07/14 |
| نبود public ثبت‌شده یا mismatch | عدم ادعای تأیید ثبت؛ match محلی از اتصال رسمی جدا | 08 |
| الگوریتم نسخه جدید در مسیر قدیمی | profile legacy با مدرک و بردار مستقل؛ بدون fallback | 01/09 |
| انتقال مالکیت یا لغو session هنگام sign | بررسی مجدد authorization و race tests | 10/16 |
| vault موفق و DB ناموفق | operation staging و reconciliation/crash test | 04/12 |
| rotation/approval همزمان یا replay | CAS/unique/transaction و مجوز مصرف یک‌باره | 11/13/16 |
| leak در log/HTTP/browser یا export | canary synthetic و capture واقعی؛ private export denied | 14/15/16 |
| DB اشتباه و cache/OS متفاوت | guard نام واقعی DB؛ config بیرونی و SHA برابر | 01/17 |

## 9. وابستگی‌ها و مرزهای قطعی

- provider عملی vault/KMS برای ادعای آمادگی تولید ضروری است؛ in-memory fake فقط تست است. vendor/زیرساخت جدید بدون تصمیم اجرایی معرفی نمی‌شود.
- از primitiveهای .NET استفاده شود؛ اضافه کردن package رمزنگاری/JOSE فقط پس از ضرورت، version pin، license/security/dependency review و بردار تطبیق. sample file-based SDK نباید اجبار به temp private PEM بسازد؛ adapter به signer protected متصل شود.
- encrypted import در صورت پشتیبانی نیازمند پروفایل PBE مصوب و آزمون interoperability است؛ عدد iteration یا الگوریتم از روی حدس در محصول ثابت نمی‌شود.
- فاز ۱۰: nonce واقعی، JWT درخواست عملیاتی، auth/connection check، دریافت public key سازمان و dispatch. گام ۹ فقط ساخت/verify آفلاین با ورودی synthetic و شبکه مسدود است. JWE/رمزگذاری payload مالیاتی تنها در حد پروفایل محلی مستند و fixture این فاز، نه دریافت زنده کلید سرور؛ implementation wire dispatch در فاز ۱۰.
- فاز ۱۲: entitlement، قفل annual و مصرف سهمیه. این فاز قراردادهای منع عملیات زودهنگام را حفظ می‌کند و سیستم billing جدید نمی‌سازد.
- CA/CSR: کل پیاده‌سازی مسیر گواهی معوق است؛ حفظ مرجع و مرز معماری تعهد به اجرای آن در این فاز ایجاد نمی‌کند.
- **خارج محدوده:** ارسال واقعی، استعلام واقعی، production deployment، تیکتینگ، اصلاح import انبوه فاز ۸، بازنویسی زیرساخت dev دو OS و ساخت agent محلی/HSM driver عمومی.
- هیچ migration/تغییر خطرناک DB بدون تأیید مستقل مالک اجرا نمی‌شود؛ تست نوشتنی فقط `argintaxpayer_test`، با داده synthetic و FK/history محفوظ.

## 10. بدهی‌های فاز ۸؛ انتقال بدون ورود به محدوده

| بدهی | وضعیت در فاز ۹ |
|---|---|
| QA عملیاتی import انبوه، partial failure و durable outcome | معوق؛ نیازمند تصمیم جداگانه مالک |
| تطبیق تغییرات محلی اسکریپت Ubuntu و same-SHA فاز ۸ | معوق؛ گام ۱۷ فقط parity تحویل فاز ۹ را بررسی می‌کند |
| checklist مرورگر احرازشده فاز ۸ بدون شواهد جزءبه‌جزء | به عنوان محدودیت شواهد حفظ می‌شود؛ بازتأیید تاریخی ادعا نمی‌شود |
| متن pending انتشار در docs فاز ۸ با وجود Release واقعی | اختلاف مستند ثبت‌شده؛ اصلاح تاریخچه تنها با تصمیم مالک |
| تیکتینگ | تا پایان کل پروژه معوق؛ وارد این فاز نمی‌شود |

## 11. دفتر اجرا و تأیید

**تصویب برنامه:** دستور مالک برای اجرای گام۱ دریافت شد. **گام ۱:** تأیید صریح مالک در 2026-10-20. **گام ۲:** طراحی آماده بررسی؛ تأیید نشده. **گام‌های ۳–۱۸:** آغاز نشده. **DB migration:** مجوز اجرا ندارد. **Merge/Tag/Release:** مجوز ندارد.

برای هر گام همین‌جا یک رکورد با فیلدهای زیر افزوده می‌شود:
شماره/عنوان ثابت؛ هدف؛ منابع دقیق و تعارض‌ها؛ فایل‌ها و commit SHA؛ تست‌های اجراشده با count و skipped؛ تست‌های اجرا‌نشده و علت؛ دستورات واقعی macOS؛ وضعیت migration/SQL/DB guard؛ شواهد redacted؛ معیارهای پذیرش تکمیل/مسدود؛ ریسک و بدهی؛ تصمیم صریح مالک با تاریخ. وضعیت «آماده بررسی» با «تأییدشده» فرق دارد.

**شرط پایان فاز:** ۱۸ گام با signoff واقعی، همه تست‌های الزامی و same-SHA تأیید، restore آزموده، منابع/profile legacy مشخص، هیچ ادعای اتصال واقعی، و بدهی‌های باقی‌مانده شفاف. تأیید عملکرد به‌تنهایی مجوز انتشار نیست. در گام ۱۸ نتیجه Merge/Tag/Release تنها پس از اجرای تأییدشده و مشاهده artifact واقعی ثبت می‌شود.

## گام 01 — تثبیت منابع و ابزار بررسی (2026-10-20)

**وضعیت:** تأییدشده با پیام صریح مالک «گام ۱ تایید هست گام ۲ را کامل کن» در 2026-10-20. جزئیات خروجی جدید macOS همراه این تأیید ارسال نشده؛ تأیید مالک ثبت می‌شود و آزمون‌های اجرا‌نشده قبلی به PASS تبدیل نمی‌شوند. دستور آغاز گام۲ دریافت شد؛ تعداد/ترتیب۱۸گام ثابت است.
**مبنای اجرای این گام:** `38b40eae93f769a04c06d807405ddf0a80322fd4`، با ancestor فاز ۸ `b8f307ed06041d902aed9d8432d3b082e1cd714a`. کار در worktree ایزوله انجام شد؛ main و checkout قبلی بازنویسی نشدند.

### تطبیق بایت منابع

اصل چهار ZIP و سه PDF بازیابی و SHA256 محاسبه شد؛ هر هفت مورد با کاتالوگ تطابق دارند. هیچ ZIP، باینری SDK یا PEM نمونه وارد Git نشد. فایل‌ها فقط به عنوان شواهد خصوصی خوانده شدند و نمونه کلیدها اجرا/مصرف نشدند.

| ID | SHA256 بایت اصلی بررسی‌شده |
|---|---|
| SDK-SOURCE-OLD 0.0.30 | `91f7b10914778b0ce13406bf09adce037c669ffd954b6db243b3f0f08528387a` |
| SDK-NUGET-OLD 0.0.34 | `8c08bc486ac7081cc1063058860c2accb3cf96ad7a50252ca1e72e230b868690` |
| SDK-SOURCE-NEW 2.0.32 | `66f2936c67c61e1c9c40660e7f9e0bd7666576b8b741fc2d5232a7a1f0d4d2f0` |
| SDK-NUGET-NEW 2.0.32 | `abc9a5ea63b7d0ac7d4fe4548d3d92f981f8ab586b28fe1f4ba500bfc8d1997d` |
| TAX-CONNECT-16 | `a4995c5a1f8766338a3816d28fd87bd3488dce6f91514557d304fe17d7eb595e` |
| TAX-SDK-GUIDE-OLD | `ef3439dcb1a8aa7b5dc1ca46dadf8ee333da01a38fe7910d483342135f1c134a` |
| TAX-SDK-GUIDE-NEW | `5c4145ae701832ea5017ff68cc1a8a9ef0cffe2761a3b5ca94ceceef1823accf` |

### پروفایل انتخاب‌شده و source → تصمیم → آزمون آتی

**شناسه پروفایل محلی پیشنهادی تثبیت‌شده:** `legacy-key-source-0.0.30-rsa-sha256-pkcs1-base64`. این شناسه مرجع رفتار آفلاین است، نه نام پروتکل رسمی یا ادعای تأیید live. بسته NuGet جدیدی نصب نشده است. منبع 0.0.30 مبنای قابل خواندن رفتار است؛ binary 0.0.34 فقط مرجع مقایسه باقی می‌ماند تا تطبیق مستقل انجام شود. اگر در آینده binary انتخاب شود باید نسخه و قرارداد آن صریح و آزموده باشد.

| منبع دقیق | نتیجه مستند | آزمون/گام مصرف‌کننده |
|---|---|---|
| راهنمای legacy ص۴–۹، به‌ویژه ص۸–۹ | مسیر SELF_TSP با کلید قبلی متناظر با public ثبت‌شده؛ فایل PEM با PRIVATE KEY و PKCS#8؛ بدون ورودی certificate | import/key match گام۷–۸؛ sign گام۹ |
| SOURCE-OLD، `TaxCollectData.Library/Business/Signatory/Pkcs8Signatory.cs` خطوط۴۵–۴۶ | UTF-8 → RSA SignData با SHA256 و Pkcs1 → Base64 معمولی؛ **نه Base64URL/JWS** | گام۹: verify با کلید صحیح، wrong key و tampered input؛ Persian/UTF-8 و Base64 padding |
| همان فایل، SHA `3e050951a98e38fd60c15d10efb9b8665a8a3e297bc3733e47401a482289a107` | Sign(string) روی bytes رشته؛ normalization مرحله جداست؛ فایل private در SDK نمونه نیازمند جایگزین امن است | گام۵/۹: signer protected، بدون temp plaintext |
| SOURCE-OLD، `Business/Signatory/InMemorySignatory.cs` SHA `2d42acc7f0e5415d93c9e9046b3b49815a12a48bc6c72b78c6ca5b4758838bc4` | همان primitive برای رشته؛ نگهداری string در SDK الگوی مناسب custody آرگین نیست | گام۲/۵/۹؛ قرارداد حداقل عمر secret |
| SOURCE-OLD، `Extensions/ServiceCollectionExtensions.cs` خطوط۱۱۱–۱۲۰، SHA `ea6cf6150d82387a291986577f7eeda70a3424d09aeeb876fbd8aa5f9d8e6f85` | apiVersion خالی → ObjectNormalizer/ObjectTransferApi؛ v1 → SimpleNormalizer/SimpleTransferApi؛ نباید مخلوط شوند | گام۹: دو fixture مستقل؛ انتخاب قرارداد wire در فاز۱۰ |
| SOURCE-OLD، `Business/Normalize/ObjectNormalizer.cs` SHA `8a0adcab1e6b8fb766d35582f1497d510c7e0b88e338abc91b4b298bd281ecdd` | flatten مسیرها با نقطه و E-index، sort کلیدها، delimiter # و escape ##، empty/null با #؛ headerها و Bearer treatment در متن منبع | گام۹: fixture `Field#0#Value` موجود در تست SDK؛ nested/array/null/#/bool/headers؛ culture و empty object باید مستقل بررسی شوند |
| SOURCE-OLD، `Business/Normalize/SimpleNormalizer.cs` SHA `9ab2ae3c6db5269a591f8eeaaebb1280a85ab94a4659ce43e8b30ccf1ff7562a` | JSON سپس الحاق token header، requestTraceId و timestamp به ترتیب موجود؛ جایگزین عمومی ObjectNormalizer نیست | گام۹ fixture جدا؛ عدم ادعای یک normalization جهانی |
| TAX-CONNECT-16 ص۵ | ذکر public key/گواهی با طول۲۰۴۸؛ برنامه تولید RSA-2048 بر این بند متکی است، نه توصیه اندازه دلخواه از SDK | گام۶ و ۹؛ استمرار پشتیبانی live مسیر قدیمی در فاز۱۰ |
| راهنمای جدید ص۴–۷ و TAX-CONNECT-16 ص۹–۱۲ | مسیر گواهی و nonce/JWS؛ SDK 2.0.32 دارای factory با private+certificate و JwsSignatory دارای x5c است | فقط مرز معماری؛ در این فاز پیاده‌سازی نمی‌شود |

**محدودیت قالب عمومی:** از قالب PKCS#8 خصوصی نمی‌توان قالب public مورد پذیرش کارپوشه را استنتاج کرد. SPKI/PKCS#1 و PEM/Base64 خروجی عمومی باید پیش از فعال‌کردن دانلود قابل ثبت در گام۶ با بند رسمی مربوط تطبیق شوند؛ تا آن زمان برچسب «تأییدشده برای کارپوشه» مجاز نیست. CSR پیش‌نیاز این بررسی نیست.

### تفاوت نسخه‌ها و مخاطرات شناسایی‌شده

- source قدیمی csproj نسخه0.0.30 و target netstandard2.0 دارد. nuspec قدیمی نسخه0.0.34 با repository commit `d986076958bbfe27f122d0745712d8666046669a` است؛ **برابری binary/source اثبات نشده**. راهنمای جدید 2.0.31 و package/source جدید 2.0.32 هستند؛ هر دو حفظ و cross-wire ممنوع.
- SOURCE-OLD، ServiceCollectionExtensions.cs خط۱۳۰: callback اعتبارسنجی TLS همواره true است. **این transport نباید وارد محصول شود.** سند مرجع امضا است، نه مجوز کپی کل SDK. کلاینت شبکه امن و live dispatch متعلق به فاز۱۰ است.
- DefaultEncryptor.cs دارای RSACng و catch با خروجی رشته `error` است؛ وابستگی OS و masking failure مناسب نیست. الگوریتم wire encryption از این نمونه به سیاست encryption-at-rest تعمیم داده نمی‌شود؛ custody مستقل است.
- ObjectNormalizer از sort و ToString استفاده می‌کند؛ اختلاف فرهنگ/اعداد و empty-object در fixture باید بررسی شوند. تعویض خودسرانه comparator ممکن است wire bytes را عوض کند؛ رفتار دقیق در گام۹ با بردارها تثبیت می‌شود.
- TaxApiService.Instance و DI singleton نمونه برای tenantهای همزمان نباید process-wide استفاده شوند؛ primitive امضا و state هر مؤدی جدا می‌مانند.
- قدیمی بودن dependencyها به تنهایی نتیجه ممیزی آسیب‌پذیری نیست. NuGet/SDK dependency adoption، security review و binary execution در این گام انجام نشدند.
- اعتبار عملیاتی امروز مسیر قدیمی و قرارداد دقیق public artifact هنوز gateهای اعلام‌شده‌اند؛ هیچ endpoint مالیاتی صدا زده نشد. این موارد مانع ثبت ممیزی منابع نیستند، ولی مانع ادعای قابلیت live یا قالب عمومی تأییدشده هستند.

### ابزار تحویل‌شده

- `scripts/phase09-verify.sh`: wrapper Bash سازگار با macOS/Ubuntu، بدون source فایل env خودکار.
- `scripts/phase09_verify.py`: preflight ancestry/clean tree/۱۸ گام و SHA؛ modeهای pure/focused/integration/full؛ منع match تهی در discovery و شمارش TRX؛ هیچ migration یا startup سرویس.
- `tests/phase09_verify_test.py`: هشت آزمون ایمنی parser، exact DB، alias تکراری، quoted password، پورت/loopback، mismatch نام واقعی، redaction خطای provider، حذف تنظیمات inherited و empty discovery. تست guard از subprocess جایگزین استفاده می‌کند؛ **اتصال DB واقعی را اثبات نمی‌کند**.

`preflight` فقط ساختار و SHA را بررسی می‌کند و به .NET/DB نیاز ندارد. `pure` build/.NET tests/web را با حذف ConnectionStrings و PG* و integration=0 اجرا می‌کند؛ تست‌های قدیمی opt-in که early return می‌کنند همچنان ممکن است در xUnit passed شمارده شوند، بنابراین این mode هرگز پذیرش DB نیست. `focused --filter` نیز بدون DB است. `integration` و `full` ابتدا connection string را با subset صریح و نام دقیق test DB/loopback parse و سپس read-only `SELECT current_database()` با psql اجرا می‌کنند؛ در صورت شکست، هیچ build/test شروع نمی‌شود. password در argv یا خروجی guard قرار نمی‌گیرد. تنظیمات ناشناخته یا alias تکراری به جای حدس رد می‌شوند؛ service/options/PGSERVICE ارثی مجاز نیستند.

### شواهد اجرا و محدودیت محیط

- Python safety tests: **8/8 PASS**؛ Bash syntax: **PASS**؛ preflight روی checkout تمیز: **PASS**. این‌ها آزمون ابزار هستند، نه آزمون cryptography محصول.
- اصل هفت فایل: checksum **7/7 MATCH**؛ مسیرهای منبع بالا خوانده شدند؛ هیچ باینری SDK اجرا نشد.
- .NET build/test: **اجرا نشده**؛ dotnet در این محیط نصب نیست. PG واقعی: **اجرا نشده**؛ دیتابیس/credential تست در اختیار این اجرا نیست.
- پذیرش macOS، Ubuntu و browser: هنوز **انجام نشده**؛ در این گام UI یا runtime محصول تغییر نکرده است.
- Web regression: پس از نصب دقیق lockfile با npm ci، **187/187 PASS، صفر failed/skipped**. اجرای اولیه قبل از نصب dependency به دلیل نبود typescript در mfa-proxy.test.mjs شکست خورد؛ پس از npm ci همان suite بدون تغییر سورس سبز شد. Typecheck/build وب در این گام ادعا نشده‌اند.

### دستورات پذیرش گام۱ روی macOS

این گام تغییر UI، DB یا primitive رمزنگاری محصول ندارد؛ تست ضروری آن ممیزی مستند و ابزار safety/preflight است. از ریشه مخزن:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
git status --short
git log -1 --oneline
python3 tests/phase09_verify_test.py
bash -n scripts/phase09-verify.sh
bash scripts/phase09-verify.sh preflight
```

انتظار: هشت تست safety موفق، syntax بدون خطا، checkout تمیز، ancestry صحیح و جدول۱۸گامی. برای baseline کامل بدون DB روی مک با .NET SDK مطابق global.json و Node/npm نصب‌شده:

```bash
bash scripts/phase09-verify.sh pure
```

این دستور بدون DB، restore/build/.NET tests و npm ci/test/typecheck/build را اجرا می‌کند و اتمام آن پذیرش دیتابیس یا امضای مالیاتی نیست. فعلاً integration/full را برای گام۱ اجرا نکنید؛ migration لازم نیست و تنظیم credential یا اتصال DB برای پذیرش این گام ضروری نیست.

**معیارهای پذیرش گام۱:** منابع۷گانه با SHA منطبق؛ profile legacy از JWS جدا؛ اختلاف نسخه‌ها و مخاطرات TLS/OS/culture ثبت؛ هیچ private key یا SDK binary در diff؛ safety tests و preflight در macOS موفق؛ هیچ Migration یا اتصال مالیاتی. نتایج macOS و تأیید صریح مالک برای تغییر وضعیت به «تأییدشده» لازم است.

## گام 02 — معماری custody، قراردادها و مجوزها (2026-10-20)

**وضعیت:** طراحی تکمیل و آماده بررسی مالک؛ گام۲ هنوز تأیید نشده و گام۳ آغاز نشده است.
**مبنای بررسی:** commit `28b978664469c8f417891e41a4148933df0a3dd3`؛ سند فاز، CertificateCustodyPolicy و ISecretProvider مستقیماً از شاخه خوانده شدند. این گام خروجی معماری و قرارداد طراحی دارد؛ provider اجرایی، policy runtime، schema و endpoint در این گام پیاده‌سازی/نصب نمی‌شوند. رفتارهای تعریف‌شده زیر قرارداد گام‌های۳–۱۶ هستند، نه ادعای اجرای آنها.
**دامنه:** فقط مسیر بدون گواهی؛ هیچ CSR، certificate یا ارسال واقعی پیش‌نیاز آن نیست. ثبت تأیید گام۱ در همین تغییر انجام شد.

### 02.1 تصمیم معماری قابل پیاده‌سازی

**انتخاب طراحی:** custody سمت سرور با رضایت مالک، primitiveهای .NET و envelope در یک مرز اختصاصی `ProtectedKeyCustody`؛ adapter پیشنهادی برای اجرای گام۵، **Vault Transit برای wrap/unwrap کلید رمزگذاری + KV v2 برای نگهداری envelope رمز‌شده** است. این انتخاب طرح provider است، نه مجوز خرید، نصب، ارسال کلید واقعی یا ادعای آماده‌بودن Vault. نسخه server/client هنگام پیاده‌سازی گام۵ pin و license/dependency بررسی می‌شود؛ latest به عنوان نسخه اجرایی پذیرفته نیست.

Transit محل ذخیره ciphertext برنامه نیست؛ envelope باید در KV نگهداری شود. KEK در Transit مدیریت می‌شود؛ برنامه KEK خام را دریافت نمی‌کند. DEK فقط در buffer گذرا داخل custody وجود دارد. DB تجاری تنها reference داخلی و metadata عمومی را نگه می‌دارد. مدل موجود ExternalVaultReference حفظ می‌شود. ذخیره ciphertext کلید در جدول certificate، قرار دادن master key در appsettings یا استفاده از کلید MFA مجاز نیست.

برای developer/test: adapter ephemeral فقط با کلید synthetic و عمر process؛ توقف process به معنی از دست رفتن داده تست است و پیام محصول نباید آن را «نگهداری پایدار» بنامد. برای آزمون پایداری گام۵، Vault اختصاصی test با credential و namespace جدا لازم است. **اگر provider واقعی موجود نباشد، generate/import واقعی و sign با کلید واقعی مسدودند؛ fallback فایل یا master key محیطی نداریم.**

انتخاب provider قابل جایگزینی است؛ تغییر آن نیازمند حفظ قرارداد حفاظت و review مالک است، نه تغییر تعداد گام‌ها. signer محلی، driver توکن و HSM در این فاز ساخته نمی‌شوند.

| جزء | مسئولیت | نباید انجام دهد |
|---|---|---|
| Domain/Signing | state transition، پروفایل مجاز، سیاست دسترسی خالص | دریافت secret bytes، SDK DTO یا HTTP |
| Application/Signing | resolve scope، رضایت، workflow، revision و idempotency | دریافت KEK یا بازگرداندن private |
| ProtectedKeyCustody در Infrastructure/Security | تولید/parse موقت کلید، حفاظت envelope، بازگشایی گذرا و dispose | ثبت secret در DTO عمومی، فایل موقت یا log |
| Infrastructure/Signing | امضای primitive و profile آفلاین legacy با key handle تحت custody | انتخاب الگوریتم از ورودی آزاد یا network dispatch |
| Persistence/Signing | metadata، operation، audit و review در DB تجاری | ذخیره private، password، DEK یا KEK |
| Vault adapter | حفاظت و CAS envelope در KV، wrap/unwrap DEK در Transit | قبول vault path/URL دلخواه کاربر |
| Api/BFF | احراز هویت، Origin، محدودیت upload، DTO عمومی | API عمومی Sign(bytes)، endpoint export private |
| UI | تولید/ورود کلید، public artifact، fingerprint و history | private preview، localStorage/IndexedDB یا تضمین اتصال رسمی |

این مرز در طراحی ابتدا یک ماژول با DI مشخص است؛ **معادل isolation سخت‌افزاری یا process مستقل نیست**. مهاجم دارای کنترل process custody ممکن است secret گذرا را بخواند. جداسازی process/host در deployment و آزمون آن در گام۵/۱۲ بررسی می‌شود؛ بدون اجرای آن ادعای جداسازی فیزیکی نداریم.

### 02.2 قالب حفاظت و fingerprint

این سیاست امنیت محصول است، نه الزام الگوریتم مالیاتی:

- برای هر KeyVersion یک DEK تصادفی۳۲بایتی با CSPRNG؛ AES-256-GCM، nonce۱۲بایتی و tag۱۶بایتی؛ هر encryption از DEK تازه استفاده می‌کند، از DEK قبلی برای encrypt دوباره استفاده نمی‌شود.
- plaintext داخل custody، DER PKCS#8 نرمال‌شده است؛ import PEM تنها ورودی گذراست. کلید و bufferهای قابل کنترل در finally پاک و RSA handle dispose می‌شوند؛ managed memory و crash dump «قطعاً پاک‌شده» فرض نمی‌شوند.
- AAD به صورت bytes ثابت: ASCII `argin:key:v1`، سپس CustomerUserId، TaxpayerId، FiscalMemoryId، KeyVersionId هرکدام UUID در قالب canonical lowercase D و جداکننده `|`. همه شناسه‌ها سرورساخته/سرورتأییدشده‌اند. تغییری در مالک تجاری Level2، CustomerUserId مالک کلید را تغییر نمی‌دهد.
- envelope: FormatVersion=1، AlgorithmId، Nonce، Ciphertext، Tag، WrappedDek، ProtectionKeyVersion و opaque KeyVersionId؛ AAD از metadata معتبر بازسازی و بررسی می‌شود. format نامعلوم، mismatch یا tamper موجب رد قطعی است.
- fingerprint داخلی public key: SHA-256 روی DER SubjectPublicKeyInfo، lowercase hex۶۴کاراکتری. این قرارداد **قالب مورد پذیرش کارپوشه را تعیین نمی‌کند**؛ gate گام۶ محفوظ است. fingerprint گواهی قدیمی تغییر معنا نمی‌دهد.
- plaintext/DEK/password در audit، trace، exception، request dump، analytics یا command argv ممنوع‌اند. envelope و vault reference نیز در response عمومی و log عمومی نمی‌آیند.
- رمز import اگر بعداً پشتیبانی شود فقط در ورودی گذراست؛ export خصوصی در کل فاز خاموش است. password workflow و PBE تا گام۷ بدون حدس فعال نمی‌شوند.

### 02.3 اعتبار سرویس و دسترسی عملیاتی Vault

- Api/BFF credential مستقیم Vault ندارد؛ فقط custody client یا مرز داخلی مجاز به provider دسترسی دارد. اگر در گام۵ یک process مشترک اجرا شود، این موضوع و ریسک credential مشترک صریح ثبت می‌شود.
- workload identity/credential با عمر محدود و renew کنترل‌شده، از منبع خصوصی بیرون Git؛ مقدار root/admin token در environment برنامه ممنوع. ISecretProvider فقط bootstrap reference/credential را در boundary مجاز فراهم می‌کند؛ SecretValue.ToString راهکار جلوگیری از serialization نیست.
- سیاست Vault برای prefix اختصاصی آرگین: read/create/update envelope لازم، wrap/unwrap با KEK مشخص؛ هیچ مجوز root، list سراسری، export KEK، تغییر policy یا destroy تاریخچه به runtime داده نمی‌شود.
- مسیرها از mapping داخلی KeyVersionId ساخته می‌شوند؛ client path ارسال نمی‌کند. نام مؤدی/کدملی در path قرار نمی‌گیرد. TLS معتبر الزامی است؛ callback همواره true SDK نمونه بازاستفاده نمی‌شود.
- soft-delete/destroy و KEK rotation دسترسی اپراتوری جدا از workload دارند؛ restore نیازمند runbook و audit است. compromise یا outage هیچ fallback plaintext ایجاد نمی‌کند.
- مجوز تجاری داخل نرم‌افزار و ACL زیرساخت دو کنترل جدا هستند؛ ACL سرویس به تنهایی tenant authorization نیست. credential مشترک با دسترسی تمام namespace یک residual risk است و ادعای «اپراتور زیرساخت هرگز نمی‌تواند کلید را ببیند» مطرح نمی‌شود.

### 02.4 رضایت و ماتریس دسترسی قطعی

Customer در جدول یعنی مالک حساب نرم‌افزار که Taxpayer به او تعلق دارد. اگر یک مدیر مؤدی متعلق به حساب خودش دارد، از مسیر مالکیت همان حساب عمل می‌کند؛ نقش مدیر به‌خودی‌خود دسترسی custody مشتری ایجاد نمی‌کند.

| عملیات | Customer مالک | Level2 فعلی مشتری | Level1 دارای scope | سرویس داخلی |
|---|---|---|---|---|
| metadata/status و public artifact/history تجاری | مجاز | فقط scope فعلی، redacted | طبق scope موجود، redacted | فقط نیاز کاری |
| generate/import کلید مالک | با رضایت صریح و reauth | پیش‌فرض ممنوع | پیش‌فرض ممنوع | فقط اجرای command معتبر مالک |
| درخواست تغییر/rotation | برای داده خود | درخواست علت‌دار در scope | درخواست علت‌دار در scope | خودسرانه ممنوع |
| تأیید تغییر محلی با مجوز KeyChangeReview | تأیید مدیریتی ندارد | scope فعلی + MFA، غیر از annual reset | scope + MFA، شامل بررسی annual exception | فقط مصرف approval معتبر |
| sign روی snapshot مجاز | ایجاد درخواست محلی خود | پیش‌فرض ممنوع | پیش‌فرض ممنوع | فقط مصرف context معتبر مالک |
| مشاهده/download private یا password | ممنوع | ممنوع | ممنوع | هیچ API export؛ فقط استفاده گذرا در custody |
| unwrap DEK/خواندن KV | ممنوع | ممنوع | ممنوع | workload custody با حداقل ACL |
| تغییر حافظه عملیاتی یا annual binding | در فاز۹ ممنوع | در فاز۹ ممنوع | در فاز۹ ممنوع | تا قابلیت مجاز فاز۱۰/۱۲ ممنوع |

واگذاری تجاری، حق امضا به مدیر نمی‌دهد. approval مدیر اجازه تولید/ورود کلید به جای مالک نیست؛ بعد از approval لازم، **خود مالک** درخواست generation/import و رضایت را انجام می‌دهد. requester و approver یک شخص نباشند؛ self-approval حتی با دو نقش رد می‌شود. تغییر annual در قرارداد فعلی نیازمند Level1 است؛ تأیید Level2 به آن تعمیم داده نمی‌شود.

**رضایت custody:** ثبت نسخه متن، actor، scope، زمانUTC و operation ID پیش از generate/import، با توضیح «کلید خصوصی به‌صورت امن در سرویس آرگین نگهداری می‌شود؛ خروجی خصوصی در این نسخه ارائه نمی‌شود». checkbox از پیش فعال یا رضایت مدیر جایگزین مالک نیست. generation/import بدون رضایت معتبر رد می‌شود.

**reauth:** برای generate/import و درخواست تغییر حساس، احراز مجدد حداکثر۵دقیقه قبل؛ روش‌های ورود تأییدشده موجود (password/OTP و MFA در صورت الزام حساب). موفقیت session قدیمی به تنهایی کافی نیست. توکن reauth به actor، session و purpose متصل است. این مدت سیاست محصول است نه قانون سازمان مالیاتی. برای کار پس‌زمینه آینده، قرارداد workload و رضایت مجزا در فاز۱۰ لازم است؛ credential کاربر در worker ذخیره نمی‌شود.

### 02.5 scope و قراردادهای طراحی تثبیت‌شده

این بخش مشخصات پیاده‌سازی آینده است؛ **کلاس/interface اجرایی جدید در این commit وجود ندارد**.

- `SigningScope(Guid CustomerUserId, Guid TaxpayerId, Guid FiscalMemoryId)`: فقط Application resolver از actor/session و رابطه واقعی DB می‌سازد. active بودن مؤدی، رابطه حافظه با مؤدی، CustomerUserId و مالکیت فعلی پیش از عملیات و هنگام commit/sign دوباره کنترل می‌شوند.
- `GenerateKeyCommand`: TaxpayerId، FiscalMemoryId، ProfileId، OperationId، ConsentVersion، ReauthReference و ExpectedRevision. CustomerUserId از payload معتبر نمی‌شود؛ سرور resolve می‌کند.
- `ImportKeyCommand`: metadata همان بالا + ورودی disposable `SensitiveKeyInput`. کلاس sensitive یک byte-buffer محدود دارد، record نیست، public getter قابل serialization برای bytes ندارد، ToString فقط redacted و Dispose پاک‌کننده است؛ API model عمومی و storage model آن نیست.
- `IKeyCustodyService.GenerateAsync(SigningScope, GenerateKeyCommand, CancellationToken) -> Task<KeyOperationResult>`.
- `IKeyCustodyService.ImportAsync(SigningScope, ImportKeyCommand, SensitiveKeyInput, CancellationToken) -> Task<KeyOperationResult>`.
- `KeyOperationResult`: Status، OperationId، optional KeyVersionId و PublicKeyView؛ فاقد private، ciphertext، vault URL/reference، password و DEK. نتیجه public-only قابل replay است.
- `PublicKeyView`: KeyVersionId، PublicFingerprintSha256، AlgorithmProfileId، LifecycleState، PortalVerificationState، Revision و PublicArtifactAvailability. artifact فقط پس از تعیین encoding در گام۶.
- `IKeyProtectionProvider.WrapDataKeyAsync/UnwrapDataKeyAsync`: فقط در custody، با ProtectionContext معتبر و buffer disposable؛ در Application/API expose نمی‌شود. بدنه این قرارداد و نسخه پروتکل Vault در گام۵ با API نسخه pin‌شده تثبیت می‌شود.
- `IKeyOperationRepository`: reserve operation، compare revision، complete metadata+audit در یک transaction؛ داده private دریافت نمی‌کند.
- `ISigningAuditSink`: رویداد ثابت با EventId/OperationId/Actor/Scope/Action/Outcome/UTC/Revision/Fingerprint؛ payload آزاد و exception serialization ندارد.
- `IDigitalSigningService.SignAsync(AuthorizedSigningRequest, CancellationToken) -> Task<SigningResult>`: فقط مصرف داخلی؛ درخواست شامل SnapshotId/Hash، Scope، KeyVersionId، ProfileId، ExpectedRevision و AuthorizationContext داخلی است. payload صورتحساب را سرویس از snapshot مورد اعتماد resolve می‌کند؛ متن دلخواه browser پذیرفته نمی‌شود.
- `SigningResult`: signature به Base64 عادی، ProfileId، KeyVersionId و SnapshotHash؛ پاسخ مصرف‌کننده داخلی است، نه public download توکن auth. امضای nonce واقعی و توکن مالیاتی در فاز۹ ایجاد نمی‌شود.

قرارداد موجود IPrivateKeyCustody حذف نمی‌شود. adapter آینده باید FiscalMemoryId را به scope قانونی resolve کند؛ هیچ endpoint مستقیم آن و هیچ `GetPrivateKey` اضافه نمی‌شود. fingerprint هر بار از public واقعی مشتق می‌شود؛ caller نمی‌تواند مقدار fingerprint دلخواه را به عنوان شاهد تطابق ثبت کند.

### 02.6 ماشین وضعیت و gate خارجی

دو محور مستقل داریم: LifecycleState و PortalVerificationState. lifecycle هیچ‌گاه Connected یا TaxAuthorityAccepted نامیده نمی‌شود.

| وضعیت مبدأ | عملیات مجاز | مقصد | شرط |
|---|---|---|---|
| نبود کلید | generate/import | Staged | مالک، رضایت، reauth، reserve operation و provider آماده |
| Staged | validate+seal+DB audit commit | LocallyReady | کلید valid و envelope پایدار و metadata هماهنگ |
| Staged | شکست یا timeout | Failed/RecoveryRequired در operation | هیچ کلید انتخاب‌شده قابل استفاده ایجاد نشود |
| LocallyReady | select برای تست/امضای محلی | SelectedLocal | مالک، revision و unique؛ sign فقط purpose محلی |
| SelectedLocal | suspend | Suspended | actor مجاز یا safety stop؛ علت و audit |
| SelectedLocal/LocallyReady/Suspended | compromise | Compromised | توقف فوری sign؛ append-only history |
| SelectedLocal | جایگزینی محلی مصوب | Superseded | انتخاب نسخه جدید و بستن نسخه قبلی اتمیک |
| Suspended | بررسی و resume محلی | LocallyReady | revision، approval لازم و رضایت مالک؛ selection مجدد جدا |
| Compromised/Superseded | sign یا فعال‌کردن مجدد | رد | terminal برای انتخاب و امضای جدید |

PortalVerificationState در این فاز `Unverified` یا `AwaitingExternalVerification` است. ارائه public key توسط کاربر فقط `MatchesProvidedPublicKey` را به عنوان نتیجه بررسی محلی ثبت می‌کند؛ به معنی ثبت یا اعتبار رسمی نیست. هیچ approval محلی PortalVerified ایجاد نمی‌کند.

نسخه جدید می‌تواند برای آزمون محلی انتخاب شود؛ این کار binding حافظه عملیاتی، سابقه invoiceها یا key-version اسناد قبلی را تغییر نمی‌دهد. امضای آنلاین آینده باید gate مستقل تأیید رسمی کلید همان حافظه و entitlement را بگذراند.

### 02.7 transaction، replay، revoke و race

1. Reserve operation با کلید یکتای CustomerUserId/TaxpayerId/FiscalMemoryId/OperationId و request metadata digest غیرمحرمانه. digest هرگز hash private/password نیست؛ fingerprint public پس از parse می‌تواند به نتیجه اضافه شود.
2. provider با opaque operation/key-version ID و create-if-absent/CAS یک envelope staging ایجاد می‌کند. retry برای همان operation نباید کلید دوم generate کند؛ پس از timeout ابتدا وضعیت provider reconcile می‌شود.
3. DB transaction metadata/version/selection و audit موفق را اتمیک می‌نویسد؛ پیش از commit مالکیت/session/revision مجدداً بررسی می‌شود. هیچ فرض transaction توزیع‌شده بین DB و Vault نداریم.
4. envelope بدون DB commit فقط orphan/staged است و sign نمی‌شود؛ reconciliation بر اساس operation ID آن را complete یا quarantine می‌کند. cleanup خودکار evidence و نسخه فعال را حذف نمی‌کند.
5. scope انتقال‌یافته، session revoked، stale revision یا تغییر profile بین reserve و commit: fail closed؛ actor قدیمی حق اتمام ندارد.
6. برای sign مجاز، ابتدا durable intent ثبت؛ انتخاب نسخه و مجوز در نقطه پذیرش sign تحت هماهنگی transaction/revision تثبیت می‌شود. suspension قبل از این نقطه sign را رد می‌کند؛ suspension بعد از پذیرفته‌شدن ممکن است امضای درحال‌اجرای همان snapshot را لغو نکند. نتیجه پس از fail audit/publication تحویل نمی‌شود. ادعای «لغو امضای تولیدشده در گذشته» نداریم.
7. approval تغییر یک‌باره، scope-bound، revision-bound و برای این طراحی دارای اعتبار۲۴ساعته است؛ مصرف اتمیک با command مالک و رویداد audit. approval منقضی یا مصرف‌شده replay نمی‌شود. این مدت سیاست داخلی محصول است.
8. نبود DB/audit/provider اجازه نتیجه موفق نمی‌دهد. عملیات failed و denied بدون secret ثبت می‌شوند؛ اگر audit مرکزی unavailable باشد، رویداد حداقلی redacted در operational sink محدود ثبت و عملیات حساس متوقف می‌شود؛ این sink جای business audit اتمیک نیست.

### 02.8 threat model و آزمون‌های الزامی مصرف‌کننده

| تهدید | کنترل طراحی | شاهد اجرایی لازم در گام مرتبط |
|---|---|---|
| IDOR و Level2 قبلی | server scope + recheck ownership/session | گام۱۴/۱۶: HTTP واقعی و انتقال همزمان |
| admin با قصد خواندن/استفاده کلید | deny generate/import/sign/export برای حساب دیگر؛ ACL مجزا | گام۱۴/۱۶: metadata allowed و sign/export denied |
| DB dump یا backup سرقت‌شده | فقط opaque reference؛ envelope در Vault و KEK مستقل | گام۵/۱۲: restore/absence-of-key tests |
| ciphertext/AAD جابه‌جا بین مؤدیان | scope-bound AAD و version | گام۵: tamper و tenant swap قطعی rejected |
| مهاجم با دسترسی runtime custody | کوتاه‌کردن عمر secret، credential محدود، dump/telemetry policy | گام۵/۱۶: redaction؛ residual risk host admin محفوظ |
| CSRF، upload بزرگ و PEM بدساخت | BFF Origin و parser bounded و no-store | گام۷/۱۴: HTTP و parser negative tests |
| replay و double generation | reserve operation و provider CAS/reconcile | گام۴/۱۶: concurrent retry/crash |
| rotate/revoke حین sign | نقطه پذیرش تعریف‌شده و revision/selection | گام۱۱/۱۶: دو ترتیب race و تاریخچه |
| outage Vault/audit و timeout | fail closed بدون fallback plaintext | گام۵/۱۲/۱۶: fault injection |
| اختلاط JWS و legacy | profile version ثابت از گام۱ | گام۹: bytes/verify و non-JWS format |
| import private بدون public ثبت‌شده | وضعیت Unverified صریح | گام۸/۱۵: match محلی با registration یکی نشود |
| استثنای annual توسط Level2 | approval محلی از annual binding جدا | گام۱۳/۱۶: deny و عدم تغییر حافظه |

### 02.9 backup، rotation و مرز تحویل

Backup به سه مجموعه مستقل نیاز دارد: DB metadata+audit، KV envelopeها و اطلاعات operation، و recovery قابل استفاده KEK/Vault با کنترل دسترسی مستقل. صرفاً backup DB بازیابی کلید را ممکن نمی‌کند. RPO/RTO، cadence، محل backup و آزمون restore واقعی در گام۱۲ با deployment واقعی تصویب می‌شوند؛ اکنون disaster recovery آماده ادعا نمی‌شود.

rotation KEK با rewrap WrappedDek و حفظ private/public fingerprint انجام می‌شود؛ rotation کلید امضا یک KeyVersion و public جدید ایجاد می‌کند. فعال‌سازی خارجی کلید جدید در حافظه فعلی تا قابلیت تأیید فاز۱۰ مسدود می‌ماند. حذف نسخه KEK تا تعیین backup retention و restore gate مجاز نیست. compromised key به صورت محلی بسته و کاربر برای روال رسمی تعویض هدایت می‌شود؛ «ابطال رسمی خودکار» وجود ندارد.

CSR/گواهی، export private، real auth/nonce، ارسال صورتحساب، entitlement، تیکتینگ و بدهی import فاز۸ وارد این گام نشدند. هیچ migration تولید یا اجرا نشد و جداول certificate فعلی تغییر نکردند.

### 02.10 منابع طراحی و شواهد

منابع مالیاتی و legacy همان شناسه/صفحات و SHAهای گام۱ هستند؛ این گام الزام مالیاتی جدیدی استنتاج نمی‌کند. منابع زیر فقط قابلیت provider و primitive عمومی را تأیید می‌کنند، نه تأیید محصول یا انطباق مالیاتی:
- HashiCorp، Transit secrets engine: https://developer.hashicorp.com/vault/docs/secrets/transit — Transit داده ارسالی برنامه را ذخیره نمی‌کند؛ encryption/key versioning را از storage جدا طراحی کرده‌ایم.
- HashiCorp، KV v2: https://developer.hashicorp.com/vault/docs/secrets/kv/kv-v2 — مرجع backend نگهداری نسخه‌دار envelope.
- Microsoft، AesGcm در .NET9: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-9.0 — مرجع primitive؛ پارامترهای envelope بالا سیاست داخلی آرگین‌اند.

**بازبینی طراحی:** جدول۱۸گام بدون تغییر؛ دامنه بدون گواهی حفظ؛ نقش‌ها و reauth، export-deny، provider/failure، AAD، وضعیت محلی/خارجی، race و backup به گام‌های اجرایی مالک متصل شدند. فقط همین فایل تغییر کرده است.
**محدودیت اجرا:** محیط پروژه در این نوبت unavailable است؛ build، Python/.NET tests، preflight، اتصال provider و DB اجرا نشدند. آمار PASS گام۱ شواهد همان گام است، نه اجرای مجدد این گام. تست‌های policy/authorization فوق قرارداد آزمون‌های مراحل اجرا هستند و در این commit تست اجرایی جدید ساخته نشده است.
**وضعیت تأیید:** آماده review معماری؛ قبول مالک در پیام بعدی لازم است. قبول این طراحی مجوز provisioning Vault، import secret واقعی، migration یا انتشار main نیست.

### دستورات بررسی macOS و معیار پذیرش گام۲

از ریشه مخزن:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
git status --short
git log -1 --oneline
git show --stat --oneline HEAD
git diff HEAD^ HEAD --check
python3 tests/phase09_verify_test.py
bash scripts/phase09-verify.sh preflight
```

انتظار: فقط `docs/phases/phase-09.md` در تغییر این گام؛ هشت safety test ابزار موجود موفق و preflight با checkout تمیز/۱۸گام موفق. این آزمون‌ها معماری یا runtime authorization/provider را اثبات نمی‌کنند.

معیار پذیرش اصلی این گام، review و تأیید طراحی بالاست: رضایت صریح مالک، deny پیش‌فرض مدیریتی برای custody/sign، provider مستقل از DB، KEK جدا از MFA، حالات local/external جدا و fail-closed. نیازی به اجرای migration، ساخت Vault، تولید کلید واقعی یا تکرار full build برای این تغییر صرفاً سندی نیست. پس از تأیید گام۲، گام۳ فقط مدل داده و SQL پیشنهادی را برای بررسی آماده می‌کند.
