import DashboardIcon, { type DashboardIconName } from "../../components/dashboard-icon";
import SubscriptionStatus from "../../components/subscription-status";
import DashboardAnalytics from "./dashboard-analytics";
import "./dashboard-design.css";
import Link from "next/link";
import { currentIdentity } from "../../../lib/identity";

export const dynamic = "force-dynamic";

type Tile = { title: string; description: string; href?: string; meta?: string; icon: DashboardIconName };
const customerModules: Tile[] = [
  { title: "خریداران و مؤدیان", description: "حساب‌ها و اشخاص حقیقی یا حقوقی وابسته به شما", href: "/parties", icon: "parties", meta: "فعال" },
  { title: "کالاها و خدمات", description: "تعریف و مدیریت اقلام صورتحساب", icon: "catalog", meta: "در فاز بعد" },
  { title: "پیش‌نویس صورتحساب", description: "ساخت و مشاهده پیش‌نویس داخلی، بدون ارسال مالیاتی", href: "/invoices", icon: "invoice", meta: "پیش‌نویس" },
  { title: "تنظیمات", description: "تنظیمات مالیاتی و ترجیحات حساب", icon: "settings", meta: "در انتظار تعریف" },
  { title: "پروفایل و اشتراک", description: "اطلاعات خریدار پنل؛ انتخاب و خرید پلن پس از فعال‌شدن فروش", href: "/account", icon: "subscription", meta: "پروفایل فعال" },
  { title: "تراکنش‌های مالی", description: "سوابق خرید، پرداخت و تمدید اشتراک", href: "/financial-history", icon: "history", meta: "فعال در توسعه" }
];
const adminModules: Tile[] = [
  { title: "کاربران نرم‌افزار", description: "ایجاد و مدیریت حساب‌های ورود و نقش‌های سامانه", href: "/users", icon: "parties", meta: "مدیریت" },
  { title: "خریداران و مؤدیان", description: "ثبت مشتری، اشخاص حقیقی و حقوقی و مؤدیان متعلق به هر حساب", href: "/parties", icon: "parties", meta: "مدیریت" },
  { title: "مالکیت تجاری مشتریان", description: "انتساب مشتری به ادمین و پیگیری محدوده دسترسی", href: "/users", icon: "ownership", meta: "مدیریت" },
  { title: "پیش‌نویس صورتحساب‌ها", description: "مشاهده و ایجاد پیش‌نویس برای مؤدیان مجاز", href: "/invoices", icon: "invoice", meta: "پیش‌نویس" },
  { title: "اشتراک‌ها و تراکنش‌ها", description: "مدیریت پلن‌ها، پرداخت‌های دستی و مانده تسویه", href: "/subscriptions", icon: "subscription", meta: "توسعه" },
  { title: "تنظیمات و امنیت", description: "نشست‌ها، امنیت و تأیید دومرحله‌ای", href: "/account", icon: "settings", meta: "فعال" }
];

export default async function DashboardPage() {
  const actor = await currentIdentity();
  if (!actor) return null;
  const level1 = actor.roles.includes("Level1");
  const level2 = actor.roles.includes("Level2");
  const admin = level1 || level2;
  const customer = actor.roles.includes("Customer");
  if (!admin && !customer) return null;
  const modules = admin ? adminModules : customerModules;
  return <>
        <DashboardAnalytics role={level1 ? "Level1" : level2 ? "Level2" : "Customer"}/>
        {customer && <SubscriptionStatus/>}
        <details className="argin-dashboard__legacy">
          <summary>
            <span className="argin-dashboard__legacy-heading"><DashboardIcon name="modules" />دسترسی به سایر بخش‌های سامانه</span>
            <span>نمایش میان‌برها</span>
          </summary>
        <section className="app-module-grid" aria-label="ماژول‌های سامانه">
          {modules.map(item => <article key={item.title} className="app-module">
            <div className="app-module-heading"><span className="app-module-icon"><DashboardIcon name={item.icon} /></span>
              <small>{item.meta}</small></div>
            <h3>{item.title}</h3><p>{item.description}</p>
            {item.href ? <Link href={item.href} className="app-module-link">ورود به بخش <span aria-hidden="true">←</span></Link>
              : <span className="app-module-disabled" aria-label="هنوز فعال نیست">به‌زودی</span>}
          </article>)}
        </section>
        <div className="app-help">برای حفظ امنیت، امکانات مدیریت مشتریان فقط در محدوده مجوزهای نقش فعلی در دسترس هستند.</div>
        </details>
  </>;
}
