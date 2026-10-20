# ArginTaxPayer — Phase 09: Cryptographic Keys, CSR & Digital Signing

**وضعیت:** پیشنهاد برنامه برای تصویب مالک؛ هیچ گام اجرایی آغاز یا تأیید نشده است.
**تاریخ بررسی:** 2026-10-20
**Repository:** HekmatBeigverdi/ArginTaxPayer
**Base:** main — `b8f307ed06041d902aed9d8432d3b082e1cd714a`
**Previous release:** `phase-08-v1.0.0`
**Branch:** `phase/09-cryptographic-keys-signing`
**تعداد پیشنهادی ثابت:** ۲۴ گام، شامل پذیرش نهایی و انتشار مشروط. پس از تصویب، تغییر تعداد، ترتیب یا محدوده نیازمند تأیید صریح مالک است. زیرکار و رفع نقص داخل گام مرتبط ثبت می‌شود، نه با افزودن گام جدید.
**Goal:** نگهداری امن و ایزوله کلید، تولید و ورود کلید، CSR، اعتبارسنجی گواهی و امضای محلی مبتنی بر منابع رسمی؛ بدون اتصال یا ارسال واقعی صورتحساب.
**روش اجرا:** اجرای مستقیم گام‌به‌گام با مهارت executing-plans پس از تصویب؛ توقف در پایان هر گام تا تأیید صریح مالک. این سند هم طراحی و هم برنامه و دفتر شواهد فاز است؛ فایل موازی برای گزارش هر گام ساخته نمی‌شود.

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

SHA-256 منابع مرجع در `docs/references/tax-authority-sdk-catalog.md` موجود است؛ در گام ۱ تطبیق با بایت اصلی انجام و نتیجه در همین سند ثبت می‌شود. به منابع وب غیررسمی برای تعیین الزام اتکا نشده است. جست‌وجوی عمومی، نسخه رسمی جاری قابل اتکایی برای اثبات استمرار مسیر قدیمی یا پروفایل CSR مرجع صدور فراهم نکرد؛ پس «آخرین نسخه عملیاتی» ادعا نمی‌شود.

**قانون منبع:** برای هر قابلیت، شناسه/نسخه/صفحه رسمی، مسیر و SHA فایل SDK، الگوریتم و padding و encoding و بردار آزمون مشخص شود. نبود اصل منبع یا تعارض حل‌نشده، همان قابلیت را مسدود می‌کند. هیچ fallback الگوریتمی خودکار وجود ندارد. سیاست ص۵ درباره API قدیمی «تا اطلاع ثانوی» است و دلیل فعال بودن امروز آن نیست. CSR حقیقی/حقوقی فقط پس از تأیید پروفایل رسمی CA شامل OIDها، encoding، subject و extensionها فعال می‌شود؛ پروفایل ساختگی یا گواهی self-signed به عنوان معتبر ارائه نمی‌شود.

## 3. گزینه‌ها و معماری پیشنهادی

۱. **پیشنهاد: signer تحت custody صریح کاربر در سرویس جدا از منطق تجاری، با vault/KMS و قرارداد قابل تعویض.** مناسب امضای آتی سمت سرور و هم‌راستا با قرارداد موجود؛ نیازمند مرز دسترسی عملیاتی، backup مستقل و رضایت آگاهانه است.
۲. signer کاملاً محلی/توکن سخت‌افزاری: کاهش custody سرور، اما agent محلی، نصب و bridge جدید لازم دارد؛ در این فاز فقط مرز سازگاری طراحی می‌شود و ساخت agent/پشتیبانی همه توکن‌ها خارج محدوده است.
۳. کلید رمز‌شده مستقیم در DB با master key محیطی: برای تولید توصیه نمی‌شود؛ با قرارداد vault-reference فعلی نیز اختلاف دارد. از کلید ثابت مشترک، secret در appsettings و reuse کلید MFA استفاده نمی‌شود.

تصویب برنامه به معنی انتخاب vendor یا مجوز انتقال کلید واقعی نیست. گام ۲ طرح provider واقعی و سیاست export را برای تصویب در خروجی همان گام مشخص می‌کند. قبل از آماده بودن provider امن، فقط کلیدهای ephemeral تست مجازند؛ custody واقعی fail closed می‌ماند. در UI انتخاب آگاهانه «نگهداری امن در سرویس امضای آرگین» پیش از generation/import اجباری است.

### مرز مسئولیت و مسیر داده

- **Domain/Signing:** policy، lifecycle، نسخه کلید و پروفایل رسمی؛ بدون bytes خصوصی، HTTP یا SDK DTO.
- **Application/Signing:** authorization، use case و idempotency؛ `SigningScope(CustomerUserId, TaxpayerId, FiscalMemoryId)` از actor و DB معتبر ساخته می‌شود. شناسه‌های ارسالی کاربر به تنهایی scope نمی‌سازند.
- **Infrastructure/Signing:** primitiveهای .NET، parser محدود، CSR، certificate validation، پروفایل امضای نسخه‌دار و SDK adapter؛ هیچ singleton mutable بین مؤدیان.
- **Infrastructure/Security:** vault adapter و envelope protection؛ تنها مرز دارای دسترسی موقت به secret. Application فقط KeyVersionId را به signer مجاز می‌دهد، نه PEM یا vault handle دلخواه.
- **Persistence:** metadata، نسخه و تاریخچه، approval، audit و وضعیت عملیات؛ تنها opaque vault reference، هرگز private bytes. هماهنگی DB/vault با operation ID، staging و reconciliation برای جلوگیری از orphan؛ تراکنش توزیع‌شده فرض نمی‌شود.
- **Api + Next BFF:** DTO عمومی محدود، session سمت سرور، allowlist مسیر، Origin/CSRF، upload محدود و no-store. هیچ endpoint دریافت private key به شکل JSON یا arbitrary signing oracle وجود ندارد.
- **UI:** modal فشرده فارسی RTL با انتخاب کاربر نرم‌افزار و مؤدی قابل جست‌وجو، loader/skeleton، تاریخ شمسی نمایشی و UTC در قرارداد؛ fingerprint، اعتبار گواهی و تفکیک وضعیت محلی از اتصال رسمی.

### قراردادهای پیشنهادی؛ تثبیت دقیق در گام ۳

`IKeyCustodyService.GenerateAsync(scope, profileId, operationId, ct)` و `ImportAsync(scope, SensitiveKeyInput, operationId, ct)` فقط KeyVersionId و public metadata برمی‌گردانند. SensitiveKeyInput نباید record قابل serialization یا ToString افشاگر باشد.
`ICsrService.CreateAsync(scope, keyVersionId, caProfileId, validatedSubject, ct)` خروجی CSR عمومی دارد.
`ICertificateValidator.ValidateAsync(scope, keyVersionId, certificateChain, trustPolicyVersion, ct)` نتیجه تفصیلی بدون secret دارد.
`IDigitalSigningService.SignAsync(AuthorizedSigningRequest, ct)` خروجی امضا و key/profile version دارد؛ purpose، input size و scope محدودند؛ پروفایل الگوریتم ورودی آزاد کاربر نیست.
`IKeyProtectionProvider` پشت custody، `ISigningAuditSink` و `IKeyOperationRepository` پشت application قرار می‌گیرند. قرارداد قبلی `IPrivateKeyCustody` از طریق adapter سازگار می‌شود، نه حذف ناگهانی.

مسیرهای پیشنهادی زیر خانواده `/api/dev/taxpayers/{taxpayerId}/signing/` و BFF متناظر: generate، import، public-artifacts، csr، certificates، validation، versions، rotation-requests، reset-requests و history. Development exposure فعلی حفظ می‌شود؛ بازکردن production API نیازمند تصمیم انتشار است. خروجی امضای wire-facing صرفاً برای مصرف سرویس داخلی است؛ endpoint امضای متن دلخواه برای مرورگر ساخته نمی‌شود.

### مدل metadata پیشنهادی

`SigningKeyVersion`: مشتری/مؤدی/حافظه، الگوریتم و source profile، public-key fingerprint، opaque reference، version، status، revision و UTC.
`CertificateVersion`: key version، certificate fingerprint مستقل، chain عمومی تحت کنترل دسترسی، subject/issuer/serial، validity، validation policy/evidence و زمان بررسی.
`KeyOperation`: operation/idempotency ID، request digest غیرمحرمانه، state، vault staging reference و نتیجه قابل replay بدون secret.
`KeyChangeRequest`: نوع تغییر، reason، requester/approver، revision، expiry، مصرف یک‌باره و تاریخچه.
`SigningAuditEvent`: actor، scope، action، outcome، revision، correlation و fingerprint؛ هیچ payload خصوصی یا signed authentication token.
موجودیت‌های موجود ابتدا reuse می‌شوند؛ تغییر معنا یا backfill fingerprint قدیمی بدون بررسی ممنوع. تاریخچه حذف نمی‌شود؛ unique کلید انتخاب‌شده برای امضای جدید باید در DB و تراکنش enforce شود. جایگزینی گواهی با جایگزینی کلید یا حافظه یکسان نیست.

## 4. سیاست امنیت و عملیات

- **Encryption at rest:** پیشنهاد برنامه، نه الزام سازمان: envelope encryption با DEK تصادفی ۲۵۶بیتی برای هر نسخه و AES-256-GCM، nonce ۹۶بیتی یکتا و tag ۱۲۸بیتی؛ AAD شامل customer/taxpayer/memory/key-version و format version. DEK توسط KEK نسخه‌دار در KMS/vault wrap می‌شود. KEK خارج Git/DB/backup داده و جدا از MFA است. provider باید حفاظت کلید در حالت ذخیره، least privilege و audit داشته باشد؛ نبود provider/کلید معتبر عملیات را متوقف می‌کند.
- **پردازش secret:** تولید در boundary امن؛ import فقط upload احرازشده TLS بدون body logging یا temp plaintext. raw key/رمز فقط ورودی گذرا است؛ هرگز echo نمی‌شود. bufferهای قابل کنترل پاک و handleها dispose می‌شوند؛ پاک‌شدن قطعی همه نسخه‌های managed memory ادعا نمی‌شود. dump/telemetry و crash reporting باید redacted/محدود باشند.
- **Export:** پیش‌فرض خاموش. در صورت تصویب خروجی در گام ۲، فقط مالک با reauthentication قوی، رضایت/هشدار و audit می‌تواند artifact رمز‌شده PKCS#8 یا بسته مصوب را یک‌بار دریافت کند؛ گذرواژه گذرا و هرگز ذخیره/بازگردانده نمی‌شود. دانلود binary رمز‌شده no-store است؛ private plaintext در پاسخ API ممنوع. مدیران حق export کلید مشتری ندارند. endpoint در صورت عدم تصویب export ساخته نمی‌شود؛ گام ۱۰ همچنان تست منع خروجی دارد.
- **Backup/Recovery:** ciphertext/metadata و vault key backup جدا، restore آزمایشی با کلید synthetic در محیط ایزوله؛ manifest نسخه‌ها و fingerprint؛ بدون KEK recovery معتبر، بازگشایی ممکن نیست و راهکار کلید جدید/ابطال از مرجع لازم است. RPO/RTO عددی باید در گام ۱۸ بر اساس deployment واقعی پیشنهاد و تصویب شوند، قبل از ادعای آمادگی عملیاتی.
- **Rotation:** KEK rewrap با تعویض کلید امضا تفاوت دارد. rotation KEK resumable و قابل rollback نسخه‌ای است؛ کلید امضای جدید تا اثبات ثبت/اتصال در فاز ۱۰ مجاز به جایگزینی حافظه عملیاتی نیست. از حذف KEK قدیم تا restore تمام backupهای مشمول خودداری می‌شود. compromised key فوراً local signing را مسدود می‌کند؛ ابطال رسمی خودکار ادعا نمی‌شود.
- **Certificate:** parse/size/chain depth محدود، chain سفارشی با trust anchors نسخه‌دار و تأییدشده مستقل از OS store، identity و KeyUsage/EKU بر اساس پروفایل رسمی، validity UTC و match ریاضی؛ root همراه upload خودبه‌خود trusted نمی‌شود. revocation: Valid/Revoked/Unknown/Unavailable مجزا؛ حالت نامعلوم وقتی policy نیاز دارد اجازه امضا نمی‌دهد. AIA/CRL/OCSP URL دلخواه SSRF ایجاد نمی‌کند: allowlist، منع IP داخلی، timeout/size و resolver امن؛ آزمون با fixture، بدون تماس مالیاتی.
- **Authorization:** Customer برای داده خود؛ Level2 فقط مشتریان فعلی خود؛ Level1 طبق scope مدیریتی موجود metadata و approval. metadata access مساوی حق sign/import/export نیست. هیچ مدیر به طور پیش‌فرض read/export secret ندارد. درخواست/تأیید تغییر از هم جدا، MFA/reauth و حق مشخص؛ مالکیت مجدداً هنگام commit و sign بررسی می‌شود.
- **Audit:** موفق/ناموفق/رد مجوز بدون body، immutable و durable؛ تغییر حساس بدون ثبت audit commit نمی‌شود. audit unavailable موجب توقف عملیات حساس است. history مشتری/Level2 redacted و scoped؛ log امنیتی سراسری Level1.
- **Concurrency:** ExpectedRevision، unique index، idempotency scoped، قفل/تراکنش و retry محدود؛ هیچ قفل صرفاً in-process برای تضمین چند instance کافی نیست. عملیات همزمان rotate/sign، transfer/sign و replay approval پوشش دارد.

### تغییر یا بازتنظیم حافظه

در این فاز: درخواست علت‌دار، بررسی scoped، تأیید مجاز و تاریخچه، مسدودسازی کلید compromised و آماده‌سازی replacement محلی. Level2 می‌تواند تغییر کلید در محدوده مشتری خود را طبق مجوز مصوب بررسی کند؛ این مجوز اجازه تغییر حافظه/مؤدی قفل‌شده سالانه نمی‌دهد. طبق قرارداد فعلی، آن استثنا فقط Level1 و در فاز ۱۲ enforce می‌شود. در فاز ۹ هیچ تأییدی وضعیت Connected/Active واقعی نمی‌سازد، annual binding را reset نمی‌کند یا سند تاریخی را بازنویسی نمی‌کند. نبود اتصال و entitlement معتبر به معنی PendingExternalVerification/BlockedByFuturePhase است، نه موفقیت صوری.

## 5. جدول ثابت پیشنهادی ۲۴ گام

همه ردیف‌ها **شروع‌نشده و در انتظار تصویب برنامه** هستند. هر گام وابسته به پذیرش گام قبلی است؛ خروجی مسدود، تأییدشده تلقی نمی‌شود.

| گام | خروجی قابل بررسی | معیار پذیرش اختصاصی |
|---|---|---|
| 01 | تثبیت baseline، ممیزی منابع دو SDK، compatibility و ماتریس source→profile→test | SHA منابع و نسخه‌ها تطبیق؛ تفاوت 2.0.31/2.0.32 و قدیمی ثبت؛ الگوریتم بدون مدرک فعال نیست |
| 02 | threat model، انتخاب custody/provider، رضایت، export و trust/CA profile | مرز اعتماد و حملات ثبت؛ سیاست provider قابل اجرا و تصمیم مالک برای export؛ ناسازگاری ADR حل و مستند |
| 03 | قراردادها، state machine و ماتریس مجوز signing | انتقال مجاز/ممنوع، scope و DTO بدون secret؛ admin-read از sign/export جدا |
| 04 | مدل persistence و طرح migration افزایشی + SQL/preflight | سازگاری metadata فعلی، restrictive FK و unique/CAS؛ فقط تولید و بررسی SQL، بدون اجرای DB |
| 05 | repository و تراکنش، operation/idempotency و audit پایه | پس از تأیید مستقل migration فقط test DB؛ crash/retry نتیجه یک‌بار و audit اتمیک؛ DB/vault orphan قابل reconciliation |
| 06 | provider حفاظت و envelope/vault adapter | tamper/AAD swap/missing KEK رد؛ nonce یکتا؛ DB فاقد private bytes؛ fail closed |
| 07 | تولید امن کلید و public artifact | پروفایل رسمی مجاز؛ public مشتق‌شده match و fingerprint پایدار؛ API فاقد private؛ رضایت custody اجباری |
| 08 | import کنترل‌شده private.txt/PEM/قالب‌های مصوب | malformed/weak/oversize/multiple objects رد؛ encrypted import فقط با password workflow امن؛ هیچ temp/log plaintext |
| 09 | تطابق private/public/certificate و fingerprint | modulus/exponent یا SPKI canonical و challenge verification؛ mismatch قطعی؛ ثبت در کارپوشه از match محلی استنتاج نشود |
| 10 | کنترل export و بازیابی artifact عمومی | export پیش‌فرض منع؛ در صورت تصویب فقط encrypted owner-only با reauth و audit، بدون plaintext/JSON؛ retry/replay کنترل شود |
| 11 | CSR حقیقی/حقوقی در نرم‌افزار | PKCS#10 معتبر، signature قابل verify و subject/OID مطابق CA profile رسمی؛ نبود profile مانع فعال‌شدن |
| 12 | ورود گواهی و chain با metadata نسخه‌دار | DER/PEM مجاز، cert-key mismatch/identity mismatch رد؛ root آپلودشده trusted نیست؛ تاریخچه حفظ |
| 13 | اعتبارسنجی trust/expiry/revocation | chain معتبر/منقضی/آینده/نامعتبر و unknown مجزا؛ trust policy یکسان macOS/Ubuntu؛ SSRF مسدود |
| 14 | primitives و سرویس امضای مستقل .NET | بردار مثبت/منفی، input تغییرکرده و wrong key؛ الگوریتم/padding دقیق profile؛ خطا بدون secret |
| 15 | پروفایل SDK جدید و سازگاری legacy به صورت آفلاین | canonicalization/encoding/header و verify با شواهد SDK؛ عدم cross-wire؛ HTTP مالیاتی و nonce واقعی صفر |
| 16 | سرویس کاربردی signing و اتصال read-only به snapshot صورتحساب | امضا به نسخه snapshot/hash و key version متصل؛ ویرایش draft امضای قبلی را نامعتبر کند؛ هیچ تغییر Sent/dispatch یا صورتحساب تاریخی |
| 17 | lifecycle کلید/گواهی و rotation محلی | یک نسخه منتخب، منع sign با revoked/compromised؛ race rotate/sign مشخص؛ replacement نیازمند تأیید خارجی باقی بماند |
| 18 | KEK rotation، backup/recovery و آزمون خرابی | rewrap/resume، restore با ciphertext+KEK صحیح، failure بدون KEK، orphan recovery؛ runbook و RPO/RTO قابل بررسی |
| 19 | درخواست تغییر/بازتنظیم و approval history | reason، scope، revision، expiry و مصرف یک‌باره؛ Level2 نمی‌تواند annual reset تأیید کند؛ حافظه واقعی فعال نمی‌شود |
| 20 | API/BFF امن و قراردادهای HTTP | 401/403 یا 404 طبق سیاست، CSRF، limit، no-store، redaction و safe errors؛ هیچ private-read یا generic sign endpoint |
| 21 | UI یکپارچه تولید/import/CSR/گواهی/status/history | فارسی RTL و فشرده، search، loader، تاریخ شمسی؛ public-only download؛ حالت محلی/اتصال رسمی واضح؛ secret در state ماندگار مرورگر نیست |
| 22 | مجموعه Security/Authorization/Concurrency/Regression | HTTP و PG واقعی ایزوله، transfer/MFA/session revoke، race و leak tests؛ regression فازهای ۶–۸؛ static checks جای runtime نیست |
| 23 | پذیرش macOS و Ubuntu روی یک SHA | build/test/browser/DB guard و restore شواهددار در هر دو؛ clean tree و SHA برابر، بدون تغییر سورس محیطی |
| 24 | بستن فاز و تحویل فاز ۱۰؛ انتشار با تأیید | تمام پذیرش‌ها و بدهی‌ها ثبت؛ فقط با تأیید جدا Merge، tag پیشنهادی phase-09-v1.0.0 و GitHub Release با SHA واقعی |

## 6. نقشه فایل‌ها و آزمون‌های هر گام

مسیرهای جدید زیر **پیشنهادی** هستند؛ فایل موجود قبل از تغییر خوانده می‌شود. گام ۳ نام قراردادها را تثبیت می‌کند؛ تغییر طراحی مهم نیازمند مالک است. تمام گزارش گام‌ها در همین فایل درج می‌شود؛ source catalog فقط در صورت اصلاح حقیقت مرجع به‌روزرسانی می‌شود.

| گام | فایل‌های اصلی درگیر | آزمون اختصاصی پیشنهادی |
|---|---|---|
| 01 | همین سند؛ scripts/phase09-verify.sh (در اجرای گام) | preflight branch/SHA، test DB guard؛ شواهد checksum در همین سند |
| 02 | همین سند؛ قرارداد deployment/provider | threat/decision review، misuse cases |
| 03 | src/ArginTaxPayer.Domain/Signing/؛ Application/Signing/Contracts/ | Phase09SigningPolicyTests، Phase09SigningAuthorizationTests |
| 04 | Infrastructure/Persistence/Signing/؛ DbContext؛ Migrations/ | Phase09SigningPersistenceModelTests؛ بررسی SQL و rollback/recovery |
| 05 | Infrastructure/Persistence/Signing/KeyOperationRepository.cs | Phase09SigningOperationIntegrationTests، Phase09SigningAuditTests |
| 06 | Infrastructure/Security/KeyProtection/ | Phase09KeyProtectionTests و provider contract tests |
| 07 | Infrastructure/Signing/KeyGenerationService.cs | Phase09KeyGenerationTests |
| 08 | Infrastructure/Signing/PrivateKeyImporter.cs | Phase09PrivateKeyImportTests |
| 09 | Infrastructure/Signing/KeyPairMatcher.cs | Phase09KeyPairMatchingTests |
| 10 | Application/Signing/KeyArtifactService.cs؛ protected export adapter اگر تصویب شد | Phase09KeyExportPolicyTests |
| 11 | Infrastructure/Signing/CsrService.cs؛ پروفایل CA مصوب | Phase09CsrTests |
| 12 | Infrastructure/Signing/CertificateImporter.cs | Phase09CertificateImportTests |
| 13 | Infrastructure/Signing/CertificateValidator.cs | Phase09CertificateValidationTests، Phase09RevocationSafetyTests |
| 14 | Infrastructure/Signing/DigitalSigningService.cs | Phase09SignatureVectorTests |
| 15 | Infrastructure/Signing/Profiles/؛ SDK adapters جدا | Phase09NewSdkContractTests، Phase09LegacyCompatibilityTests |
| 16 | Application/Signing/InvoiceSigningService.cs؛ adapter snapshot موجود | Phase09InvoiceSigningSnapshotTests |
| 17 | Application/Signing/KeyLifecycleService.cs | Phase09KeyLifecycleTests، Phase09RotationConcurrencyTests |
| 18 | Infrastructure/Security/KeyProtection/؛ scripts/phase09-recovery-verify.* | Phase09KeyRecoveryTests؛ restore عملیاتی synthetic |
| 19 | Application/Signing/KeyChangeReviewService.cs؛ persistence history | Phase09KeyChangeApprovalTests |
| 20 | Api/Signing/؛ apps/web/app/api/signing/ | Phase09SigningHttpSecurityTests؛ apps/web/tests/phase09-signing-proxy.test.mjs |
| 21 | apps/web/app/(console)/signing/؛ apps/web/app/signing/ | apps/web/tests/phase09-signing-ui.test.mjs؛ browser checklist واقعی |
| 22 | tests/ArginTaxPayer.Tests/Phase09*؛ apps/web/tests/phase09-* | جامع HTTP/PG و race و regression؛ logs redaction capture |
| 23 | scripts/phase09-verify.sh؛ همین سند | خروجی مستقل دو OS و fingerprint/chain/signature parity |
| 24 | همین سند؛ Git/GitHub فقط پس از تأیید | diff scope، clean tree، tag target و Release URL |

## 7. قرارداد انجام هر گام و تست macOS

هر گام: ۱) آزمون رفتاری متناسب، ۲) اجرای آن و مشاهده failure مورد انتظار، ۳) پیاده‌سازی حداقلی، ۴) اجرای آزمون و regression مرتبط، ۵) review diff برای secret و scope، ۶) commit قابل بازبینی، ۷) ثبت شواهد و دستورات macOS در همین سند، ۸) توقف برای تأیید مالک. گام‌های صرفاً سندی به build اجباری بی‌فایده نیاز ندارند؛ acceptance آنها review و checksum است. تولید migration با اجرای آن یک مجوز نیست.

در گام ۱ اسکریپت واحد `scripts/phase09-verify.sh` ساخته می‌شود؛ **در حال حاضر وجود ندارد**. قرارداد آن: pure برای unit/source بدون DB؛ focused برای تست‌های گام با اطمینان از کشف حداقل یک تست؛ integration فقط پس از parse connection و تأیید `SELECT current_database() = 'argintaxpayer_test'`؛ full برای کل regression و web. unset بودن PG integration باید از خروجی معلوم باشد؛ skip به عنوان PASS گزارش نمی‌شود. هیچ mode migration اعمال نمی‌کند. clean و SHA پیش/پس ثبت می‌شوند؛ تست نباید secret config را چاپ کند.

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

### پذیرش دو پلتفرم در گام ۲۳

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

در macOS همان SHA کامل commit ثبت و full اجرا می‌شود. اگر working tree اوبونتو تغییر داشت، آن تغییرات بدون تصمیم مالک stash/reset/commit یا merge نمی‌شوند؛ بدهی تطبیق فاز ۸ خارج محدوده باقی می‌ماند و در صورت مانع بودن، پذیرش با checkout تمیز ایزوله همان SHA و config خارجی انجام می‌شود. paths، build caches، OS trust store و key provider config نباید سورس را تغییر دهند. generated `.next`/`.next-dev`/next-env.d.ts طبق ignore فعلی بررسی شوند. یکسانی chain policy به trust bundle نسخه‌دار وابسته است، نه اعتماد پیش‌فرض OS. امضای deterministic فقط در profile قطعی با الگوریتم deterministic مقایسه bytes می‌شود؛ در سایر موارد verify و محتوای معنایی معیار است.

## 8. ماتریس خطر و Review Focus

| خطر/ورودی | کنترل و شاهد لازم | گام مالک |
|---|---|---|
| کلید یکسان یا شناسه مالیاتی تکراری در دو حساب | scope مستقل و عدم dedupe سراسری افشاگر؛ AAD swap رد؛ سیاست reuse کلید جدا بررسی شود | 03/06/09/22 |
| سرقت DB/backup یا دسترسی مدیر نرم‌افزار | private خارج DB؛ KEK مستقل؛ API بدون secret read؛ residual risk اپراتور vault/host صریح | 02/06/18/22 |
| لغو session یا انتقال مشتری هنگام sign | بازخوانی authorization، revision/transaction و race test؛ Level2 قدیمی دسترسی ندارد | 16/22 |
| upload مخرب و password اشتباه | bounds، parser محدود، عدم echo، عدم temp plaintext و rate limit | 08/12/20 |
| cert معتبر از نظر chain ولی برای مؤدی دیگر | تطابق کلید کافی نیست؛ identity رسمی جدا validate شود | 09/13 |
| اعتبارسنجی با root دلخواه و قطع revocation | custom trust + policy version؛ Unknown هرگز Valid نیست؛ fail closed | 13 |
| signed bytes متفاوت به دلیل serialization | بردار رسمی، UTF-8/Base64URL و snapshot دقیق؛ duplicate JSON keys رد | 14/15/16 |
| CRL/AIA به شبکه داخلی | allowlist/timeout/size و SSRF negative tests | 13/22 |
| vault نوشته شده ولی DB commit نشده | staging + operation ID + cleanup/reconcile و crash test | 05/18 |
| دو rotation/approval همزمان | CAS/unique/transaction، one-time approval و replay negative | 17/19/22 |
| ثبت secret در exception/telemetry/BFF | capture خروجی واقعی HTTP/log/trace؛ canary synthetic؛ ممنوعیت logging body | 20/22 |
| اسکریپت integration روی DB اشتباه | guard نام واقعی DB قبل از نوشتن؛ نام مشابه و connection override منفی | 01/05/23 |

## 9. وابستگی‌ها و مرزهای قطعی

- provider عملی vault/KMS و trust anchors رسمی برای ادعای آمادگی تولید ضروری است؛ in-memory fake فقط تست است. vendor/زیرساخت جدید بدون تصمیم اجرایی معرفی نمی‌شود.
- از primitiveهای .NET استفاده شود؛ اضافه کردن package رمزنگاری/JOSE فقط پس از ضرورت، version pin، license/security/dependency review و بردار تطبیق. sample file-based SDK نباید اجبار به temp private PEM بسازد؛ adapter به signer protected متصل شود.
- encrypted import/export نیازمند پروفایل PBE مصوب و آزمون interoperability است؛ عدد iteration یا الگوریتم از روی حدس در محصول ثابت نمی‌شود.
- فاز ۱۰: nonce واقعی، JWT درخواست عملیاتی، auth/connection check، دریافت public key سازمان و dispatch. گام ۱۵ فقط ساخت/verify آفلاین با nonce synthetic و شبکه مسدود است. JWE/رمزگذاری payload مالیاتی تنها در حد پروفایل محلی مستند و fixture این فاز، نه دریافت زنده کلید سرور؛ implementation wire dispatch در فاز ۱۰.
- فاز ۱۲: entitlement، قفل annual و مصرف سهمیه. این فاز قراردادهای منع عملیات زودهنگام را حفظ می‌کند و سیستم billing جدید نمی‌سازد.
- CA: تولید CSR داخل برنامه؛ صدور گواهی معتبر و احراز هویت بیرون برنامه تا وجود API رسمی و مجوز؛ هیچ scraping/CAPTCHA automation یا صدور fake انجام نمی‌شود.
- **خارج محدوده:** ارسال واقعی، استعلام واقعی، production deployment، تیکتینگ، اصلاح import انبوه فاز ۸، بازنویسی زیرساخت dev دو OS و ساخت agent محلی/HSM driver عمومی.
- هیچ migration/تغییر خطرناک DB بدون تأیید مستقل مالک اجرا نمی‌شود؛ تست نوشتنی فقط `argintaxpayer_test`، با داده synthetic و FK/history محفوظ.

## 10. بدهی‌های فاز ۸؛ انتقال بدون ورود به محدوده

| بدهی | وضعیت در فاز ۹ |
|---|---|
| QA عملیاتی import انبوه، partial failure و durable outcome | معوق؛ نیازمند تصمیم جداگانه مالک |
| تطبیق تغییرات محلی اسکریپت Ubuntu و same-SHA فاز ۸ | معوق؛ گام ۲۳ فقط parity تحویل فاز ۹ را بررسی می‌کند |
| checklist مرورگر احرازشده فاز ۸ بدون شواهد جزءبه‌جزء | به عنوان محدودیت شواهد حفظ می‌شود؛ بازتأیید تاریخی ادعا نمی‌شود |
| متن pending انتشار در docs فاز ۸ با وجود Release واقعی | اختلاف مستند ثبت‌شده؛ اصلاح تاریخچه تنها با تصمیم مالک |
| تیکتینگ | تا پایان کل پروژه معوق؛ وارد این فاز نمی‌شود |

## 11. دفتر اجرا و تأیید

**تصویب برنامه:** در انتظار مالک. **گام ۱:** آغاز نشده. **گام‌های ۲–۲۴:** آغاز نشده. **DB migration:** مجوز اجرا ندارد. **Merge/Tag/Release:** مجوز ندارد.

برای هر گام همین‌جا یک رکورد با فیلدهای زیر افزوده می‌شود:
شماره/عنوان ثابت؛ هدف؛ منابع دقیق و تعارض‌ها؛ فایل‌ها و commit SHA؛ تست‌های اجراشده با count و skipped؛ تست‌های اجرا‌نشده و علت؛ دستورات واقعی macOS؛ وضعیت migration/SQL/DB guard؛ شواهد redacted؛ معیارهای پذیرش تکمیل/مسدود؛ ریسک و بدهی؛ تصمیم صریح مالک با تاریخ. وضعیت «آماده بررسی» با «تأییدشده» فرق دارد.

**شرط پایان فاز:** ۲۴ گام با signoff واقعی، همه تست‌های الزامی و same-SHA تأیید، restore آزموده، منابع/CA/profile مشخص، هیچ ادعای اتصال واقعی، و بدهی‌های باقی‌مانده شفاف. تأیید عملکرد به‌تنهایی مجوز انتشار نیست. در گام ۲۴ نتیجه Merge/Tag/Release تنها پس از اجرای تأییدشده و مشاهده artifact واقعی ثبت می‌شود.
