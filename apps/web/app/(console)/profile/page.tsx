import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../../lib/identity";
import SoftwareProfileForm from "../../account/software-profile-form";

export const dynamic = "force-dynamic";
export default async function ProfilePage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  return <div className="console-page console-settings-page">
    <div className="console-page-heading"><div>
      <p className="app-kicker">مشخصات حساب</p>
      <h1>پروفایل و حساب من</h1>
      <p>اطلاعات فردی، نشانی و لوگوی مورد استفاده در فاکتور</p>
    </div></div>
    <SoftwareProfileForm required={actor.roles.includes("Customer")}/>
  </div>;
}
