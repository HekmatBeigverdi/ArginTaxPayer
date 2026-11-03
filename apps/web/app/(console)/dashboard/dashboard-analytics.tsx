"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

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
export default function DashboardAnalytics({role}:{role:Role}){
  const [period,setPeriod]=useState<Period>("30d");
  const [version,retry]=useState(0);
  const [state,setState]=useState<RemoteState>("loading");
  const [overview,setOverview]=useState<Overview|null>(null);
  const [trend,setTrend]=useState<Trends|null>(null);
  const [finance,setFinance]=useState<Finance|null>(null);
  const [attention,setAttention]=useState<AttentionItem[]>([]);
  const admin=role!=="Customer";
  useEffect(()=>{
    const controller=new AbortController();
    // Clear previous-period data immediately to avoid stale financial figures.
    setState("loading");setOverview(null);setTrend(null);setFinance(null);setAttention([]);
    const tasks=[query("overview",period,controller.signal),query("trends",period,controller.signal),
      query("attention",period,controller.signal)];
    if(admin)tasks.push(query("finance",period,controller.signal));
    Promise.all(tasks).then(([o,t,a,f])=>{
      if(controller.signal.aborted)return;
      const next=o as Overview;
      if(next.schemaVersion!==1||next.role!==role||!next.invoiceStatus||!Array.isArray((t as Trends).buckets)
        ||!Array.isArray((a as AttentionResponse).items)||(admin&&(!(f as Finance)?.currency|| (f as Finance).role!==role)))throw new Error("invalid_dashboard_response");
      setOverview(next);setTrend(t as Trends);setAttention((a as AttentionResponse).items);
      setFinance(admin?f as Finance:null);setState("ready");
    }).catch(()=>{if(!controller.signal.aborted)setState("error");});
    return()=>controller.abort();
  },[role,admin,period,version]);
  const cards=state==="ready"&&overview?(admin&&finance?[
    {label:"فروش اشتراک",value:money(finance.salesGrossIrr),note:"مبلغ سفارش‌های صادرشده در بازه"},
    {label:"دریافتی تأییدشده",value:money(finance.collectionsInPeriodIrr),note:"دریافت‌های ثبت‌شده در بازه",tone:"success" as const},
    {label:"مطالبات فروش",value:money(finance.salesOutstandingIrr),note:"مانده سفارش‌های همین بازه"},
    {label:role==="Level1"?"سهم مدیر سطح ۱":"سهم همکاری شما",value:money(role==="Level1"?(finance.rootEarnedShareIrr??0):finance.partnerEarnedShareIrr),note:"سهم ثبت‌شده در بازه"}
  ]:[
    {label:"صورتحساب‌ها",value:fmt(overview.invoiceStatus.logicalInvoiceCount),note:"زنجیره‌های یکتا در بازه"},
    {label:"تأییدشده توسط سازمان",value:fmt(overview.invoiceStatus.accepted+overview.invoiceStatus.acceptedCancellation),note:"فقط وضعیت قطعی",tone:"success" as const},
    {label:"نامشخص / نیازمند بررسی",value:fmt(overview.invoiceStatus.unknown+overview.invoiceStatus.attention+overview.invoiceStatus.rejected),note:"نیازمند پیگیری؛ ارسال مجدد خودکار ندارد",tone:"warning" as const},
    {label:"پیش‌نویس‌های فعال",value:fmt(overview.invoiceDrafts),note:"پیش‌نویس‌های تبدیل‌نشده در بازه"}
  ]):[];
  return <div className="argin-dashboard" dir="rtl">
    <header className="argin-dashboard__header">
      <div><span className="argin-dashboard__eyebrow">پیشخوان تحلیلی آرگین</span>
        <h1 className="argin-dashboard__title">{role==="Level1"?"نمای مدیریتی کل سامانه":role==="Level2"?"عملکرد مشتریان زیرمجموعه":"وضعیت صورتحساب‌های شما"}</h1>
        <p className="argin-dashboard__subtitle">گزارش‌های واقعی متناسب با دسترسی شما؛ {state==="ready"&&overview?"آخرین دریافت: "+new Intl.DateTimeFormat("fa-IR",{dateStyle:"short",timeStyle:"short",timeZone:"Asia/Tehran"}).format(new Date(overview.asOfUtc)):"در حال بررسی وضعیت گزارش‌ها"}</p>
      </div><DashboardPeriodFilter period={period} setPeriod={setPeriod}/>
    </header>
    {state!=="ready"?<DashboardState kind={state} onRetry={()=>retry(n=>n+1)}/>:<>
      <section aria-label="شاخص‌های کلیدی" className="argin-dashboard__kpis">{cards.map(card=><DashboardKpiCard key={card.label} {...card}/>)}</section>
      <div className="argin-dashboard__charts">
        <DashboardPanel title="روند صورتحساب‌ها"><DashboardTrendChart data={trend?.buckets??[]}/></DashboardPanel>
        <DashboardPanel title="ترکیب آخرین وضعیت صورتحساب‌ها"><DashboardStatusChart status={overview!.invoiceStatus}/></DashboardPanel>
      </div>
      <div className="argin-dashboard__footer-grid">
        <DashboardPanel title="موارد نیازمند پیگیری">
          {!attention.length?<DashboardState kind="empty"/>:<ul className="argin-dashboard__attention">{attention.map(item=>
            <li key={item.invoiceId}><span className="argin-dashboard__status" data-tone="warning">{statusLabels[item.status]??"نیازمند بررسی"}</span><span>{dates(item.issuedAtUtc.slice(0,10))}</span><Link href="/invoices">مشاهده صورتحساب</Link></li>)}</ul>}
        </DashboardPanel>
        <DashboardPanel title="دسترسی سریع">
          <nav className="argin-dashboard__quick" aria-label="اقدامات پرکاربرد"><Link href="/invoices">صورتحساب‌ها</Link>
            <Link href="/parties">خریداران و مؤدیان</Link>
            <Link href={admin?"/subscriptions":"/account"}>{admin?"اشتراک‌ها و فروش":"اشتراک من"}</Link>
          </nav>
          {role==="Customer"&&overview?.subscription&&<p className="argin-dashboard__kpi-note">اشتراک فعلی: {overview.subscription.planName} · {fmt(overview.subscription.remainingDays)} روز مانده</p>}
        </DashboardPanel>
      </div>
    </>}
  </div>;
}
