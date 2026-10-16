import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import CatalogWorkspace from "./workspace";

export const dynamic = "force-dynamic";
export default async function CatalogPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  return <main className="catalog-shell" lang="fa" dir="rtl">
    <header className="catalog-header">
      <div><p className="catalog-kicker">آرگین · کاتالوگ</p><h1>مدیریت کالا و خدمات</h1>
        <p>اطلاعات اختصاصی هر مشتری و جست‌وجوی نسخه‌های تأییدشده شناسه رسمی</p></div>
      <a href="/dashboard" className="catalog-link">بازگشت به داشبورد</a>
    </header>
    <CatalogWorkspace actorId={actor.userId} />
  </main>;
}
