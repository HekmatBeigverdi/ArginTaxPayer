import {redirect} from "next/navigation";
import {currentIdentity,identityUiEnabled} from "../../../lib/identity";
import CheckoutTaxSettings from "../../subscriptions/checkout-tax-settings";
import SmsServiceSettings from "../../subscriptions/sms-service-settings";
import SmsSepSettings from "../../subscriptions/sms-sep-settings";

export const dynamic="force-dynamic";
export default async function AdminSettingsPage(){
 if(!identityUiEnabled())redirect("/");
 const actor=await currentIdentity();
 if(!actor)redirect("/identity");
 if(!actor.roles.includes("Level1"))redirect("/dashboard");
 return <div className="console-page console-settings-page console-admin-settings-page">
  <div className="console-page-heading"><div>
   <p className="app-kicker">مدیریت سامانه</p>
   <h1>تنظیمات مدیریت</h1>
   <p>تنظیمات عمومی نرم‌افزار، مالی و سایر تنظیمات آینده</p>
  </div></div>
  <section className="console-settings-card">
   <div className="console-settings-card-title">
    <h2>تنظیمات مالی نرم‌افزار</h2>
    <p>این مقادیر توسط مدیر سطح یک تعیین می‌شوند.</p>
   </div>
   <CheckoutTaxSettings/>
  </section>
  <section className="console-settings-card">
   <div className="console-settings-card-title"><h2>تنظیمات پیامک</h2><p>هزینه پیامک مشتری برابر هزینه واقعی قاصدک به‌علاوه این درصد است.</p></div>
   <SmsServiceSettings/>
  </section>
  <section className="console-settings-card">
   <div className="console-settings-card-title"><h2>درگاه SEP پیامک</h2><p>شماره ترمینال و ادمین مسئول پرداخت را برای مشتریان این مجموعه تعیین کنید.</p></div>
   <SmsSepSettings/>
  </section>
 </div>;
}
