import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled, identityDashboardOnlyEnabled } from "../../lib/identity";
import IdentityScreen from "./identity-screen";

export const dynamic = "force-dynamic";

export default async function IdentityPage() {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (actor) redirect("/dashboard");
  return <main className="identity-layout" dir="rtl" lang="fa">
    <IdentityScreen restrictedLogin={identityDashboardOnlyEnabled()}/>
  </main>;
}
