"use client";
import { useEffect,useState } from "react";
type Request={id:string;subscriptionId:string;currentMemoryId:string;proposedMemoryId:string;taxpayerId:string;reason:string;status:number;requestedAtUtc:string};
export default function FiscalMemoryApprovals(){
 const [rows,setRows]=useState<Request[]>([]);const [reasons,setReasons]=useState<Record<string,string>>({});
 const [loading,setLoading]=useState(true);const [busy,setBusy]=useState("");const [error,setError]=useState("");const [notice,setNotice]=useState("");const [tick,setTick]=useState(0);
 useEffect(()=>{let active=true;setLoading(true);fetch("/api/phase12/subscriptions/binding-exceptions/pending",{cache:"no-store"})
  .then(async r=>{if(!r.ok)throw new Error("دریافت درخواست‌های در انتظار انجام نشد.");const v=await r.json();if(active)setRows(Array.isArray(v.requests)?v.requests:[]);})
  .catch(e=>{if(active)setError(e instanceof Error?e.message:"خطای دریافت درخواست‌ها.");})
  .finally(()=>{if(active)setLoading(false);});return()=>{active=false};},[tick]);
 async function decide(item:Request,approve:boolean){const reason=reasons[item.id]?.trim()??"";if(reason.length<10||busy)return;
  setBusy(item.id);setError("");setNotice("");try{const r=await fetch(`/api/phase12/subscriptions/binding-exceptions/${item.id}/review`,{
   method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({approve,decisionReason:reason})});
   if(!r.ok)throw new Error(r.status===409?"درخواست منقضی یا اطلاعات اتصال تغییر کرده است؛ آن را دوباره بررسی کنید.":"ثبت تصمیم انجام نشد.");
   setNotice(approve?"حافظهٔ Production برای همان مودی به پلن متصل شد.":"درخواست تغییر حافظه رد شد.");setTick(v=>v+1);
  }catch(e){setError(e instanceof Error?e.message:"خطای ثبت تصمیم.");}finally{setBusy("");}}
 return <section className="signing-admin-review" dir="rtl" aria-busy={loading||!!busy}>
  <div className="signing-panel-heading"><h1>درخواست‌های تغییر حافظهٔ Production</h1><button className="identity-secondary" disabled={loading} onClick={()=>setTick(v=>v+1)}>بازخوانی</button></div>
  <p>تأیید فقط حافظهٔ متصل به اشتراک را برای همان مودی تغییر می‌دهد؛ مودی و Sandbox تغییر نمی‌کنند.</p>
  {error&&<p role="alert">{error}</p>}{notice&&<p role="status">{notice}</p>}
  {loading?<p role="status">در حال دریافت…</p>:rows.length===0?<p>درخواست در انتظاری وجود ندارد.</p>:
   <div className="signing-admin-review__list">{rows.map(item=><article className="signing-admin-review__item" key={item.id}>
    <strong>اشتراک {item.subscriptionId}</strong><dl><div><dt>مودی</dt><dd dir="ltr">{item.taxpayerId}</dd></div>
     <div><dt>حافظهٔ فعلی</dt><dd dir="ltr">{item.currentMemoryId}</dd></div><div><dt>حافظهٔ پیشنهادی</dt><dd dir="ltr">{item.proposedMemoryId}</dd></div>
     <div><dt>تاریخ درخواست</dt><dd>{new Date(item.requestedAtUtc).toLocaleString("fa-IR")}</dd></div></dl>
    <p>{item.reason}</p><label>دلیل تصمیم<textarea minLength={10} maxLength={1000} value={reasons[item.id]??""} onChange={e=>setReasons(v=>({...v,[item.id]:e.target.value}))}/></label>
    <div className="signing-admin-review__actions"><button className="identity-primary" disabled={busy===item.id||(reasons[item.id]?.trim().length??0)<10} onClick={()=>void decide(item,true)}>تأیید و اعمال</button>
     <button className="identity-secondary" disabled={busy===item.id||(reasons[item.id]?.trim().length??0)<10} onClick={()=>void decide(item,false)}>رد درخواست</button></div>
   </article>)}</div>}
 </section>;
}
