import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";
import AccountControls from "../../account/account-controls";
import AdminNomination from "../../account/admin-nomination";

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
  return <div className="console-page">
    <div className="console-page-heading"><div><p className="app-kicker">حساب کاربری</p>
      <h1>پروفایل و امنیت</h1><p>هویت، احراز هویت دومرحله‌ای و نشست‌های فعال</p></div></div>
    <section className="intro">
      <h2>حساب شما</h2>
      <p>شناسه کاربر: <bdi dir="ltr">{actor.userId}</bdi></p>
      <p>نقش‌های فعلی: {actor.roles.map(role => roleLabels[role] ?? role).join("، ")}</p>
    </section>
    <div id="security"><AccountControls canEnrollMfa={actor.roles.some(role =>
      ["Level1", "Level2", "PendingLevel1", "PendingLevel2"].includes(role))}/></div>
    {actor.roles.includes("Level1") && <AdminNomination/>}
  </div>;
}
