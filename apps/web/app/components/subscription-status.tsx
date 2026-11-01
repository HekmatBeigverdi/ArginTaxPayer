"use client";
import Link from "next/link";
import { useEffect,useState } from "react";
type Status={planName:string;planKind:number|null;expiresAtUtc:string|null;
remainingInvoices:number|null;remainingDays:number;hasActivePlan:boolean};
export default function SubscriptionStatus({compact=false}:{compact?:boolean}){
 const [status,setStatus]=useState<Status|null>(null);
 const [failed,setFailed]=useState(false);
 useEffect(()=>{
  const controller=new AbortController();
  fetch("/api/customer-subscription",{cache:"no-store",signal:controller.signal})
    .then(async response=>{if(!response.ok)throw new Error("Unavailable");return response.json() as Promise<Status>;})
    .then(data=>{if(!controller.signal.aborted)setStatus(data);})
    .catch(()=>{if(!controller.signal.aborted)setFailed(true);});
  return()=>controller.abort();
 },[]);
 if(failed)return compact?null:<p>وضعیت اشتراک فعلاً در دسترس نیست.</p>;
 if(!status)return compact?null:<p>در حال دریافت وضعیت اشتراک...</p>;
 const name=status.hasActivePlan?status.planName.split("-").slice(1).join("-")||status.planName:"نسخه آزمایشی";
 if(compact)return <Link href="/profile" aria-label="وضعیت اشتراک" style={{fontSize:12,padding:"5px 9px",border:"1px solid #bcd6c7",borderRadius:9}}>
  {name}{status.planKind===2&&status.remainingInvoices!==null?" · "+status.remainingInvoices.toLocaleString("fa-IR")+" فاکتور":""}
 </Link>;
 return <section className="app-summary" aria-label="اشتراک نرم‌افزار">
  <div><small>پلن فعلی</small><strong>{name}</strong></div>
  <div><small>مانده اعتبار</small><strong>{status.hasActivePlan?status.remainingDays.toLocaleString("fa-IR")+" روز":"بدون اشتراک"}</strong></div>
  <div><small>سهمیه فاکتور</small><strong>{status.planKind===2?
    (status.remainingInvoices??0).toLocaleString("fa-IR")+" عدد":
    status.hasActivePlan?"نامحدود":"تا ۲ فاکتور آزمایشی"}</strong></div>
 </section>;
}
