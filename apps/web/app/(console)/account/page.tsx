import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";
import AccountControls from "../../account/account-controls";
import AdminNomination from "../../account/admin-nomination";

export const dynamic = "force-dynamic";

export default async function AccountPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  return <div className="console-page console-settings-page">
    <div className="console-page-heading"><div>
      <p className="app-kicker">حریم خصوصی و دسترسی</p>
      <h1>حساب و امنیت</h1>
      <p>تنظیمات احراز هویت و حفاظت از دسترسی به حساب</p>
    </div></div>
    <section className="console-settings-card" id="security">
      <div className="console-settings-card-title"><h2>اطلاعات امنیتی</h2>
        <p>تنظیمات احراز هویت دومرحله‌ای حساب</p></div>
      <AccountControls canEnrollMfa={actor.roles.some(role =>
        ["Level1", "Level2", "PendingLevel1", "PendingLevel2"].includes(role))}/>
    </section>
    {actor.roles.includes("Level1") && <section className="console-settings-card">
      <AdminNomination/>
    </section>}
  </div>;
}
