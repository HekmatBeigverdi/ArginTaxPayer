"use client";

import Link from "next/link";
import {useEffect,useState} from "react";

type Role="Level1"|"Level2"|"Customer";
type AttentionItem={invoiceId:string;taxpayerId:string;status:string;issuedAtUtc:string;link:string};
type AttentionStatus="loading"|"ready"|"error";
type Watch={schemaVersion:number;role:"Level1"|"Level2";expiringWithinSevenDays:number;expiringWithinThirtyDays:number;exhaustedQuantityPlans:number;items:{subscriptionId:string;status:string;remainingDays:number;expiresAtUtc:string}[]};
type Props={role:Role;attentionStatus:AttentionStatus;attention:AttentionItem[];onRetry:()=>void};
const n=(value:number)=>new Intl.NumberFormat("fa-IR").format(value);
const day=(s:string)=>new Intl.DateTimeFormat("fa-IR-u-ca-persian",{dateStyle:"medium",timeZone:"Asia/Tehran"}).format(new Date(s));
const labels:Record<string,string>={rejected:"ردشده",unknown:"نتیجه نامشخص",attention:"نیازمند بررسی"};
export default function DashboardActionCenter({role,attentionStatus,attention,onRetry}:Props){
  const admin=role!=="Customer";
  const [watchState,setWatchState]=useState<"loading"|"ready"|"error">("loading");
  const [watch,setWatch]=useState<Watch|null>(null);
  const [retryCount,setRetryCount]=useState(0);
  useEffect(()=>{
    if(!admin)return;
    const controller=new AbortController();
    setWatchState("loading");setWatch(null);
    fetch("/api/dashboard/subscriptions-watch",{cache:"no-store",signal:controller.signal})
      .then(async response=>{
        if(!response.ok)throw new Error("watch_unavailable");
        const data=await response.json() as Watch;
        if(data.schemaVersion!==1||data.role!==role||!Array.isArray(data.items)||
          !Number.isSafeInteger(data.expiringWithinSevenDays)||!Number.isSafeInteger(data.exhaustedQuantityPlans))
          throw new Error("invalid_watch");
        if(!controller.signal.aborted){setWatch(data);setWatchState("ready");}
      }).catch(()=>{if(!controller.signal.aborted){setWatch(null);setWatchState("error");}});
    return()=>controller.abort();
  },[role,admin,retryCount]);

  const sortedInvoices=[...attention].sort((a,b)=>{
    const prio=(s:string)=>s==="rejected"?0:s==="unknown"?1:2;
    return prio(a.status)-prio(b.status)||b.issuedAtUtc.localeCompare(a.issuedAtUtc);
  });
  const tasks=[
    ...sortedInvoices.map(i=>({id:"invoice-"+i.invoiceId,title:labels[i.status]??"صورتحساب نیازمند بررسی",
      text:"صورتحساب · "+day(i.issuedAtUtc),priority:i.status==="rejected"?"urgent" as const:"review" as const,
      href:"/invoices",action:"بررسی صورتحساب"})),
    ...(admin&&watch?watch.items.map(i=>({
      id:"subscription-"+i.subscriptionId,
      title:i.status==="quota_exhausted"?"سهمیه صورتحساب به پایان رسیده":i.status==="urgent"?"اشتراک در آستانه انقضا":"تمدید اشتراک نزدیک است",
      text:"اشتراک · "+day(i.expiresAtUtc)+" · "+n(i.remainingDays)+" روز مانده",
      priority:i.status==="quota_exhausted"||i.status==="urgent"?"urgent" as const:"review" as const,
      href:"/subscriptions",action:"مدیریت اشتراک"})):[])
  ].sort((a,b)=>(a.priority==="urgent"?0:1)-(b.priority==="urgent"?0:1));

  const loading=attentionStatus==="loading"||(admin&&watchState==="loading");
  const errors=attentionStatus==="error"||(admin&&watchState==="error");
  return <section className="argin-dashboard__card argin-dashboard__action-center" aria-label="مرکز پیگیری اقدامات">
    <div className="argin-dashboard__action-header">
      <div><span className="argin-dashboard__eyebrow">اولویت‌های عملیاتی</span>
        <h2 className="argin-dashboard__section-title">مرکز پیگیری و اقدامات ضروری</h2>
        <p className="argin-dashboard__kpi-note">نمایش موارد خواندنی و قابل پیگیری در محدوده دسترسی شما؛ هیچ اقدامی خودکار اجرا نمی‌شود.</p>
      </div>
      <div className="argin-dashboard__action-count" aria-label="موارد قابل نمایش">{loading?"در حال بررسی":n(tasks.length)+" مورد"}</div>
    </div>
    {loading&&<div className="argin-dashboard__action-pending" role="status" aria-busy="true" aria-label="در حال بررسی موارد مهم">
      <svg viewBox="0 0 330 52" width="100%" aria-hidden="true">
        <rect x="8" y="9" rx="7" width="170" height="12" fill="#d6e7e5" className="argin-dashboard__skeleton-bar"/>
        <rect x="8" y="30" rx="7" width="266" height="10" fill="#e6eff0" className="argin-dashboard__skeleton-bar"/>
      </svg>
    </div>}
    {errors&&<div className="argin-dashboard__action-alert" role="alert">
      بخشی از اطلاعات دریافت نشد؛ تعداد موارد ممکن است کامل نباشد.
      {attentionStatus==="error"&&<button type="button" onClick={onRetry}>تلاش مجدد برای صورتحساب‌ها</button>}
      {admin&&watchState==="error"&&<button type="button" onClick={()=>setRetryCount(v=>v+1)}>تلاش مجدد برای اشتراک‌ها</button>}
    </div>}
    {tasks.length>0?<ul className="argin-dashboard__action-list">
      {tasks.slice(0,5).map(task=><li key={task.id}>
        <span className="argin-dashboard__action-marker" data-priority={task.priority} aria-hidden="true"/>
        <div className="argin-dashboard__action-info"><strong>{task.title}</strong><small>{task.text}</small></div>
        <Link href={task.href}>{task.action}</Link>
      </li>)}
    </ul>:!loading&&!errors?<div className="argin-dashboard__action-clear" role="status">
      مورد فوری در گزارش‌های دریافت‌شده یافت نشد.
    </div>:null}
    <div className="argin-dashboard__action-bottom">
      <Link href="/invoices">همه صورتحساب‌ها</Link>
      {admin&&<Link href="/subscriptions">مدیریت اشتراک‌ها</Link>}
      {tasks.length>5&&<span>۵ مورد از {n(tasks.length)} مورد دریافت‌شده نمایش داده شده است.</span>}
    </div>
  </section>;
}
