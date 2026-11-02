import { redirect } from "next/navigation";
import { currentIdentity,identityUiEnabled } from "../../../lib/identity";
import CustomerFinancialHistory from "../../financial-history/customer-financial-history";

export const dynamic="force-dynamic";
export default async function CustomerFinancialHistoryPage(){
  if(!identityUiEnabled())redirect("/");
  const actor=await currentIdentity();
  if(!actor)redirect("/identity");
  return <div className="console-page">
    <div className="console-page-heading"><div>
      <p className="app-kicker">حساب من</p><h1>پرداخت‌ها و اشتراک‌ها</h1>
      <p>سوابق سفارش و پرداخت بابت خرید و تمدید اشتراک نرم‌افزار</p>
    </div></div>
    <CustomerFinancialHistory/>
  </div>;
}
