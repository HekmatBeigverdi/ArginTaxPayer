import Link from "next/link";
import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";

export const dynamic = "force-dynamic";

type Tile = { title: string; description: string; href?: string; meta?: string; symbol: string };
const customerModules: Tile[] = [
  { title: "خریداران و مؤدیان", description: "حساب‌ها و اشخاص حقیقی یا حقوقی وابسته به شما", href: "/parties", symbol: "اش", meta: "فعال" },
  { title: "کالاها و خدمات", description: "تعریف و مدیریت اقلام صورتحساب", symbol: "کا", meta: "در فاز بعد" },
  { title: "صدور و ارسال صورتحساب", description: "انتخاب قالب، ارسال و فهرست صورتحساب‌های ثبت‌شده", symbol: "فا", meta: "در فاز بعد" },
  { title: "تنظیمات", description: "تنظیمات مالیاتی و ترجیحات حساب", symbol: "تن", meta: "در انتظار تعریف" },
  { title: "پروفایل و اشتراک", description: "اطلاعات خریدار پنل؛ انتخاب و خرید پلن پس از فعال‌شدن فروش", href: "/account", symbol: "پر", meta: "پروفایل فعال" },
  { title: "تراکنش‌های مالی", description: "سوابق خرید، پرداخت و تمدید", symbol: "ما", meta: "در فاز مالی" }
];
const adminModules: Tile[] = [
  { title: "کاربران نرم‌افزار", description: "ایجاد و مدیریت حساب‌های ورود و نقش‌های سامانه", href: "/users", symbol: "کا", meta: "مدیریت" },
  { title: "خریداران و مؤدیان", description: "ثبت مشتری، اشخاص حقیقی و حقوقی و مؤدیان متعلق به هر حساب", href: "/parties", symbol: "اش", meta: "مدیریت" },
  { title: "مالکیت تجاری مشتریان", description: "انتساب مشتری به ادمین و پیگیری محدوده دسترسی", href: "/users", symbol: "مل", meta: "مدیریت" },
  { title: "کالاها و صورتحساب‌ها", description: "نظارت بر عملیات مالیاتی مشتریان مجاز", symbol: "فا", meta: "در فاز بعد" },
  { title: "اشتراک‌ها و تراکنش‌ها", description: "مدیریت پلن‌ها، پرداخت‌ها و تمدیدها", symbol: "اش", meta: "در فاز مالی" },
  { title: "تنظیمات و امنیت", description: "نشست‌ها، امنیت و تأیید دومرحله‌ای", href: "/account", symbol: "ام", meta: "فعال" }
];

export default async function DashboardPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const level1 = actor.roles.includes("Level1");
  const level2 = actor.roles.includes("Level2");
  const admin = level1 || level2;
  const customer = actor.roles.includes("Customer");
  if (!admin && !customer) redirect("/account");
  const modules = admin ? adminModules : customerModules;
  const role = level1 ? "مدیر سطح یک" : level2 ? "مدیر سطح دو" : "کاربر پنل";
  const nav = admin
    ? [{ label: "داشبورد", href: "/dashboard" }, { label: "کاربران نرم‌افزار", href: "/users" },
      { label: "خریداران و مؤدیان", href: "/parties" }, { label: "حساب و امنیت", href: "/account" }]
    : [{ label: "داشبورد", href: "/dashboard" }, { label: "خریداران و مؤدیان", href: "/parties" },
      { label: "پروفایل و اشتراک", href: "/account" }];
  return <main className="app-shell" dir="rtl" lang="fa">
    <aside className="app-sidebar" aria-label="ناوبری اصلی">
      <Link href="/dashboard" className="app-brand"><span className="app-brand-mark">آ</span>
        <span><strong>آرگین</strong><small>سامانه یکپارچه مؤدیان</small></span></Link>
      <div className="app-nav-heading">فضای کاری</div>
      <nav className="app-nav">{nav.map(item => <Link key={item.href} href={item.href}
        aria-current={item.href === "/dashboard" ? "page" : undefined}>{item.label}<span aria-hidden="true">←</span></Link>)}</nav>
      <div className="app-sidebar-footer"><span>نسخه آزمایشی</span><p>عملیات مالیاتی واقعی و خرید اشتراک هنوز فعال نیست.</p></div>
    </aside>
    <div className="app-main">
      <header className="app-topbar">
        <span>پنل مدیریت / داشبورد</span>
        <div className="app-topbar-user"><span className="app-role">{role}</span>
          <Link href="/account" aria-label="مدیریت حساب و امنیت">حساب من</Link></div>
      </header>
      <div className="app-content">
        <div className="app-welcome"><div><p className="app-kicker">پیشخوان آرگین</p>
          <h1>{admin ? "مرکز مدیریت سامانه" : "داشبورد شما"}</h1>
          <p>{admin
            ? "مدیریت حساب‌های نرم‌افزار و مشتریان را از بخش‌های مستقل انجام دهید."
            : "از اینجا به اطلاعات مؤدیان و بخش‌های پنل خود دسترسی دارید."}</p></div>
          <span className="app-welcome-graphic" aria-hidden="true">آ</span>
        </div>
        <section aria-label="نمای کلی امکانات" className="app-summary">
          <div><small>نقش فعال</small><strong>{role}</strong></div>
          <div><small>حوزه دسترسی</small><strong>{level1 ? "مدیریت سطح یک" : level2 ? "مشتریان زیرمجموعه" : "حساب شخصی"}</strong></div>
          <div><small>وضعیت پنل</small><strong>محیط توسعه</strong></div>
        </section>
        <div className="app-section-title"><div><h2>بخش‌های سامانه</h2>
          <p>ماژول‌های فعال و قابلیت‌های برنامه‌ریزی‌شده</p></div></div>
        <section className="app-module-grid" aria-label="ماژول‌های سامانه">
          {modules.map(item => <article key={item.title} className="app-module">
            <div className="app-module-heading"><span className="app-module-icon">{item.symbol}</span>
              <small>{item.meta}</small></div>
            <h3>{item.title}</h3><p>{item.description}</p>
            {item.href ? <Link href={item.href} className="app-module-link">ورود به بخش <span aria-hidden="true">←</span></Link>
              : <span className="app-module-disabled" aria-label="هنوز فعال نیست">به‌زودی</span>}
          </article>)}
        </section>
        <div className="app-help">برای حفظ امنیت، امکانات مدیریت مشتریان فقط در محدوده مجوزهای نقش فعلی در دسترس هستند.</div>
      </div>
    </div>
  </main>;
}
