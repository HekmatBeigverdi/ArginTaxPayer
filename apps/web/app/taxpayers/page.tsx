import Link from "next/link";
import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import TaxpayerWorkspace from "./taxpayer-workspace";

export const dynamic = "force-dynamic";

export default async function TaxpayersPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const level1 = actor.roles.includes("Level1");
  const level2 = actor.roles.includes("Level2");
  if (!level1 && !level2) redirect("/account");
  return <main className="shell taxpayer-layout" dir="rtl" lang="fa">
    <header className="header">
      <div><p className="eyebrow">ArginTaxPayer · Fiscal Taxpayers</p>
        <h1>مدیریت مؤدیان و حافظه مالیاتی</h1>
        <p className="subtitle">اطلاعات مؤدی و حافظه‌های مجاز — محیط توسعه</p>
      </div>
      <nav className="taxpayer-nav" aria-label="دسترسی سریع">
        <Link href="/account">حساب و امنیت</Link><Link href="/">خانه</Link>
      </nav>
    </header>
    <TaxpayerWorkspace isLevel1={level1}/>
  </main>;
}
