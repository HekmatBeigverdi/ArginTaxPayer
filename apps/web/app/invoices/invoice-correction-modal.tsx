"use client";
import {useCallback,useEffect,useState} from "react";
type Line={description:string;quantity:number;unitPriceRial:number;discountPerUnitRial:number;vatRatePercent:number|null;officialIdentifierSnapshot:string|null};
type Draft={workspaceId:string;predecessorInvoiceId:string;revision:number;lines:Line[];settlementMethod:number;cashAmountRial:number;creditAmountRial:number};
type Result={decision:number;code:string;draft?:Draft|null;invoiceId?:string|null;status?:string|null;referenceNumber?:string|null;inquiryCode?:string|null};
type Entry={quantity:number;unitPriceRial:number;discountPerUnitRial:number};
const fa=(v:number)=>v.toLocaleString("fa-IR",{maximumFractionDigits:2});
export default function InvoiceCorrectionModal({taxpayerId,invoiceId,onClose,onChanged}:{
 taxpayerId:string;invoiceId:string;onClose:()=>void;onChanged:()=>void}){
 const url="/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId);
 const [draft,setDraft]=useState<Draft|null>(null);
 const [entries,setEntries]=useState<Entry[]>([]);
 const [settlement,setSettlement]=useState(1);
 const [cash,setCash]=useState(0);
 const [credit,setCredit]=useState(0);
 const [mistake,setMistake]=useState(false);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState("");
 const [message,setMessage]=useState("");
 const [stage,setStage]=useState<"items"|"settlement"|"review">("items");
 const apply=(v:Draft)=>{
  setDraft(v);setEntries(v.lines.map(x=>({
   quantity:x.quantity,unitPriceRial:x.unitPriceRial,discountPerUnitRial:x.discountPerUnitRial
  })));setSettlement(v.settlementMethod);setCash(v.cashAmountRial);setCredit(v.creditAmountRial);
 };
 const action=useCallback(async(method:"GET"|"POST"|"PUT"|"DELETE",suffix:string,body?:unknown):Promise<Result>=>{
  const response=await fetch(url+"/"+suffix,{method,cache:"no-store",credentials:"same-origin",
   ...(body?{headers:{"Content-Type":"application/json"},body:JSON.stringify(body)}:{})});
  const result=await response.json() as Result & {error?:string};
  if(!response.ok)throw Error(String(result.code??result.error??response.status));
  return result;
 },[url]);
 useEffect(()=>{
  let live=true;
  void action("POST","correction-draft").then(r=>{if(live&&r.draft)apply(r.draft);})
   .catch(e=>{if(live)setError("باز کردن پیش‌نویس اصلاحی ناموفق بود: "+(e as Error).message);});
  return()=>{live=false;};
 },[action]);
 const change=(i:number,key:keyof Entry,value:number)=>{
  setEntries(prev=>prev.map((e,index)=>index===i?{...e,[key]:value}:e));
 };
 const save=async()=>{
  if(!draft||busy)return;
  setBusy(true);setError("");setMessage("");
  try{
   const result=await action("PUT","correction-draft",{
    expectedRevision:draft.revision,lines:entries,settlementMethod:settlement,
    cashAmountRial:cash,creditAmountRial:credit,
    reductionCorrectsOriginalMistake:mistake
   });
   if(result.draft)apply(result.draft);
   setMessage("پیش‌نویس اصلاحی ذخیره شد؛ می‌توانید بعداً ادامه دهید.");
  }catch(e){setError((e as Error).message);}finally{setBusy(false);}
 };
 const discard=async()=>{
  if(busy||!window.confirm("پیش‌نویس اصلاحی کنار گذاشته شود؟ صورتحساب ثبت‌شده تغییر نمی‌کند."))return;
  setBusy(true);
  try{await action("DELETE","correction-draft");onChanged();onClose();}
  catch(e){setError((e as Error).message);setBusy(false);}
 };
 const send=async()=>{
  if(busy||!draft)return;
  if(!window.confirm("پیش‌نویس ذخیره‌شده را به‌عنوان صورتحساب اصلاحی مستقل به Sandbox ارسال می‌کنید؟"))return;
  setBusy(true);setError("");setMessage("در حال صدور و ارسال صورتحساب اصلاحی…");
  try{
   const r=await action("POST","correction-send-and-inquire");
   setMessage("شناسه داخلی سند اصلاحی: "+String(r.invoiceId??"—")+
    "؛ وضعیت ارسال: "+String(r.status??"نامشخص")+
    "؛ شماره رهگیری: "+String(r.referenceNumber??"در انتظار بررسی")+
    "؛ نتیجه استعلام: "+String(r.inquiryCode??"نامشخص"));
   setDraft(null);onChanged();
  }catch(e){
   setError("نتیجه ارسال نیازمند بررسی است؛ بدون استعلام، درخواست را تکرار نکنید. "+(e as Error).message);
  }finally{setBusy(false);}
 };
 const roughTotal=entries.reduce((sum,x,i)=>{
  const rate=draft?.lines[i]?.vatRatePercent??0;
  const net=x.quantity*(x.unitPriceRial-x.discountPerUnitRial);
  return sum+net+Math.round(net*rate/100);
 },0);
 return <div className="invoice-register__overlay" role="presentation">
  <div className="invoice-register__detail-modal" role="dialog" aria-modal="true"
   aria-label="ویرایش صورتحساب اصلاحی" dir="rtl">
   <header className="invoice-register__dialog-header">
    <h2>اصلاح صورتحساب</h2>
    <button type="button" className="identity-secondary" onClick={onClose} disabled={busy}>×</button>
   </header>
   <p className="field-hint">نسخه اصلی ثابت می‌ماند؛ سند اصلاحی شناسه مالیاتی جدید خواهد داشت.</p>
   {error&&<p role="alert" className="form-error">{error}</p>}
   {message&&<p role="status">{message}</p>}
   {!draft&&!error&&!message&&<p role="status">در حال دریافت پیش‌نویس…</p>}
   {draft&&<div style={{display:"grid",gap:12,maxHeight:"68dvh",overflow:"auto",padding:8}}>
    <div style={{display:"flex",gap:8,flexWrap:"wrap"}}>
     {([{id:"items",text:"اقلام و قیمت‌ها"},{id:"settlement",text:"تسویه"},{id:"review",text:"مرور تغییرات"}] as const)
      .map(t=><button key={t.id} type="button" className="identity-secondary"
       onClick={()=>setStage(t.id)} aria-pressed={stage===t.id}>{t.text}</button>)}
    </div>
    {stage==="items"&&<div className="table-scroll"><table className="console-table">
     <thead><tr><th>شرح</th><th>شناسه رسمی</th><th>تعداد</th><th>قیمت واحد (ریال)</th><th>تخفیف واحد</th><th>نرخ مالیات</th></tr></thead>
     <tbody>{draft.lines.map((line,i)=><tr key={i}>
      <td>{line.description}</td><td><bdi dir="ltr">{line.officialIdentifierSnapshot??"—"}</bdi></td>
      {(["quantity","unitPriceRial","discountPerUnitRial"] as const).map(k=><td key={k}>
       <input type="number" min={k==="discountPerUnitRial"?0:0.0001} step="any"
        style={{width:110}} disabled={busy} value={entries[i]?.[k]??0}
        onChange={e=>change(i,k,Number(e.target.value))}/>
      </td>)}
      <td>{line.vatRatePercent??"—"}٪</td>
     </tr>)}</tbody>
    </table>
    <label><input type="checkbox" checked={mistake} onChange={e=>setMistake(e.target.checked)}/>
     کاهش مقدار صرفاً برای اصلاح اشتباه ثبت اولیه است (نه برگشت واقعی کالا)
    </label></div>}
    {stage==="settlement"&&<div style={{display:"grid",gap:10}}>
     <label>روش تسویه
      <select value={settlement} onChange={e=>setSettlement(Number(e.target.value))} disabled={busy}>
       <option value={1}>نقدی</option><option value={2}>نسیه</option><option value={3}>نقدی و نسیه</option>
      </select>
     </label>
     <label>مبلغ نقدی (ریال)<input type="number" min="0" value={cash}
      onChange={e=>setCash(Number(e.target.value))}/></label>
     <label>مبلغ نسیه (ریال)<input type="number" min="0" value={credit}
      onChange={e=>setCredit(Number(e.target.value))}/></label>
     <p>مجموع تقریبی اقلام با مالیات: {fa(roughTotal)} ریال؛ مقادیر نهایی در Backend با قواعد رسمی محاسبه می‌شوند.</p>
    </div>}
    {stage==="review"&&<div>
     <h3>مرور اصلاحات</h3>
     <table className="console-table"><thead><tr><th>قلم</th><th>فی قبلی</th><th>فی جدید</th><th>تعداد قبلی</th><th>تعداد جدید</th></tr></thead>
      <tbody>{draft.lines.map((line,i)=><tr key={i}><td>{line.description}</td>
       <td>{fa(line.unitPriceRial)}</td><td>{fa(entries[i]?.unitPriceRial??0)}</td>
       <td>{fa(line.quantity)}</td><td>{fa(entries[i]?.quantity??0)}</td></tr>)}</tbody>
     </table>
    </div>}
    <div style={{display:"flex",gap:8,flexWrap:"wrap"}}>
     <button type="button" className="identity-secondary" disabled={busy} onClick={()=>void save()}>ذخیره پیش‌نویس</button>
     <button type="button" className="identity-secondary" disabled={busy} onClick={()=>void discard()}>انصراف از اصلاح</button>
     <button type="button" className="identity-primary" disabled={busy} onClick={()=>void send()}>ارسال پیش‌نویس ذخیره‌شده</button>
    </div>
   </div>}
  </div>
 </div>;
}
