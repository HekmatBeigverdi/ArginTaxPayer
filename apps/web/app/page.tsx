const sections = [
  { title: "صورتحساب‌ها", description: "گردش کار صدور و پیگیری صورتحساب، در فازهای آینده" },
  { title: "مؤدیان", description: "مدیریت شرکت‌ها و اشخاص با دسترسی‌های مجاز، در فازهای آینده" },
  { title: "اشتراک و تسویه", description: "مدیریت پلن‌ها، وصول وجه و سهم مشارکت، در فازهای آینده" },
] as const;

export default function HomePage() {
  return (
    <main className="shell">
      <header className="header">
        <div>
          <p className="eyebrow">ArginTaxPayer · Foundation</p>
          <h1>سامانه مؤدیان آرگین</h1>
          <p className="subtitle">پوسته اولیه رابط کاربری — فاز دوم</p>
        </div>
        <span className="status">نسخه توسعه</span>
      </header>
      <section className="intro" aria-labelledby="foundation-title">
        <h2 id="foundation-title">آماده‌سازی محیط کار</h2>
        <p>این صفحه صرفاً نمای اولیه پلتفرم است. هنوز ورود کاربران، اطلاعات مؤدیان و عملیات مالیاتی واقعی فعال نشده‌اند.</p>
      </section>
      <section className="features" aria-label="ماژول‌های برنامه‌ریزی‌شده">
        {sections.map((section) => (
          <article className="feature" key={section.title}>
            <h3>{section.title}</h3>
            <p>{section.description}</p>
            <span aria-label="هنوز فعال نیست" className="planned">در دست طراحی</span>
          </article>
        ))}
      </section>
      <footer className="footer">نرم‌افزار آرگین · توسعه ساختار پایه</footer>
    </main>
  );
}
