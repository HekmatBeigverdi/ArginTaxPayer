"use client";

import Link from "next/link";
import { useEffect, useId, useMemo, useState } from "react";

type Role = "Level1" | "Level2" | "Customer";
type Period = "7d" | "30d" | "90d" | "year";
type InvoiceStatus = {
  logicalInvoiceCount:number;accepted:number;acceptedCancellation:number;
  rejected:number;processing:number;awaitingInquiry:number;unknown:number;
  prepared:number;attention:number;unclassified:number;successRatePercent:number|null;
};
type Overview = {
  schemaVersion:number;role:Role;asOfUtc:string;
  invoiceStatus:InvoiceStatus;invoiceDrafts:number;
  subscription:null|{planName:string;hasActivePlan:boolean;remainingInvoices:number|null;remainingDays:number;expiresAtUtc:string|null};
};
type Trends = {schemaVersion:number;buckets:{date:string;issued:number;acceptedByIssueDate:number}[]};
type Finance = {
  schemaVersion:number;role:"Level1"|"Level2";currency:"IRR";
  salesGrossIrr:number;salesCollectedForCohortIrr:number;salesOutstandingIrr:number;
  collectionsInPeriodIrr:number;partnerEarnedShareIrr:number;
  rootEarnedShareIrr?:number;partnerSettlementNetIrr:number;
};
type AttentionItem={invoiceId:string;taxpayerId:string;status:string;issuedAtUtc:string;link:string};
type AttentionResponse={items:AttentionItem[]};
type RemoteState="loading"|"ready"|"error";

const fmt=(value:number)=>new Intl.NumberFormat("fa-IR").format(value);
const money=(value:number)=>fmt(value)+" ریال";
const dates=(value:string)=>new Intl.DateTimeFormat("fa-IR-u-ca-persian",{year:"numeric",month:"short",day:"numeric",timeZone:"Asia/Tehran"}).format(new Date(value+"T12:00:00Z"));
const periodNames:Record<Period,string>={"7d":"۷ روز","30d":"۳۰ روز","90d":"۹۰ روز","year":"سال جاری"};
const statusLabels:Record<string,string> = {
  unknown:"نتیجه نامشخص",rejected:"ردشده",attention:"نیازمند بررسی",
  accepted:"تأییدشده",prepared:"آماده ارسال",awaitingInquiry:"در انتظار استعلام",processing:"در حال ارسال",unclassified:"تعیین‌نشده"
};
function query(section:string,period:Period,signal:AbortSignal){
  return fetch("/api/dashboard/"+section+"?period="+period,{cache:"no-store",signal})
    .then(async response=>{if(!response.ok)throw new Error("dashboard_unavailable");return response.json() as Promise<unknown>;});
}
function DashboardKpiCard({label,value,note,tone}:{label:string;value:string;note:string;tone?:"success"|"warning"|"danger"}){
  return <article className="argin-dashboard__card" aria-label={label}>
    <span className="argin-dashboard__kpi-label">{label}</span>
    <strong className="argin-dashboard__kpi-value" data-tone={tone}>{value}</strong>
    <span className="argin-dashboard__kpi-note">{note}</span>
  </article>;
}
function DashboardPanel({title,children}:{title:string;children:React.ReactNode}){
  return <section className="argin-dashboard__card" aria-label={title}><h2 className="argin-dashboard__section-title">{title}</h2>{children}</section>;
}
function DashboardState({kind,onRetry}:{kind:"loading"|"error"|"empty";onRetry?:()=>void}){
  const text=kind==="loading"?"در حال دریافت آمار واقعی...":kind==="error"?"دریافت آمار ممکن نشد؛ مقادیر قدیمی یا ساختگی نمایش داده نمی‌شود.":"برای این بازه زمانی اطلاعاتی ثبت نشده است.";
  return <div className={"argin-dashboard__"+kind} role={kind==="error"?"alert":"status"} aria-live="polite">
    {text} {kind==="error"&&onRetry&&<button type="button" onClick={onRetry} className="argin-dashboard__retry">تلاش مجدد</button>}
  </div>;
}
/** Each region keeps its geometry while independently fetching; SVG has no simulated numbers. */
function DashboardSkeleton({variant="lines",rows=3}:{variant?:"lines"|"chart"|"list";rows?:number}){
  const gid=useId().replace(/:/g,"");
  const height=variant==="chart"?176:variant==="list"?Math.max(80,rows*37):74;
  return <div className="argin-dashboard__skeleton" role="status" aria-label="در حال دریافت اطلاعات از سرور" aria-busy="true">
    <svg viewBox={`0 0 400 ${height}`} width="100%" aria-hidden="true" focusable="false" preserveAspectRatio="none">
      <defs><linearGradient id={gid} x1="0" x2="1">
        <stop offset="0%" stopColor="#e9f0f2"/>
        <stop offset="48%" stopColor="#d6e9e7"/>
        <stop offset="100%" stopColor="#e9f0f2"/>
      </linearGradient></defs>
      {variant==="chart"?<>
        {[40,88,136].map(y=><line key={y} x1="8" x2="392" y1={y} y2={y} stroke="#e8eef0" strokeDasharray="5 6"/>)}
        <path className="argin-dashboard__skeleton-chart-line" d="M 12 138 L 60 114 L 110 126 L 155 68 L 205 90 L 255 42 L 310 68 L 388 22" fill="none" stroke={`url(#${gid})`} strokeWidth="9" strokeLinecap="round" strokeLinejoin="round"/>
      </>:Array.from({length:rows},(_,i)=>{
        const y=variant==="list"?5+i*37:9+i*24;
        return <g key={i}>
          <rect className="argin-dashboard__skeleton-bar" x="8" y={y} rx="7" width={i%3===0?236:i%3===1?342:175} height="13" fill={`url(#${gid})`}/>
          {variant==="list"&&<rect className="argin-dashboard__skeleton-bar" x="330" y={y} rx="7" width="61" height="13" fill={`url(#${gid})`}/>}
        </g>;
      })}
    </svg>
    <span className="argin-dashboard__sr-only">در حال دریافت اطلاعات از سرور</span>
  </div>;
}
function DashboardKpiSkeleton(){
  return <section aria-label="در حال دریافت شاخص‌ها" className="argin-dashboard__kpis" aria-busy="true">
    {Array.from({length:4},(_,i)=><article key={i} className="argin-dashboard__card">
      <DashboardSkeleton rows={3}/>
    </article>)}
  </section>;
}
function DashboardPeriodFilter({period,setPeriod}:{period:Period;setPeriod:(period:Period)=>void}){
  return <div className="argin-dashboard__filters" role="group" aria-label="بازه زمانی آمار">
    {(Object.keys(periodNames) as Period[]).map(key=><button type="button" key={key} aria-pressed={period===key} onClick={()=>setPeriod(key)}>{periodNames[key]}</button>)}
  </div>;
}
function DashboardTrendChart({data}:{data:Trends["buckets"]}){
  const points=useMemo(()=>{
    if(!data.length)return {issued:"",accepted:"",labels:[] as string[]};
    const max=Math.max(1,...data.map(x=>x.issued),...data.map(x=>x.acceptedByIssueDate));
    const coords=(field:"issued"|"acceptedByIssueDate")=>data.map((item,i)=>{
      const x=data.length===1?160:12+i*(296/(data.length-1));
      const y=122-item[field]/max*104;
      return x.toFixed(2)+","+y.toFixed(2);
    }).join(" ");
    return {issued:coords("issued"),accepted:coords("acceptedByIssueDate"),
      labels:[data[0].date,data[Math.floor((data.length-1)/2)].date,data[data.length-1].date]};
  },[data]);
  if(!data.length)return <DashboardState kind="empty"/>;
  return <>
    <div className="argin-dashboard__chart">
      <svg viewBox="0 0 320 140" width="100%" role="img" aria-label="نمودار تعداد صدور صورتحساب و وضعیت تأیید به تفکیک تاریخ صدور" preserveAspectRatio="none">
        {[18,70,122].map(y=><line key={y} x1="12" x2="308" y1={y} y2={y} stroke="#dce7e9" strokeDasharray="4 5"/>)}
        <polyline fill="none" stroke="#177f81" strokeWidth="2.8" strokeLinejoin="round" strokeLinecap="round" points={points.issued}/>
        <polyline fill="none" stroke="#e2a54b" strokeWidth="2.4" strokeLinejoin="round" strokeLinecap="round" points={points.accepted}/>
      </svg>
      <div className="argin-dashboard__chart-dates">{points.labels.map((date,i)=><span key={i}>{dates(date)}</span>)}</div>
    </div>
    <div className="argin-dashboard__legend"><span><i className="argin-dashboard__dot" data-tone="primary"/>صدورشده</span><span><i className="argin-dashboard__dot" data-tone="amber"/>دارای تأیید قطعی (براساس تاریخ صدور)</span></div>
    <p className="argin-dashboard__kpi-note">تأییدها بر مبنای تاریخ صدور صورتحساب محاسبه شده‌اند، نه تاریخ پاسخ سازمان.</p>
    <div className="argin-dashboard__table-wrap"><table className="argin-dashboard__table"><caption>جدول معادل نمودار، آخرین روزهای دارای فعالیت</caption><thead><tr><th scope="col">روز</th><th scope="col">صدور</th><th scope="col">تأیید</th></tr></thead><tbody>{data.slice(-7).map((d,i)=><tr key={i}><td>{dates(d.date)}</td><td>{fmt(d.issued)}</td><td>{fmt(d.acceptedByIssueDate)}</td></tr>)}</tbody></table></div>
  </>;
}
function DashboardStatusChart({status}:{status:InvoiceStatus}){
  const groups=[
    {label:"تأیید قطعی",value:status.accepted+status.acceptedCancellation,tone:"success"},
    {label:"ردشده",value:status.rejected,tone:"danger"},
    {label:"نامشخص",value:status.unknown,tone:"warning"},
    {label:"در انتظار استعلام",value:status.awaitingInquiry,tone:"neutral"},
    {label:"آماده / در جریان",value:status.prepared+status.processing,tone:"neutral"},
    {label:"نیازمند بررسی",value:status.attention,tone:"warning"},
    {label:"تعیین‌نشده",value:status.unclassified,tone:"neutral"},
  ] as const;
  if(!status.logicalInvoiceCount)return <DashboardState kind="empty"/>;
  return <div className="argin-dashboard__composition">{groups.filter(x=>x.value>0).map(group=>
    <div className="argin-dashboard__composition-row" key={group.label}>
      <div><span>{group.label}</span><strong>{fmt(group.value)}</strong></div>
      <div className="argin-dashboard__track"><span data-tone={group.tone} style={{width:Math.min(100,group.value/status.logicalInvoiceCount*100)+"%"}}/></div>
    </div>
  )}<p className="argin-dashboard__kpi-note">هر زنجیره صورتحساب یک‌بار شمرده می‌شود. رسید ارسال به‌معنای تأیید سازمان نیست.</p></div>;
}
type Loadable<T>={status:RemoteState;data:T|null};
const waiting=<T,>():Loadable<T>=>({status:"loading",data:null});

export default function DashboardAnalytics({role}:{role:Role}){
  const [period,setPeriod]=useState<Period>("30d");
  const [version,retry]=useState(0);
  const [overviewState,setOverviewState]=useState<Loadable<Overview>>(waiting);
  const [trendState,setTrendState]=useState<Loadable<Trends>>(waiting);
  const [attentionState,setAttentionState]=useState<Loadable<AttentionItem[]>>(waiting);
  const [financeState,setFinanceState]=useState<Loadable<Finance>>(waiting);
  const admin=role!=="Customer";

  useEffect(()=>{
    const controller=new AbortController();
    // Requests settle independently: a slow chart or finance feed must not block the KPI cards.
    setOverviewState(waiting());setTrendState(waiting());setAttentionState(waiting());setFinanceState(waiting());
    query("overview",period,controller.signal).then(data=>{
      const next=data as Overview;
      if(next.schemaVersion!==1||next.role!==role||!next.invoiceStatus)throw new Error("invalid_dashboard_response");
      if(!controller.signal.aborted)setOverviewState({status:"ready",data:next});
    }).catch(()=>{if(!controller.signal.aborted)setOverviewState({status:"error",data:null});});
    query("trends",period,controller.signal).then(data=>{
      const next=data as Trends;
      if(next.schemaVersion!==1||!Array.isArray(next.buckets))throw new Error("invalid_dashboard_response");
      if(!controller.signal.aborted)setTrendState({status:"ready",data:next});
    }).catch(()=>{if(!controller.signal.aborted)setTrendState({status:"error",data:null});});
    query("attention",period,controller.signal).then(data=>{
      const next=data as AttentionResponse;
      if(!Array.isArray(next.items))throw new Error("invalid_dashboard_response");
      if(!controller.signal.aborted)setAttentionState({status:"ready",data:next.items});
    }).catch(()=>{if(!controller.signal.aborted)setAttentionState({status:"error",data:null});});
    if(admin)query("finance",period,controller.signal).then(data=>{
      const next=data as Finance;
      if(next.schemaVersion!==1||next.role!==role||next.currency!=="IRR")throw new Error("invalid_dashboard_response");
      if(!controller.signal.aborted)setFinanceState({status:"ready",data:next});
    }).catch(()=>{if(!controller.signal.aborted)setFinanceState({status:"error",data:null});});
    return()=>controller.abort();
  },[role,admin,period,version]);

  function changePeriod(next:Period){
    if(next===period)return;
    setOverviewState(waiting());setTrendState(waiting());setAttentionState(waiting());setFinanceState(waiting());
    setPeriod(next);
  }
  const overview=overviewState.data;
  const finance=financeState.data;
  const kpiState=admin?financeState:overviewState;
  const cards=kpiState.status==="ready"?(admin&&finance?[
    {label:"فروش اشتراک",value:money(finance.salesGrossIrr),note:"مبلغ سفارش‌های صادرشده در بازه"},
    {label:"دریافتی تأییدشده",value:money(finance.collectionsInPeriodIrr),note:"دریافت‌های ثبت‌شده در بازه",tone:"success" as const},
    {label:"مطالبات فروش",value:money(finance.salesOutstandingIrr),note:"مانده سفارش‌های همین بازه"},
    {label:role==="Level1"?"سهم مدیر سطح ۱":"سهم همکاری شما",value:role==="Level1"&&finance.rootEarnedShareIrr===undefined?"نامشخص":money(role==="Level1"?finance.rootEarnedShareIrr:finance.partnerEarnedShareIrr),note:"سهم ثبت‌شده در بازه"}
  ]:(!admin&&overview?[
    {label:"صورتحساب‌ها",value:fmt(overview.invoiceStatus.logicalInvoiceCount),note:"زنجیره‌های یکتا در بازه"},
    {label:"تأییدشده توسط سازمان",value:fmt(overview.invoiceStatus.accepted+overview.invoiceStatus.acceptedCancellation),note:"فقط وضعیت قطعی",tone:"success" as const},
    {label:"نامشخص / نیازمند بررسی",value:fmt(overview.invoiceStatus.unknown+overview.invoiceStatus.attention+overview.invoiceStatus.rejected),note:"بدون ارسال مجدد خودکار",tone:"warning" as const},
    {label:"پیش‌نویس‌های فعال",value:fmt(overview.invoiceDrafts),note:"پیش‌نویس‌های تبدیل‌نشده در بازه"}
  ]:[])):[];
  const retryRead=()=>retry(n=>n+1);
  const visibleAttention=attentionState.data?.slice(0,5)??[];
  return <div className="argin-dashboard" dir="rtl">
    <header className="argin-dashboard__header">
      <div><span className="argin-dashboard__eyebrow">پیشخوان تحلیلی آرگین</span>
        <h1 className="argin-dashboard__title">{role==="Level1"?"نمای مدیریتی کل سامانه":role==="Level2"?"عملکرد مشتریان زیرمجموعه":"وضعیت صورتحساب‌های شما"}</h1>
        <p className="argin-dashboard__subtitle">گزارش‌های واقعی متناسب با دسترسی شما؛ {overviewState.status==="ready"&&overview?"آخرین دریافت: "+new Intl.DateTimeFormat("fa-IR",{dateStyle:"short",timeStyle:"short",timeZone:"Asia/Tehran"}).format(new Date(overview.asOfUtc)):"اطلاعات هر بخش به‌صورت مستقل دریافت می‌شود"}</p>
      </div><DashboardPeriodFilter period={period} setPeriod={changePeriod}/>
    </header>
    {kpiState.status==="loading"?<DashboardKpiSkeleton/>:kpiState.status==="error"?
      <section className="argin-dashboard__card argin-dashboard__kpi-error" aria-label="شاخص‌های کلیدی"><DashboardState kind="error" onRetry={retryRead}/></section>:
      <section aria-label="شاخص‌های کلیدی" className="argin-dashboard__kpis">{cards.map(card=><DashboardKpiCard key={card.label} {...card}/>)}</section>}
    <div className="argin-dashboard__charts">
      <DashboardPanel title="روند صورتحساب‌ها">
        {trendState.status==="loading"?<DashboardSkeleton variant="chart"/>:trendState.status==="error"?
          <DashboardState kind="error" onRetry={retryRead}/>:<DashboardTrendChart data={trendState.data?.buckets??[]}/>}
      </DashboardPanel>
      <DashboardPanel title="ترکیب آخرین وضعیت صورتحساب‌ها">
        {overviewState.status==="loading"?<DashboardSkeleton variant="chart"/>:overviewState.status==="error"?
          <DashboardState kind="error" onRetry={retryRead}/>:overview?
            <DashboardStatusChart status={overview.invoiceStatus}/>:<DashboardState kind="empty"/>}
      </DashboardPanel>
    </div>
    <div className="argin-dashboard__footer-grid">
      <DashboardPanel title="موارد نیازمند پیگیری">
        {attentionState.status==="loading"?<DashboardSkeleton variant="list" rows={4}/>:attentionState.status==="error"?
          <DashboardState kind="error" onRetry={retryRead}/>:!visibleAttention.length?<DashboardState kind="empty"/>:
          <><ul className="argin-dashboard__attention">{visibleAttention.map(item=>
            <li key={item.invoiceId}><span className="argin-dashboard__status" data-tone="warning">{statusLabels[item.status]??"نیازمند بررسی"}</span><span>{dates(item.issuedAtUtc.slice(0,10))}</span><Link href="/invoices">مشاهده صورتحساب</Link></li>)}</ul>
          <Link className="argin-dashboard__more" href="/invoices">مشاهده همه صورتحساب‌ها</Link></>}
      </DashboardPanel>
      <div className="argin-dashboard__side">
        <DashboardPanel title="دسترسی سریع">
          <nav className="argin-dashboard__quick" aria-label="اقدامات پرکاربرد"><Link href="/invoices">صورتحساب‌ها</Link>
            <Link href="/parties">خریداران و مؤدیان</Link>
            <Link href={admin?"/subscriptions":"/account"}>{admin?"اشتراک‌ها و فروش":"اشتراک من"}</Link>
          </nav>
          {role==="Customer"&&overviewState.status==="loading"&&
            <div className="argin-dashboard__subscription-waiting" aria-label="در حال دریافت اشتراک"><DashboardSkeleton rows={1}/></div>}
          {role==="Customer"&&overviewState.status==="error"&&
            <p className="argin-dashboard__kpi-note">وضعیت اشتراک در دسترس نیست. <Link href="/account">مشاهده حساب</Link></p>}
          {role==="Customer"&&overview?.subscription&&
            <p className="argin-dashboard__kpi-note">اشتراک فعلی: {overview.subscription.planName} · {fmt(overview.subscription.remainingDays)} روز مانده
            {overview.subscription.remainingInvoices!==null?" · "+fmt(overview.subscription.remainingInvoices)+" صورتحساب باقی‌مانده":""}</p>}
        </DashboardPanel>
      </div>
    </div>
  </div>;
}
