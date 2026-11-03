"use client";
import Link from "next/link";
import DashboardIcon from "./dashboard-icon";
import { useEffect,useState } from "react";
type Status={planName:string;planKind:number|null;expiresAtUtc:string|null;
remainingInvoices:number|null;remainingDays:number;hasActivePlan:boolean};
type SmsCredit={isUnlimited:boolean;availableIrr:number|null};
export default function SubscriptionStatus({compact=false}:{compact?:boolean}){
 const [status,setStatus]=useState<Status|null>(null);
 const [sms,setSms]=useState<SmsCredit|null>(null);
 const [failed,setFailed]=useState(false);
 useEffect(()=>{
  const controller=new AbortController();
  fetch("/api/customer-subscription",{cache:"no-store",signal:controller.signal})
    .then(async response=>{if(!response.ok)throw new Error("Unavailable");return response.json() as Promise<Status>;})
    .then(data=>{if(!controller.signal.aborted)setStatus(data);})
    .catch(()=>{if(!controller.signal.aborted)setFailed(true);});
  fetch("/api/sms-credit",{cache:"no-store",signal:controller.signal})
    .then(async response=>{if(!response.ok)throw new Error("Unavailable");return response.json() as Promise<SmsCredit>;})
    .then(data=>{if(!controller.signal.aborted)setSms(data);}).catch(()=>{});
  return()=>controller.abort();
 },[]);
 if(compact&&(failed||!status))return sms&&!sms.isUnlimited?<Link href="/customer-plans" className="subscription-status-compact" aria-label="مانده اعتبار پیامک">
  مانده پیامک {Math.floor((sms.availableIrr??0)/10).toLocaleString("fa-IR")} تومان
 </Link>:null;
 if(failed)return compact?null:<p>وضعیت اشتراک فعلاً در دسترس نیست.</p>;
 if(!status)return compact?null:<p>در حال دریافت وضعیت اشتراک...</p>;
 const name=status.hasActivePlan?status.planName:"نسخه آزمایشی";
 if(compact)return <Link href="/customer-plans" className="subscription-status-compact" aria-label="وضعیت اشتراک و مشاهده پلن‌ها">
  {name}{status.planKind===2&&status.remainingInvoices!==null?" · "+status.remainingInvoices.toLocaleString("fa-IR")+" فاکتور":""}
  {sms&&!sms.isUnlimited?" · پیامک "+Math.floor((sms.availableIrr??0)/10).toLocaleString("fa-IR")+" تومان":""}
 </Link>;
 return <section className="app-summary subscription-summary" aria-label="اشتراک نرم‌افزار">
  <div><span className="subscription-summary__icon"><DashboardIcon name="subscription" /></span>
    <div><small>پلن فعلی</small><strong>{name}</strong></div></div>
  <div><span className="subscription-summary__icon"><DashboardIcon name="calendar" /></span>
    <div><small>مانده اعتبار</small><strong>{status.hasActivePlan?status.remainingDays.toLocaleString("fa-IR")+" روز":"بدون اشتراک"}</strong></div></div>
  <div><span className="subscription-summary__icon"><DashboardIcon name="invoice" /></span>
    <div><small>سهمیه فاکتور</small><strong>{status.planKind===2?
    (status.remainingInvoices??0).toLocaleString("fa-IR")+" عدد":
    status.hasActivePlan?"نامحدود":"تا ۲ فاکتور آزمایشی"}</strong></div></div>
  {sms&&!sms.isUnlimited&&<div><span className="subscription-summary__icon"><DashboardIcon name="message" /></span>
    <div><small>مانده اعتبار پیامک</small><strong>{Math.floor((sms.availableIrr??0)/10).toLocaleString("fa-IR")} تومان</strong></div></div>}
 </section>;
}
