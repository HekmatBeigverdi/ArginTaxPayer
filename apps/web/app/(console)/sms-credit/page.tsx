import {redirect} from "next/navigation";
import {currentIdentity,identityUiEnabled} from "../../../lib/identity";
import SmsTopupPanel from "../../subscriptions/sms-topup-panel";

export const dynamic="force-dynamic";
export default async function SmsCreditPage(){
 if(!identityUiEnabled())redirect("/");
 const actor=await currentIdentity();if(!actor)redirect("/identity");
 const customer=actor.roles.includes("Customer");
 if(!customer&&!actor.roles.includes("Level1")&&!actor.roles.includes("Level2"))redirect("/dashboard");
 return <div className="console-page console-settings-page console-admin-settings-page">
  <div className="console-page-heading">
   <div>
    <p className="app-kicker">{customer?"حساب من":"مدیریت سامانه"}</p>
    <h1>اعتبار پیامک</h1>
    <p>مشاهده مانده و شارژ از طریق درگاه SEP</p>
   </div>
  </div>
  <SmsTopupPanel customer={customer}/>
 </div>;
}
