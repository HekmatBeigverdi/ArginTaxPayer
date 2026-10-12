import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import IdentityForms from "./identity-forms";
import PhoneForms from "./phone-forms";

export const dynamic = "force-dynamic";

export default async function IdentityPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (actor) redirect("/account");
  return <main className="identity-layout" dir="rtl" lang="fa">
    <div className="identity-brand"><span className="brand-symbol">آ</span><div><strong>آرگین</strong><span>سامانه مدیریت امور مالیاتی</span></div></div>
    <section className="identity-card" aria-label="ورود و ثبت‌نام">
      <p className="identity-kicker">حساب کاربری</p>
      <h1>به آرگین خوش آمدید</h1>
      <p className="identity-muted">ورود ایمن، ثبت‌نام آزمایشی و احراز هویت دومرحله‌ای</p>
      <IdentityForms />
    </section>
    <p className="identity-note">این بخش صرفاً در محیط توسعه فعال است. اطلاعات ورود خود را با دیگران به اشتراک نگذارید.</p>
  </main>;
}
