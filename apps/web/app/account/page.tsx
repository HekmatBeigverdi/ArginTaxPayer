import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import AccountControls from "./account-controls";

export const dynamic = "force-dynamic";

export default async function AccountPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  return <main className="shell account-layout" dir="rtl" lang="fa">
    <header className="header"><div><p className="eyebrow">ArginTaxPayer · Identity</p>
      <h1>حساب و امنیت</h1>
      <p className="subtitle">مدیریت هویت و نشست‌های فعال</p></div>
      <a href="/" className="identity-link">صفحه اصلی</a></header>
    <section className="intro">
      <h2>حساب شما</h2>
      <p>شناسه کاربر: <bdi dir="ltr">{actor.userId}</bdi></p>
      <p>نقش‌های فعلی: {actor.roles.join("، ")}</p>
    </section>
    <AccountControls/>
  </main>;
}
