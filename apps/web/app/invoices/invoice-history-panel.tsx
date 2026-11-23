"use client";
import {useEffect,useState} from "react";
type Entry={revision:number;actorUserId:string;actorDisplayName:string;action:string;reason:string;occurredAtUtc:string};
export default function InvoiceHistoryPanel({workspaceId}:{workspaceId:string}){
 const [rows,setRows]=useState<Entry[]>([]);
 const [error,setError]=useState("");
 const [loading,setLoading]=useState(true);
 useEffect(()=>{
  let active=true;setLoading(true);setError("");
  fetch("/api/invoices/workspaces/"+workspaceId+"/history",{cache:"no-store",credentials:"same-origin"})
   .then(async response=>{
    if(!response.ok)throw Error("دریافت تاریخچه مجاز نیست یا موقتاً در دسترس نیست.");
    return response.json() as Promise<Entry[]>;
   }).then(data=>{if(active)setRows(data);})
   .catch(e=>{if(active)setError((e as Error).message);})
   .finally(()=>{if(active)setLoading(false);});
  return ()=>{active=false;};
 },[workspaceId]);
 return <details className="invoice-history-panel" aria-busy={loading}>
   <summary><span>تاریخچه تغییرات پیش‌نویس</span><svg viewBox="0 0 20 20" aria-hidden="true"><path d="m5 7 5 5 5-5"/></svg></summary>
   <div className="invoice-history-panel__content">
   {loading&&<p className="invoice-entry-loading" role="status" aria-live="polite"><span className="argin-loader-spinner" aria-hidden="true"/>در حال دریافت تاریخچه…</p>}
   {error&&<p role="alert" className="form-error">{error}</p>}
   {!loading&&!error&&rows.length===0&&<p className="field-hint">برای این پیش‌نویس هنوز سابقه‌ای در جدول تاریخچه موجود نیست.</p>}
   {rows.length>0&&<div className="console-table-scroll"><table className="console-table">
    <thead><tr><th>نسخه</th><th>عملیات</th><th>تاریخ تغییر (شمسی)</th><th>کاربر ثبت‌کننده</th><th>شرح</th></tr></thead>
    <tbody>{[...rows].sort((a,b)=>Date.parse(b.occurredAtUtc)-Date.parse(a.occurredAtUtc)||b.revision-a.revision).map(item=><tr key={item.revision}>
     <td>{item.revision}</td><td>{item.action==="create"?"ایجاد":"ذخیره تغییرات"}</td>
     <td>{new Intl.DateTimeFormat("fa-IR-u-ca-persian",{timeZone:"Asia/Tehran",year:"numeric",month:"2-digit",day:"2-digit",hour:"2-digit",minute:"2-digit",second:"2-digit",hourCycle:"h23"}).format(new Date(item.occurredAtUtc))}</td>
     <td>{item.actorDisplayName}</td><td>{item.reason}</td>
    </tr>)}</tbody>
   </table></div>}
   <p className="field-hint">این تاریخچه صرفاً ثبت نسخه‌های پیش‌نویس است و به معنای تأیید یا ارسال مالیاتی نیست.</p>
   </div>
  </details>;
}
