import Link from "next/link";
import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import UsersWorkspace from "./users-workspace";

export const dynamic = "force-dynamic";
export default async function UsersPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const isLevel1 = actor.roles.includes("Level1");
  const isLevel2 = actor.roles.includes("Level2");
  if (!isLevel1 && !isLevel2) redirect("/account");
  return <main dir="rtl" lang="fa" className="shell taxpayer-layout">
    <header className="header">
      <div><p className="eyebrow">ArginTaxPayer · Customer Accounts</p>
        <h1>مدیریت کاربران نرم‌افزار</h1>
        <p className="subtitle">کاربران، مالکیت تجاری و مؤدیان وابسته به هر حساب</p>
      </div>
      <nav className="taxpayer-nav" aria-label="ناوبری">
        <Link href="/account">حساب و امنیت</Link>
        <Link href="/">صفحه اصلی</Link>
      </nav>
    </header>
    <UsersWorkspace level1={isLevel1}/>
  </main>;
}
