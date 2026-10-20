"use client";
import {useEffect,useState} from "react";

type Line={description:string;quantity:number;unitPriceRial:number;discountPerUnitRial:number;vatRatePercent:number|null};
type Draft={id:string;revision:number;taxpayerId:string;lastCompletedStep:number;buyerDisplayName:string|null;
  issueDateUtc:string;lines:Line[];financialSummary:{grossRial:number;discountRial:number;previewVatRial:number;previewGrandTotalRial:number};
  internalReviewState:string|null;internalReviewReason:string|null};
type Finding={key:string;severity:string;message:string;fieldPath:string};
type Validation={revision:number;findings:Finding[];isSubmissionAuthorized:boolean;edition:string};
async function fetchJson<T>(path:string,init?:RequestInit):Promise<T>{
 const res=await fetch(path,{...init,cache:"no-store",credentials:"same-origin"});
 if(!res.ok)throw Error(res.status===409?"نسخه پیش‌نویس تغییر کرده است؛ دوباره بازخوانی کنید.":res.status===403?
  "برای تأیید یا بازگرداندن این پیش‌نویس مجوز ندارید.":"عملیات انجام نشد (HTTP "+res.status+").");
 return res.json() as Promise<T>;
}
export default function InternalReviewPanel({id,onClose,onChanged}:{
  id:string;onClose:()=>void;onChanged:()=>void
}){
 const [draft,setDraft]=useState<Draft|null>(null);
 const [validation,setValidation]=useState<Validation|null>(null);
 const [reason,setReason]=useState("");
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState("");
 async function load(){
  const d=await fetchJson<Draft>("/api/invoices/workspaces/"+id);
  const v=await fetchJson<Validation>("/api/invoices/workspaces/"+id+"/validation");
  setDraft(d);setValidation(v);
 }
 useEffect(()=>{let alive=true;setBusy(true);Promise.all([
   fetchJson<Draft>("/api/invoices/workspaces/"+id),
   fetchJson<Validation>("/api/invoices/workspaces/"+id+"/validation")
 ]).then(([d,v])=>{if(alive){setDraft(d);setValidation(v);}})
 .catch(e=>{if(alive)setError((e as Error).message);})
 .finally(()=>{if(alive)setBusy(false);});
 return ()=>{alive=false;};},[id]);
 async function act(action:"submit"|"approve"|"return"){
  if(!draft || busy)return;
  if(action==="return" && !reason.trim()){setError("علت بازگرداندن برای اصلاح الزامی است.");return;}
  setBusy(true);setError("");
  try {
   await fetchJson<Draft>("/api/invoices/workspaces/"+id+"/review",{
    method:"POST",headers:{"Content-Type":"application/json"},
    body:JSON.stringify({expectedRevision:draft.revision,action,reason:action==="return"?reason.trim():null})
   });
   await load();onChanged();setReason("");
  }catch(e){setError((e as Error).message);}
  finally{setBusy(false);}
 }
 const state=draft?.internalReviewState??"Draft";
 const failures=validation?.findings.filter(x=>x.severity==="error")??[];
 const format=(v:number)=>v.toLocaleString("fa-IR",{maximumFractionDigits:2});
 return <div className="invoice-register__overlay">
  <div className="invoice-register__detail-modal" role="dialog" aria-modal="true"
   aria-label="بررسی و تأیید داخلی پیش‌نویس" dir="rtl">
   <div className="invoice-register__dialog-header">
    <h2>بررسی و تأیید داخلی پیش‌نویس</h2>
    <button type="button" className="identity-secondary" onClick={onClose}>× بستن</button>
   </div>
   <div className="invoice-register__dialog-body">
    <p className="field-hint">این بررسی صرفاً داخلی است و به معنی صدور، امضا یا پذیرش سازمان امور مالیاتی نیست.</p>
    {busy && <p role="status">در حال دریافت یا ثبت نتیجه بررسی…</p>}
    {error && <p role="alert" className="form-error">{error}</p>}
    {draft && <section>
      <p>نسخه: <bdi>{draft.revision}</bdi> · وضعیت: <bdi>{state==="Draft"?"پیش‌نویس":state==="InReview"?"در انتظار بررسی":state==="Approved"?"تأیید داخلی":"بازگردانده‌شده"}</bdi></p>
      <p>تاریخ صدور: <bdi dir="ltr">{draft.issueDateUtc}</bdi> · خریدار: {draft.buyerDisplayName??"ثبت نشده"}</p>
      <table className="console-table"><thead><tr><th>شرح</th><th>مقدار</th><th>قیمت واحد</th><th>تخفیف</th><th>VAT</th></tr></thead>
      <tbody>{draft.lines.map((line,i)=><tr key={i}><td>{line.description}</td><td>{format(line.quantity)}</td><td>{format(line.unitPriceRial)}</td><td>{format(line.discountPerUnitRial)}</td><td>{line.vatRatePercent??"نامشخص"}</td></tr>)}</tbody></table>
      <p>ناخالص: {format(draft.financialSummary.grossRial)} ریال · تخفیف: {format(draft.financialSummary.discountRial)} ریال</p>
      <p>VAT: {format(draft.financialSummary.previewVatRial)} ریال · <strong>جمع: {format(draft.financialSummary.previewGrandTotalRial)} ریال</strong></p>
      {draft.internalReviewReason && <p role="status">علت بازگشت: {draft.internalReviewReason}</p>}
      <h3>گزارش پیش‌اعتبارسنجی {validation?.edition}</h3>
      <p>{failures.length} خطا · {validation?.findings.filter(x=>x.severity==="warning").length??0} هشدار</p>
      <ul>{validation?.findings.map((v,i)=><li key={i}><strong>{v.severity==="error"?"خطا":"هشدار"}:</strong> {v.message}</li>)}</ul>
      {state==="InReview" && <>
       <label htmlFor="internal-review-reason">علت بازگرداندن برای اصلاح</label>
       <textarea id="internal-review-reason" maxLength={1000} rows={2} value={reason} onChange={e=>setReason(e.target.value)}/>
      </>}
      <div className="invoice-register__actions">
       {(state==="Draft"||state==="ChangesRequested")&&<button type="button" className="identity-primary"
        disabled={busy||draft.lastCompletedStep!==2||failures.length>0}
        onClick={()=>void act("submit")}>ارسال برای بازبینی داخلی</button>}
       {state==="InReview"&&<>
        <button type="button" disabled={busy||failures.length>0} className="identity-primary" onClick={()=>void act("approve")}>تأیید داخلی</button>
        <button type="button" disabled={busy||!reason.trim()} className="identity-secondary" onClick={()=>void act("return")}>بازگرداندن برای اصلاح</button>
       </>}
      </div>
     </section>}
   </div>
  </div>
 </div>;
}
