import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";
import UsersWorkspace from "../../users/users-workspace";
import AdminCreateCustomer from "../../account/admin-create-customer";

export const dynamic = "force-dynamic";
export default async function UsersPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  const isLevel1 = actor.roles.includes("Level1");
  const isLevel2 = actor.roles.includes("Level2");
  if (!isLevel1 && !isLevel2) redirect("/account");
  return <div className="console-page">
    <div className="console-page-heading"><div><p className="app-kicker">مدیریت دسترسی</p><h1>کاربران نرم‌افزار</h1>
      <p>حساب‌های ورود، نقش‌ها و مالکیت تجاری کاربران سامانه</p></div></div>
    <UsersWorkspace level1={isLevel1}/>
  </div>;
}
