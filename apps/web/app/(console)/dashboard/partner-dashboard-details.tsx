"use client";

import Link from "next/link";
import {useEffect,useState} from "react";

type PartnerOverview={
  schemaVersion:number;role:"Level2";asOfUtc:string;currency:"IRR";
  activeCustomerCount:number;activeSubscriptionCount:number;expiringSubscriptionCount:number;
  lifetimePartnerEarnedIrr:number;lifetimePartnerSettledIrr:number;lifetimePartnerOutstandingIrr:number;
};
type Finance={partnerEarnedShareIrr:number;partnerSettlementNetIrr:number;collectionsInPeriodIrr:number;currency:"IRR"};
type LoadState="loading"|"ready"|"error";
type Props={finance:Finance|null;financeState:LoadState;periodLabel:string;onRetry:()=>void};
const fmt=(n:number)=>new Intl.NumberFormat("fa-IR").format(n);
const money=(n:number)=>fmt(n)+" ریال";

function Shimmer({rows=2}:{rows?:number}){
  return <div className="argin-dashboard__partner-shimmer" aria-label="در حال دریافت اطلاعات همکار" role="status" aria-busy="true">
    <svg aria-hidden="true" focusable="false" viewBox="0 0 240 62" width="100%" preserveAspectRatio="none">
      {Array.from({length:rows},(_,i)=><rect key={i} x="7" y={8+i*27} rx="7"
        width={i===0?120:192} height="13" className="argin-dashboard__skeleton-bar" fill="#dbeae8"/>)}
    </svg>
  </div>;
}
export default function PartnerDashboardDetails({finance,financeState,periodLabel,onRetry}:Props){
  const [state,setState]=useState<LoadState>("loading");
  const [data,setData]=useState<PartnerOverview|null>(null);
  const [version,setVersion]=useState(0);
  useEffect(()=>{
    const controller=new AbortController();
    setState("loading");setData(null);
    fetch("/api/dashboard/partner",{cache:"no-store",signal:controller.signal}).then(async response=>{
      if(!response.ok)throw new Error("partner_overview_unavailable");
      const result=await response.json() as PartnerOverview;
      if(result.schemaVersion!==1||result.role!=="Level2"||result.currency!=="IRR"||
        !Number.isFinite(result.activeCustomerCount)||
        !Number.isFinite(result.lifetimePartnerOutstandingIrr))throw new Error("invalid_partner_response");
      if(!controller.signal.aborted){setData(result);setState("ready");}
    }).catch(()=>{if(!controller.signal.aborted){setData(null);setState("error");}});
    return()=>controller.abort();
  },[version]);
  return <section className="argin-dashboard__partner" aria-label="گزارش اختصاصی مدیر سطح دو">
    <div className="argin-dashboard__partner-heading">
      <div>
        <span className="argin-dashboard__eyebrow">ویژه مدیر سطح ۲</span>
        <h2>عملکرد مشتریان و وضعیت سهم همکاری</h2>
        <p>اطلاعات صرفاً برای مشتریان دارای مالکیت فعال شما محاسبه شده است.</p>
      </div>
      <Link href="/subscriptions" className="argin-dashboard__more">مشاهده حساب‌ها و تسویه‌ها</Link>
    </div>
    <div className="argin-dashboard__partner-grid">
      {[
        {label:"مشتریان فعال",value:data?fmt(data.activeCustomerCount):null,note:"تعداد مشتریان دارای حساب فعال"},
        {label:"اشتراک‌های فعال",value:data?fmt(data.activeSubscriptionCount):null,note:"اشتراک‌های معتبر در حال حاضر"},
        {label:"اشتراک‌های نزدیک تمدید",value:data?fmt(data.expiringSubscriptionCount):null,note:"انقضا در ۳۰ روز آینده"}
      ].map(item=><article key={item.label} className="argin-dashboard__card">
        <span className="argin-dashboard__kpi-label">{item.label}</span>
        {state==="loading"?<Shimmer/>:state==="error"?<span className="argin-dashboard__kpi-note" role="alert">گزارش در دسترس نیست</span>:
          <strong className="argin-dashboard__partner-value">{item.value}</strong>}
        <span className="argin-dashboard__kpi-note">{item.note}</span>
      </article>)}
    </div>
    <div className="argin-dashboard__partner-finance">
      <article className="argin-dashboard__card">
        <h3 className="argin-dashboard__section-title">وضعیت تسویه سهم همکاری</h3>
        {state==="loading"?<Shimmer rows={2}/>:state==="error"?<div className="argin-dashboard__error" role="alert">
          دریافت مانده تسویه انجام نشد.
          <button type="button" className="argin-dashboard__retry" onClick={()=>setVersion(v=>v+1)}>تلاش مجدد</button>
        </div>:data&&<>
          <dl className="argin-dashboard__partner-amounts">
            <div><dt>مجموع سهم تخصیص‌یافته</dt><dd>{money(data.lifetimePartnerEarnedIrr)}</dd></div>
            <div><dt>تسویه خالص ثبت‌شده</dt><dd>{money(data.lifetimePartnerSettledIrr)}</dd></div>
            <div className="argin-dashboard__partner-outstanding"><dt>مانده قابل تسویه</dt><dd>{money(data.lifetimePartnerOutstandingIrr)}</dd></div>
          </dl>
          <p className="argin-dashboard__kpi-note">مبالغ بالا وضعیت تجمیعی تا این لحظه هستند و به فیلتر بازه زمانی وابسته نیستند.</p>
        </>}
      </article>
      <article className="argin-dashboard__card">
        <h3 className="argin-dashboard__section-title">جریان مالی {periodLabel}</h3>
        {financeState==="loading"?<Shimmer rows={2}/>:financeState==="error"?<div className="argin-dashboard__error" role="alert">
          دریافت گزارش این بازه ممکن نشد.
          <button type="button" className="argin-dashboard__retry" onClick={onRetry}>تلاش مجدد</button>
        </div>:finance&&<dl className="argin-dashboard__partner-amounts">
          <div><dt>دریافتی تأییدشده مشتریان</dt><dd>{money(finance.collectionsInPeriodIrr)}</dd></div>
          <div><dt>سهم تخصیص‌یافته در بازه</dt><dd>{money(finance.partnerEarnedShareIrr)}</dd></div>
          <div><dt>تسویه خالص در بازه</dt><dd>{money(finance.partnerSettlementNetIrr)}</dd></div>
        </dl>}
        <p className="argin-dashboard__kpi-note">دریافتی، سهم تخصیص‌یافته و تسویه سه مفهوم مستقل هستند؛ این گزارش سود و زیان حسابداری نیست.</p>
      </article>
    </div>
  </section>;
}
