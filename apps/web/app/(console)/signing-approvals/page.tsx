import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";
import AdminSigningReviewWorkspace from "../../admin-signing-review/admin-signing-review-workspace";

export const dynamic = "force-dynamic";
export default async function SigningApprovalsPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  if (!actor.roles.includes("Level1")) redirect("/dashboard");
  return <main className="console-page" dir="rtl">
    <div className="console-page-heading"><div><p className="app-kicker">کنترل و تأیید سطح یک</p>
      <h1>تأیید تغییر کلید مؤدیان</h1>
      <p>بررسی درخواست‌های کاربران؛ صدور مجوز محدود برای تغییر امن کلید</p>
    </div></div>
    <AdminSigningReviewWorkspace/>
  </main>;
}
