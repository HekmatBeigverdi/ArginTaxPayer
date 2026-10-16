import { redirect } from "next/navigation";
import Image from "next/image";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import IdentityFlow from "./identity-flow";

export const dynamic = "force-dynamic";

export default async function IdentityPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (actor) redirect("/dashboard");
  return <main className="identity-layout" dir="rtl" lang="fa">
    <div className="identity-brand"><Image className="argin-brand-logo argin-login-logo" src="/brand/argin-logo.png" width={54} height={54} alt="نشان آرگین" priority /><div><strong>آرگین</strong><span>سامانه مدیریت امور مالیاتی</span></div></div>
    <section className="identity-card auth-simplified-card" aria-label="ورود و ثبت‌نام">
      <p className="identity-kicker">حساب کاربری</p>
      <h1>به آرگین خوش آمدید</h1>
      <p className="identity-muted">برای ورود به فضای کاری، اطلاعات حساب خود را وارد کنید.</p>
      <IdentityFlow />
    </section>
    <p className="identity-note">این بخش صرفاً در محیط توسعه فعال است. اطلاعات ورود خود را با دیگران به اشتراک نگذارید.</p>
  </main>;
}
