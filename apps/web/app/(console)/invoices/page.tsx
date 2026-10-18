import { redirect } from "next/navigation";
import { currentIdentity } from "../../../lib/identity";
import InvoiceEntryWorkspace from "../../invoices/workspace";

export const dynamic = "force-dynamic";
export default async function InvoicesPage() {
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const allowed = ["Level1", "Level2", "Customer"].some(r => actor.roles.includes(r));
  if (!allowed) redirect("/account");
  return <div className="console-page console-invoices-page" dir="rtl" lang="fa">
    <div className="console-page-heading">
      <div><p className="app-kicker">مدیریت پیش‌نویس‌های مالیاتی</p>
        <h1>صورتحساب‌های الکترونیکی</h1>
        <p>ایجاد و مشاهده پیش‌نویس، آماده‌سازی اقلام و الگوی صورتحساب</p>
      </div>
    </div>
    <InvoiceEntryWorkspace actorId={actor.userId} isAdmin={actor.roles.includes("Level1") || actor.roles.includes("Level2")} />
  </div>;
}
