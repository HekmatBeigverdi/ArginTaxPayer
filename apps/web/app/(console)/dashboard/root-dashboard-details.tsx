"use client";

import Link from "next/link";
import {useEffect,useState} from "react";

type Period=string;
type PartnerPerformance={
  partnerUserId:string;partnerLabel:string;activeCustomerCount:number;
  verifiedCollectionsInPeriodIrr:number;partnerEarnedShareInPeriodIrr:number;
  lifetimePartnerOutstandingIrr:number;
};
type RootOverview={
  schemaVersion:1;role:"Level1";currency:"IRR";asOfUtc:string;
  activeCustomerCount:number;activeSubscriptionCount:number;
  expiringSubscriptionCount:number;activePartnerCount:number;
  lifetimePartnerEarnedIrr:number;lifetimePartnerSettledIrr:number;
  lifetimePartnerOutstandingIrr:number;partners:PartnerPerformance[];
};
type Loadable={state:"loading"|"ready"|"error";data:RootOverview|null};
const fmt=(n:number)=>new Intl.NumberFormat("fa-IR").format(n);
const money=(n:number)=>fmt(n)+" ریال";

function RootSkeleton({rows=2}:{rows?:number}){
  return <div className="argin-dashboard__root-skeleton" role="status" aria-label="در حال دریافت گزارش مدیر اصلی" aria-busy="true">
    <svg viewBox={`0 0 260 ${rows*30+8}`} width="100%" aria-hidden="true" focusable="false">
      {Array.from({length:rows},(_,i)=><g key={i}>
        <rect x="4" y={i*30+4} rx="7" width={i%2?175:210} height="13" className="argin-dashboard__skeleton-bar" fill="#dce9e8"/>
      </g>)}
    </svg>
  </div>;
}
function DashboardRootCard({label,value,note,loading}:{label:string;value:string|null;note:string;loading:boolean}){
  return <article className="argin-dashboard__card">
    <span className="argin-dashboard__kpi-label">{label}</span>
    {loading?<RootSkeleton/>:<strong className="argin-dashboard__partner-value">{value??"—"}</strong>}
    <span className="argin-dashboard__kpi-note">{note}</span>
  </article>;
}
export default function RootDashboardDetails({period}:{period:Period}){
  const [current,setCurrent]=useState<Loadable>({state:"loading",data:null});
  const [version,setVersion]=useState(0);
  useEffect(()=>{
    const controller=new AbortController();
    setCurrent({state:"loading",data:null});
    fetch("/api/dashboard/root?period="+period,{cache:"no-store",signal:controller.signal})
      .then(async response=>{
        if(!response.ok)throw new Error("root_dashboard_unavailable");
        const d=await response.json() as RootOverview;
        if(d.schemaVersion!==1||d.role!=="Level1"||d.currency!=="IRR"||!Array.isArray(d.partners)||
          !Number.isSafeInteger(d.activeCustomerCount)||
          !Number.isSafeInteger(d.lifetimePartnerOutstandingIrr))throw new Error("invalid_root_dashboard_response");
        if(!controller.signal.aborted)setCurrent({state:"ready",data:d});
      }).catch(()=>{if(!controller.signal.aborted)setCurrent({state:"error",data:null});});
    return()=>controller.abort();
  },[period,version]);

  const d=current.data;
  const loading=current.state==="loading";
  const values=[
    {label:"مشتریان فعال سامانه",value:d?fmt(d.activeCustomerCount):null,note:"حساب‌های فعال تحت مدیریت اصلی"},
    {label:"اشتراک‌های فعال",value:d?fmt(d.activeSubscriptionCount):null,note:"اشتراک‌های معتبر مشتریان"},
    {label:"تمدیدهای نزدیک",value:d?fmt(d.expiringSubscriptionCount):null,note:"انقضا در ۳۰ روز پیش رو"},
    {label:"همکاران دارای مشتری",value:d?fmt(d.activePartnerCount):null,note:"مدیران سطح ۲ با مشتری فعال"}
  ];
  const maximum=d?.partners.reduce((max,p)=>Math.max(max,p.verifiedCollectionsInPeriodIrr),0)??0;

  return <section className="argin-dashboard__root" aria-label="گزارش اختصاصی مدیر سطح یک">
    <div className="argin-dashboard__partner-heading">
      <div><span className="argin-dashboard__eyebrow">ویژه مدیر سطح ۱</span>
        <h2>نمای کل سامانه و عملکرد همکاران</h2>
        <p>گزارش مدیریتی فروش اشتراک، مشتریان فعال و وضعیت تسویه همکاری؛ بدون استفاده از مبالغ صورتحساب مالیاتی.</p>
      </div>
      <Link href="/subscriptions" className="argin-dashboard__more">گزارش‌های مالی و اشتراک‌ها</Link>
    </div>
    {current.state==="error"?
      <div className="argin-dashboard__card argin-dashboard__error" role="alert">
        دریافت آمار مدیریتی ممکن نشد. اطلاعات قدیمی به‌عنوان آمار فعلی نمایش داده نمی‌شوند.
        <button type="button" className="argin-dashboard__retry" onClick={()=>setVersion(x=>x+1)}>تلاش مجدد</button>
      </div>:
      <>
        <div className="argin-dashboard__root-metrics">
          {values.map(item=><DashboardRootCard key={item.label} {...item} loading={loading}/>)}
        </div>
        <div className="argin-dashboard__root-panels">
          <article className="argin-dashboard__card">
            <h3 className="argin-dashboard__section-title">وضعیت کلی تسویه همکاران</h3>
            {loading?<RootSkeleton rows={4}/>:d&&<dl className="argin-dashboard__partner-amounts">
              <div><dt>کل سهم تخصیص‌یافته به همکاران</dt><dd>{money(d.lifetimePartnerEarnedIrr)}</dd></div>
              <div><dt>مبلغ خالص تسویه‌شده</dt><dd>{money(d.lifetimePartnerSettledIrr)}</dd></div>
              <div className="argin-dashboard__partner-outstanding"><dt>مانده سهم تسویه‌نشده همکاران</dt><dd>{money(d.lifetimePartnerOutstandingIrr)}</dd></div>
            </dl>}
            <p className="argin-dashboard__kpi-note">وضعیت تجمعی تا این لحظه؛ مستقل از فیلتر تاریخ. برگشت تسویه از پرداخت خالص کسر می‌شود.</p>
          </article>
          <article className="argin-dashboard__card">
            <h3 className="argin-dashboard__section-title">مقایسه عملکرد همکاران</h3>
            {loading?<RootSkeleton rows={6}/>:!d?.partners.length?
              <p className="argin-dashboard__empty">همکاری با مشتری فعال برای مقایسه یافت نشد.</p>:
              <>
                <p className="argin-dashboard__kpi-note">۱۰ همکار برتر از نظر دریافتی تأییدشده در بازه انتخابی</p>
                <div className="argin-dashboard__root-partner-list">
                  {d.partners.map(p=><div key={p.partnerUserId} className="argin-dashboard__root-partner-row">
                    <div className="argin-dashboard__root-partner-title">
                      <strong dir="auto">{p.partnerLabel}</strong>
                      <span>{fmt(p.activeCustomerCount)} مشتری فعال</span>
                    </div>
                    <div className="argin-dashboard__root-partner-bar" aria-label={"دریافتی تأییدشده: "+money(p.verifiedCollectionsInPeriodIrr)}>
                      <span style={{width:(maximum>0?Math.min(100,p.verifiedCollectionsInPeriodIrr/maximum*100):0)+"%"}}/>
                    </div>
                    <div className="argin-dashboard__root-partner-details">
                      <span>دریافتی بازه: <b>{money(p.verifiedCollectionsInPeriodIrr)}</b></span>
                      <span>سهم بازه: <b>{money(p.partnerEarnedShareInPeriodIrr)}</b></span>
                      <span>مانده تسویه: <b>{money(p.lifetimePartnerOutstandingIrr)}</b></span>
                    </div>
                  </div>)}
                </div>
              </>
            }
          </article>
        </div>
      </>}
  </section>;
}
