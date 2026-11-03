import {redirect} from "next/navigation";
import {currentIdentity,identityUiEnabled} from "../../../lib/identity";
import CustomerPlans from "../../customer-plans/customer-plans";
export const dynamic="force-dynamic";
export default async function CustomerPlansPage(){
 if(!identityUiEnabled())redirect("/");
 const actor=await currentIdentity();
 if(!actor)redirect("/identity");
 if(!actor.roles.includes("Customer"))redirect("/dashboard");
 return <div className="console-page customer-plans-page"><div className="console-page-heading">
  <div><p className="app-kicker">اشتراک نرم‌افزار</p><h1>پلن‌های نرم‌افزار</h1>
  <p>انتخاب پلن در محدوده مدیر تجاری حساب شما</p></div></div>
  <CustomerPlans/>
 </div>;
}
