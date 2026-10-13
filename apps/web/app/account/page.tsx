import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import AccountControls from "./account-controls";
import AdminCreateCustomer from "./admin-create-customer";
import AdminNomination from "./admin-nomination";
import CustomerOwnershipConsole from "./customer-ownership-console";

export const dynamic = "force-dynamic";
const roleLabels: Record<string, string> = {
  Customer: "کاربر", Level1: "ادمین سطح ۱", Level2: "ادمین سطح ۲",
  PendingLevel1: "ادمین سطح ۱ — در انتظار تأیید دومرحله‌ای",
  PendingLevel2: "ادمین سطح ۲ — در انتظار تأیید دومرحله‌ای",
};

export default async function AccountPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  return <main className="shell account-layout" dir="rtl" lang="fa">
    <header className="header"><div><p className="eyebrow">ArginTaxPayer · Identity</p>
      <h1>حساب و امنیت</h1>
      <p className="subtitle">مدیریت هویت و نشست‌های فعال</p></div>
      <nav className="taxpayer-nav"><a href="/" className="identity-link">صفحه اصلی</a>{actor.roles.some(role => role === "Level1" || role === "Level2") && <a href="/taxpayers" className="identity-link">مدیریت مؤدیان</a>}</nav></header>
    <section className="intro">
      <h2>حساب شما</h2>
      <p>شناسه کاربر: <bdi dir="ltr">{actor.userId}</bdi></p>
      <p>نقش‌های فعلی: {actor.roles.map(role => roleLabels[role] ?? role).join("، ")}</p>
    </section>
    <AccountControls canEnrollMfa={actor.roles.some(role =>
      ["Level1", "Level2", "PendingLevel1", "PendingLevel2"].includes(role))}/>
    {actor.roles.includes("Level1") && <AdminNomination/>}
    {actor.roles.some(role => role === "Level1" || role === "Level2") && <CustomerOwnershipConsole level1={actor.roles.includes("Level1")}/>}
    {actor.roles.some(role => role === "Level1" || role === "Level2") && <AdminCreateCustomer/>}
  </main>;
}
