import { getApiHealth, type ProbeState } from "../lib/api-health";
import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../lib/identity";

export const dynamic = "force-dynamic";

const statusLabels: Record<ProbeState, string> = {
  healthy: "سالم",
  unavailable: "در دسترس نیست",
  "not-configured": "تنظیم نشده",
};

const sections = [
  { title: "صورتحساب‌ها", description: "گردش کار صدور و پیگیری صورتحساب، در فازهای آینده" },
  { title: "کاربران نرم‌افزار", description: "مدیریت حساب کاربران، انتساب به شریک و نمایش مؤدیان هر کاربر" },
  { title: "اشتراک و تسویه", description: "مدیریت پلن‌ها، وصول وجه و سهم مشارکت، در فازهای آینده" },
] as const;

export default async function HomePage() {
  if (identityUiEnabled()) {
    const actor = await currentIdentity();
    if (actor) redirect("/dashboard");
    redirect("/identity");
  }
  const api = await getApiHealth();
  return (
    <main className="shell">
      <header className="header">
        <div>
          <p className="eyebrow">ArginTaxPayer · Foundation</p>
          <h1>سامانه مؤدیان آرگین</h1>
          <p className="subtitle">پوسته اولیه رابط کاربری — فاز دوم</p>
        </div>
        <div className="header-actions"><a className="status" href="/identity">ورود / ثبت‌نام</a><span className="status">نسخه توسعه</span></div>
      </header>
      <section className="intro" aria-labelledby="foundation-title">
        <h2 id="foundation-title">آماده‌سازی محیط کار</h2>
        <p>این صفحه نمای توسعه سامانه است. مدیریت کاربران و هویت در محیط توسعه فعال است؛ ارسال مالیاتی واقعی هنوز فعال نیست.</p>
      </section>
      <section className="intro" aria-labelledby="api-health-title">
        <h2 id="api-health-title">وضعیت ارتباط آزمایشی با API</h2>
        <p>این بررسی فقط از سمت سرور Next.js و در محیط توسعه انجام می‌شود؛ هیچ اطلاعات مؤدی یا کلید محرمانه‌ای منتقل نمی‌شود.</p>
        <div className="api-health-grid">
          <p>اجرای API: <strong>{statusLabels[api.liveness]}</strong></p>
          <p>آمادگی دیتابیس: <strong>{statusLabels[api.readiness]}</strong></p>
        </div>
      </section>
      <section className="features" aria-label="ماژول‌های برنامه‌ریزی‌شده">
        {sections.map((section) => (
          <article className="feature" key={section.title}>
            <h3>{section.title}</h3>
            <p>{section.description}</p>
            {section.title === "کاربران نرم‌افزار" ? <a href="/users" className="identity-link">ورود به مدیریت کاربران ←</a> : <span aria-label="هنوز فعال نیست" className="planned">در دست طراحی</span>}
          </article>
        ))}
      </section>
      <footer className="footer">نرم‌افزار آرگین · توسعه ساختار پایه</footer>
    </main>
  );
}
