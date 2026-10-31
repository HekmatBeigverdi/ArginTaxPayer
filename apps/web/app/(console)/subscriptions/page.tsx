import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";
import SubscriptionWorkspace from "../../subscriptions/subscription-workspace";

export const dynamic = "force-dynamic";

export default async function SubscriptionsPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const level1 = actor.roles.includes("Level1");
  if (!level1 && !actor.roles.includes("Level2")) redirect("/dashboard");
  return <div className="console-page">
    <div className="console-page-heading"><div>
      <p className="app-kicker">مدیریت مالی و اشتراک‌ها</p>
      <h1>پلن‌ها، سفارش‌ها و تسویه‌ها</h1>
      <p>ثبت عملیات داخلی مالی و مشاهده دفترکل در محدوده دسترسی مدیر</p>
    </div></div>
    <SubscriptionWorkspace level1={level1}/>
  </div>;
}
