import { redirect } from "next/navigation";
import { currentIdentity } from "../../../lib/identity";
import ReportsWorkspace from "../../reports/reports-workspace";

export const dynamic = "force-dynamic";
export default async function ReportsPage() {
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  if (!["Level1", "Level2", "Customer"].some(role => actor.roles.includes(role))) redirect("/account");
  return <div className="console-page console-reports-page" dir="rtl" lang="fa">
    <div className="console-page-heading"><div><p className="app-kicker">گزارش‌های مالیاتی</p>
      <h1>گزارشات</h1><p>خلاصه و جزئیات صورتحساب‌های ثبت‌شده</p></div></div>
    <ReportsWorkspace isAdmin={actor.roles.includes("Level1") || actor.roles.includes("Level2")} />
  </div>;
}
