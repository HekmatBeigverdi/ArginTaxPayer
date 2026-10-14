import Link from "next/link";
import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import PartyWorkspace from "./party-workspace";

export const dynamic = "force-dynamic";

export default async function PartiesPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const admin = actor.roles.includes("Level1") || actor.roles.includes("Level2");
  const customer = actor.roles.includes("Customer");
  if (!admin && !customer) redirect("/account");
  return <main className="shell party-layout" dir="rtl" lang="fa">
    <header className="header">
      <div><p className="eyebrow">ArginTaxPayer · Parties & Customers</p>
        <h1>اشخاص، مشتریان و مؤدیان</h1>
        <p className="subtitle">{admin ? "مدیریت اشخاص و اطلاعات مرتبط با مشتری" :
          "نمایش اطلاعات حساب و مؤدیان شما — فقط خواندنی"}</p>
      </div>
      <nav className="taxpayer-nav" aria-label="ناوبری">
        <Link href="/account">حساب و امنیت</Link>
        {admin && <Link href="/users">کاربران</Link>}
        <Link href="/">صفحه اصلی</Link>
      </nav>
    </header>
    <PartyWorkspace isAdmin={admin} customerId={actor.userId}/>
  </main>;
}
