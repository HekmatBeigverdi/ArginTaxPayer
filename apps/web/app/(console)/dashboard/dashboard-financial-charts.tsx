"use client";

import {useEffect,useId,useMemo,useState} from "react";

type Period="7d"|"30d"|"90d"|"year";
type Role="Level1"|"Level2";
type Bucket={
  date:string;salesGrossIrr:number;salesOutstandingForCohortIrr:number;
  collectionsInPeriodIrr:number;partnerEarnedShareIrr:number;
  rootEarnedShareIrr?:number;partnerSettlementNetIrr:number;
};
type Trends={schemaVersion:1;role:Role;currency:"IRR";buckets:Bucket[]};
type Status="loading"|"ready"|"error";
type Metric="salesGrossIrr"|"collectionsInPeriodIrr"|"salesOutstandingForCohortIrr"|"partnerEarnedShareIrr"|"partnerSettlementNetIrr"|"rootEarnedShareIrr";
const fmt=(n:number)=>new Intl.NumberFormat("fa-IR").format(n);
const date=(d:string)=>new Intl.DateTimeFormat("fa-IR-u-ca-persian",{year:"numeric",month:"short",day:"numeric",timeZone:"Asia/Tehran"}).format(new Date(d+"T12:00:00Z"));
const metricLabels:Record<Metric,string>={
  salesGrossIrr:"فروش اشتراک",collectionsInPeriodIrr:"وصول وجه",
  salesOutstandingForCohortIrr:"مانده فروش‌های دوره",partnerEarnedShareIrr:"سهم تخصیص‌یافته همکار",
  partnerSettlementNetIrr:"تسویه خالص همکاران",rootEarnedShareIrr:"سهم مدیر اصلی"
};
const money=(n:number)=>fmt(n)+" ریال";
function ChartSkeleton(){
  return <div className="argin-dashboard__finance-skeleton" role="status" aria-label="در حال دریافت نمودار مالی" aria-busy="true">
    <svg viewBox="0 0 360 160" width="100%" aria-hidden="true" focusable="false">
      {[30,70,110,150].map(y=><line key={y} x1="10" x2="350" y1={y} y2={y} stroke="#e6eff0" strokeDasharray="5 6"/>)}
      <path className="argin-dashboard__skeleton-chart-line" d="M 10 138 L 64 116 L 118 125 L 172 68 L 226 84 L 280 34 L 350 54" fill="none" stroke="#c9e1de" strokeWidth="8" strokeLinecap="round"/>
    </svg>
  </div>;
}
function FinanceLineChart({items,metric}:{items:Bucket[];metric:Metric}){
  const id=useId().replace(/:/g,"");
  const data=useMemo(()=>{
    if(!items.length)return "";
    const max=Math.max(1,...items.map(x=>Math.abs(x[metric]??0)));
    return items.map((x,i)=>{
      const px=items.length===1?180:12+(336*i/(items.length-1));
      const y=154-((x[metric]??0)/max)*130;
      return px.toFixed(1)+","+Math.max(12,Math.min(154,y)).toFixed(1);
    }).join(" ");
  },[items,metric]);
  if(!items.length)return <p className="argin-dashboard__empty">در این بازه هیچ رویداد مالی ثبت نشده است.</p>;
  const last=items[items.length-1];
  return <>
    <div className="argin-dashboard__finance-graph">
      <svg viewBox="0 0 360 170" width="100%" role="img" aria-labelledby={id} preserveAspectRatio="none">
        <title id={id}>نمودار {metricLabels[metric]} بر اساس تاریخ رویداد در تهران</title>
        {[24,88,154].map(y=><line key={y} x1="12" x2="348" y1={y} y2={y} stroke="#dbe9ec" strokeDasharray="4 6"/>)}
        <polyline points={data} fill="none" stroke="#177f81" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"/>
      </svg>
      <div className="argin-dashboard__finance-axis"><span>{date(items[0].date)}</span><span>{date(last.date)}</span></div>
    </div>
    <div className="argin-dashboard__table-wrap">
      <table className="argin-dashboard__table">
        <caption>جدول جایگزین نمودار {metricLabels[metric]}، آخرین هفت تاریخ دارای رویداد</caption>
        <thead><tr><th scope="col">تاریخ</th><th scope="col">مبلغ (ریال)</th></tr></thead>
        <tbody>{items.slice(-7).map(item=><tr key={item.date}><td>{date(item.date)}</td><td>{money(item[metric]??0)}</td></tr>)}</tbody>
      </table>
    </div>
  </>;
}
export default function DashboardFinancialCharts({role,period}:{role:Role;period:Period}){
  const [status,setStatus]=useState<Status>("loading");
  const [data,setData]=useState<Trends|null>(null);
  const [attempt,retry]=useState(0);
  const [metric,setMetric]=useState<Metric>("collectionsInPeriodIrr");
  useEffect(()=>{
    const controller=new AbortController();
    setStatus("loading");setData(null);
    fetch("/api/dashboard/financial-trends?period="+period,{cache:"no-store",signal:controller.signal})
      .then(async response=>{
        if(!response.ok)throw new Error("financial_trends_unavailable");
        const result=await response.json() as Trends;
        if(result.schemaVersion!==1||result.role!==role||result.currency!=="IRR"||!Array.isArray(result.buckets))
          throw new Error("invalid_financial_trends");
        if(!controller.signal.aborted){setData(result);setStatus("ready");}
      }).catch(()=>{if(!controller.signal.aborted){setData(null);setStatus("error");}});
    return()=>controller.abort();
  },[role,period,attempt]);
  const metrics:Metric[]=role==="Level1"?
    ["collectionsInPeriodIrr","salesGrossIrr","salesOutstandingForCohortIrr","partnerEarnedShareIrr","partnerSettlementNetIrr","rootEarnedShareIrr"]:
    ["collectionsInPeriodIrr","salesGrossIrr","salesOutstandingForCohortIrr","partnerEarnedShareIrr","partnerSettlementNetIrr"];
  return <section className="argin-dashboard__card argin-dashboard__finance-section" aria-label="روند مالی اشتراک‌ها">
    <header className="argin-dashboard__finance-heading">
      <div><h2 className="argin-dashboard__section-title">روند مالی اشتراک‌ها و تسویه</h2>
        <p>مبالغ واقعی به ریال و بر اساس تاریخ رویداد به وقت تهران</p></div>
      <div className="argin-dashboard__finance-tabs" role="group" aria-label="انتخاب شاخص نمودار">
        {metrics.map(x=><button key={x} type="button" aria-pressed={metric===x}
          onClick={()=>setMetric(x)}>{metricLabels[x]}</button>)}
      </div>
    </header>
    {status==="loading"?<ChartSkeleton/>:status==="error"?
      <div className="argin-dashboard__error" role="alert">دریافت نمودار مالی ممکن نشد.
        <button type="button" className="argin-dashboard__retry" onClick={()=>retry(v=>v+1)}>تلاش مجدد</button>
      </div>:data&&<FinanceLineChart items={data.buckets} metric={metric}/>}
    <p className="argin-dashboard__kpi-note">
      فروش: تاریخ صدور سفارش؛ وصول: تاریخ دریافت تأییدشده؛ سهم: تاریخ تخصیص؛ تسویه: تاریخ ثبت پرداخت یا برگشت.
      مانده فروش، وضعیت فعلی سفارش‌های صادرشده در هر روز است و جریان نقدی روزانه نیست.
      تاریخ‌هایی که رویدادی نداشته‌اند در نمودار نقطه ندارند.
    </p>
  </section>;
}
