"use client";

import Link from "next/link";
import {useEffect,useState} from "react";

type Role="Level1"|"Level2";
type WatchItem={subscriptionId:string;planKind:number;expiresAtUtc:string;remainingDays:number;remainingInvoices:number|null;status:"quota_exhausted"|"urgent"|"upcoming"};
type Watch={schemaVersion:1;role:Role;asOfUtc:string;activeSubscriptionCount:number;expiringWithinSevenDays:number;expiringWithinThirtyDays:number;exhaustedQuantityPlans:number;items:WatchItem[]};
type Load="loading"|"ready"|"error";
const count=(n:number)=>new Intl.NumberFormat("fa-IR").format(n);
const jalali=(s:string)=>new Intl.DateTimeFormat("fa-IR-u-ca-persian",{dateStyle:"medium",timeZone:"Asia/Tehran"}).format(new Date(s));
function Loading(){
  return <div className="argin-dashboard__watch-loading" role="status" aria-busy="true" aria-label="در حال دریافت اشتراک‌ها">
    <svg viewBox="0 0 370 126" width="100%" aria-hidden="true" focusable="false">
      {[0,1,2].map(i=><g key={i}>
        <rect className="argin-dashboard__skeleton-bar" x="5" y={i*41+5} width="180" height="12" rx="6" fill="#d4e6e4"/>
        <rect className="argin-dashboard__skeleton-bar" x="285" y={i*41+5} width="80" height="12" rx="6" fill="#e6eff0"/>
      </g>)}
    </svg>
  </div>;
}
export default function DashboardSubscriptionWatch({role}:{role:Role}){
  const [status,setStatus]=useState<Load>("loading");
  const [watch,setWatch]=useState<Watch|null>(null);
  const [version,retry]=useState(0);
  useEffect(()=>{
    const controller=new AbortController();
    setStatus("loading");setWatch(null);
    fetch("/api/dashboard/subscriptions-watch",{cache:"no-store",signal:controller.signal})
      .then(async response=>{
        if(!response.ok)throw new Error("subscription_watch_unavailable");
        const data=await response.json() as Watch;
        if(data.schemaVersion!==1||data.role!==role||!Array.isArray(data.items)||
          !Number.isSafeInteger(data.activeSubscriptionCount)||
          !Number.isSafeInteger(data.expiringWithinThirtyDays))
          throw new Error("invalid_subscription_watch");
        if(!controller.signal.aborted){setWatch(data);setStatus("ready");}
      }).catch(()=>{if(!controller.signal.aborted){setWatch(null);setStatus("error");}});
    return()=>controller.abort();
  },[role,version]);
  const stats=watch?[
    {label:"اشتراک‌های فعال",value:watch.activeSubscriptionCount},
    {label:"انقضا تا ۷ روز",value:watch.expiringWithinSevenDays},
    {label:"انقضا تا ۳۰ روز",value:watch.expiringWithinThirtyDays},
    {label:"بسته‌های بدون سهمیه",value:watch.exhaustedQuantityPlans},
  ]:[];
  return <section className="argin-dashboard__card argin-dashboard__watch" aria-label="وضعیت اشتراک و تمدیدها">
    <div className="argin-dashboard__watch-head">
      <div><h2 className="argin-dashboard__section-title">اشتراک‌ها و تمدیدهای نزدیک</h2>
        <p>وضعیت فعلی مشتریان مجاز، مستقل از فیلتر تاریخ نمودارها</p></div>
      <Link href="/subscriptions">مدیریت اشتراک‌ها</Link>
    </div>
    {status==="loading"?<Loading/>:status==="error"?
      <div className="argin-dashboard__error" role="alert">گزارش اشتراک‌ها در دسترس نیست.
        <button type="button" className="argin-dashboard__retry" onClick={()=>retry(v=>v+1)}>تلاش مجدد</button>
      </div>:watch&&<>
        <div className="argin-dashboard__watch-stats">
          {stats.map(x=><div className="argin-dashboard__watch-stat" key={x.label}>
            <span>{x.label}</span><strong>{count(x.value)}</strong>
          </div>)}
        </div>
        {watch.items.length===0?<p className="argin-dashboard__empty">اشتراک نیازمند تمدید یا بسته بدون سهمیه یافت نشد.</p>:
          <div className="argin-dashboard__table-wrap">
            <table className="argin-dashboard__table">
              <caption>حداکثر ۱۰ اشتراک نیازمند رسیدگی؛ بدون اطلاعات تماس مشتری</caption>
              <thead><tr><th scope="col">اشتراک</th><th scope="col">تاریخ انقضا</th><th scope="col">روز باقی‌مانده</th><th scope="col">سهمیه</th><th scope="col">وضعیت</th></tr></thead>
              <tbody>{watch.items.map(item=><tr key={item.subscriptionId}>
                <td><code dir="ltr">{item.subscriptionId.slice(0,8)}</code></td>
                <td>{jalali(item.expiresAtUtc)}</td>
                <td>{count(item.remainingDays)}</td>
                <td>{item.remainingInvoices===null?"نامحدود":count(item.remainingInvoices)}</td>
                <td><span className="argin-dashboard__status" data-tone={item.status==="upcoming"?"warning":"danger"}>
                  {item.status==="quota_exhausted"?"سهمیه تمام شده":item.status==="urgent"?"انقضای فوری":"نزدیک تمدید"}</span></td>
              </tr>)}</tbody>
            </table>
          </div>}
        <p className="argin-dashboard__kpi-note">برای بررسی جزئیات و تمدید، وارد مدیریت اشتراک‌ها شوید. این گزارش مجوز ارسال صورتحساب یا تمدید خودکار ایجاد نمی‌کند.</p>
      </>}
  </section>;
}
