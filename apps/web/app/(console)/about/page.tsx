import Image from "next/image";
import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";

export const dynamic = "force-dynamic";

const capabilities = [
  { icon: "◈", title: "مدیریت مؤدیان و طرف‌های تجاری", text: "ثبت و نگهداری اطلاعات خریداران، فروشندگان و مؤدیان مرتبط در یک فضای کاری واحد." },
  { icon: "▤", title: "مدیریت کالا و خدمات", text: "تعریف کالاها و خدمات برای استفاده در فرایندهای ثبت و صدور صورتحساب." },
  { icon: "▣", title: "ثبت و پیگیری صورتحساب‌ها", text: "مدیریت صورتحساب‌ها و مشاهدهٔ سوابق و وضعیت‌های مرتبط با آن‌ها." },
  { icon: "♙", title: "مدیریت کاربران و دسترسی‌ها", text: "فضای کاری مبتنی بر نقش برای کاربران و مدیران، با کنترل دسترسی به اطلاعات." },
];

export default async function AboutPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");

  return <main className="about-page">
    <header className="about-hero">
      <div className="about-hero__copy">
        <p className="app-kicker">آشنایی با نرم‌افزار</p>
        <h1>آرگین</h1>
        <p>آرگین یک نرم‌افزار یکپارچه برای مدیریت اطلاعات مالیاتی و فرایندهای روزمره کسب‌وکار است. بخش‌های مختلف آن کمک می‌کنند اطلاعات موردنیاز در یک فضای کاری منظم ثبت، نگهداری و پیگیری شوند.</p>
      </div>
      <Image className="about-hero__logo" src="/brand/argin-logo.png" width={144} height={144} alt="نشان نرم‌افزار آرگین" priority unoptimized />
    </header>

    <section className="about-story" aria-label="معرفی آرگین">
      <div className="about-story-layout">
        <div className="about-story-copy">
          <p>در آرگین می‌توانید اطلاعات خریداران، فروشندگان و مؤدیان را مدیریت کنید و کالاها و خدمات موردنیاز را در فهرست اختصاصی خود نگه دارید. این ساختار دسترسی به اطلاعات پایه را ساده‌تر می‌کند و به مرتب‌ماندن داده‌های مورد استفاده در کارهای مالیاتی کمک می‌کند.</p>
          <p>بخش صورتحساب‌ها برای ثبت و پیگیری صورتحساب‌ها در کنار اطلاعات طرف‌های تجاری و کالاها طراحی شده است. سوابق مرتبط نیز در پنل قابل مشاهده‌اند تا کاربر بتواند اطلاعات موردنیاز را در فضای کاری خودش پیدا و مدیریت کند.</p>
          <p>آرگین از نقش‌های کاربری و دسترسی‌های متناسب با آن‌ها پشتیبانی می‌کند. کاربران و مدیران، با توجه به سطح دسترسی خود، به بخش‌های مربوط دسترسی دارند و مدیریت اطلاعات در چارچوب حساب‌ها و کاربران تعریف‌شده انجام می‌شود. آرگین محصول مشترک شرکت‌های نگین حساب ویرا و داده پردازی همگام افزار است.</p>
          <div className="about-companies" aria-label="شرکت‌های آرگین">
            <article className="about-company"><Image className="about-company__logo" src="/brand/companies/negin-hesab-vira.png" width={64} height={64} alt="لوگوی نگین حساب ویرا" /><div className="about-company__details"><h2>نگین حساب ویرا</h2><p>ارائه دهنده خدمات مالی و حسابداری</p></div></article>
            <article className="about-company"><Image className="about-company__logo" src="/brand/companies/hamgam-afzar.png" width={64} height={64} alt="لوگوی داده پردازی همگام افزار" /><div className="about-company__details"><h2>داده پردازی همگام افزار</h2><p>ارائه دهنده خدمات برنامه نویسی اختصاصی</p></div></article>
          </div>
        </div>
        <figure className="about-visual">
          <Image src="/about/argin-about-workspace.png" width={1536} height={1024} sizes="(max-width: 850px) 100vw, 38vw" alt="تصویر مفهومی از داشبورد مالی و مدیریت اطلاعات در آرگین" />
          <figcaption>فضای یکپارچهٔ مدیریت اطلاعات و امور مالی</figcaption>
        </figure>
      </div>
    </section>

    <section className="about-section" aria-labelledby="about-features-title">
      <div className="about-section__heading">
        <div><p className="app-kicker">امکانات سامانه</p><h2 id="about-features-title">همه‌چیز در یک فضای کاری</h2></div>
        <p>بخش‌های اصلی برای مدیریت اطلاعات مالیاتی و فرایندهای مرتبط</p>
      </div>
      <div className="about-feature-grid">
        {capabilities.map(item => <article className="about-feature" key={item.title}>
          <span className="about-feature__icon" aria-hidden="true">{item.icon}</span>
          <h3>{item.title}</h3><p>{item.text}</p>
        </article>)}
      </div>
    </section>

    <footer className="about-footer">تمام حقوق نرم افزار آرگین متعلق به هر دو شرکت می باشد</footer>
  </main>;
}
