"use client";
import {useCallback,useEffect,useState} from "react";
import InvoiceActionToast,{InvoiceActionIcon} from "./invoice-action-toast";
import InvoiceCorrectionReceipt,{type CorrectionReceiptValue} from "./invoice-correction-receipt";
import {fetchWithSessionRefresh} from "../../lib/session-fetch";
type Line={description:string;quantity:number;unitPriceRial:number;discountPerUnitRial:number;vatRatePercent:number|null;officialIdentifierSnapshot:string|null};
type Draft={workspaceId:string;predecessorInvoiceId:string;revision:number;lines:Line[];settlementMethod:number;cashAmountRial:number;creditAmountRial:number};
type Result={decision:number;code:string;draft?:Draft|null;invoiceId?:string|null;status?:string|null;referenceNumber?:string|null;inquiryCode?:string|null};
type Entry={quantity:number;unitPriceRial:number;discountPerUnitRial:number};
class CorrectionActionError extends Error{
 constructor(readonly code:string,readonly result?:Result){
  super(code);
 }
}
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
 const [mistakeEvidence,setMistakeEvidence]=useState("");
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState("");
 const [message,setMessage]=useState("");
 const [stage,setStage]=useState<"items"|"settlement"|"review">("items");
 const [confirmation,setConfirmation]=useState<"send"|"discard"|null>(null);
 const [progress,setProgress]=useState<"saving"|"sending"|"inquiring"|null>(null);
 const [receipt,setReceipt]=useState<CorrectionReceiptValue|null>(null);
 const [submissionLocked,setSubmissionLocked]=useState(false);
 const apply=(v:Draft)=>{
  setDraft(v);setEntries(v.lines.map(x=>({
   quantity:x.quantity,unitPriceRial:x.unitPriceRial,discountPerUnitRial:x.discountPerUnitRial
  })));setSettlement(v.settlementMethod);setCash(v.cashAmountRial);setCredit(v.creditAmountRial);
 };
 const action=useCallback(async(method:"GET"|"POST"|"PUT"|"DELETE",suffix:string,body?:unknown):Promise<Result>=>{
  // Draft requests rejected before authentication can be retried after refresh.
  // Submission retains its existing single-attempt behavior.
  const request=suffix==="correction-draft"?fetchWithSessionRefresh:fetch;
  const response=await request(url+"/"+suffix,{method,cache:"no-store",credentials:"same-origin",
   ...(body?{headers:{"Content-Type":"application/json"},body:JSON.stringify(body)}:{})});
  if(response.status===401)throw Error("نشست ورود شما منقضی شده است. تغییرات فرم حفظ شده‌اند؛ دوباره وارد حساب شوید و سپس ذخیره را تکرار کنید.");
  const result=await response.json() as Result & {error?:string};
  if(!response.ok)throw new CorrectionActionError(String(result.code??result.error??response.status),result);
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
 const persistDraft=async()=>{
  if(!draft)throw Error("پیش‌نویس اصلاحی در دسترس نیست.");
  const result=await action("PUT","correction-draft",{
   expectedRevision:draft.revision,lines:entries,settlementMethod:settlement,
   cashAmountRial:cash,creditAmountRial:credit,
   reductionCorrectsOriginalMistake:mistake,reductionEvidenceReference:mistake?mistakeEvidence:null
  });
  if(!result.draft)throw Error("پاسخ ذخیره پیش‌نویس معتبر نیست؛ وضعیت پیش‌نویس را بررسی کنید.");
  apply(result.draft);
 };
 const save=async()=>{
  if(!draft||busy)return;
  setBusy(true);setError("");setMessage("");
  try{
   await persistDraft();
   setMessage("پیش‌نویس اصلاحی ذخیره شد؛ می‌توانید بعداً ادامه دهید.");
  }catch(e){setError((e as Error).message);}finally{setBusy(false);}
 };
 const discard=async()=>{
  if(busy)return;
  setConfirmation(null);
  setBusy(true);
  try{await action("DELETE","correction-draft");onChanged();onClose();}
  catch(e){setError((e as Error).message);setBusy(false);}
 };
 const send=async()=>{
  if(busy||!draft||submissionLocked)return;
  setConfirmation(null);setProgress("saving");
  setBusy(true);setError("");setMessage("در حال ذخیره تغییرات اصلاحیه…");
  let submissionRequested=false;
  try{
   await persistDraft();
   setMessage("در حال صدور و ارسال صورتحساب اصلاحی…");
   setProgress("sending");
   submissionRequested=true;
   const r=await action("POST","correction-send-and-inquire");
   setReceipt(r);setSubmissionLocked(true);setDraft(null);setMessage("");onChanged();
   if(r.invoiceId&&r.referenceNumber){
    setProgress("inquiring");
    try{
     const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
      encodeURIComponent(r.invoiceId)+"/inquiry-by-reference",{
      method:"POST",cache:"no-store",credentials:"same-origin",signal:AbortSignal.timeout(40000)});
     if(!response.ok)throw Error("inquiry_unavailable");
     const result=await response.json() as {authorityResult?:{status:string}|null;safeCode?:string};
     setReceipt({...r,authorityStatus:result.authorityResult?.status??null,inquiryCode:result.safeCode});
     onChanged();
    }catch{setError("رسید ارسال اصلاحیه ثبت شد، اما استعلام در دسترس نیست. از بخش استعلام، نتیجه آخرین سند زنجیره را بررسی کنید.");}
   }
  }catch(e){
   setMessage("");
   if(e instanceof CorrectionActionError&&e.code==="no_changes_to_correct"){
    setError("تغییری نسبت به صورتحساب اصلی ثبت نشده است. ابتدا مبلغ، تعداد، تخفیف یا روش تسویه را اصلاح کنید؛ صورتحساب اصلاحی ارسال نشده است.");
   }else if(e instanceof CorrectionActionError&&e.result?.invoiceId){
    setReceipt(e.result);setSubmissionLocked(true);setDraft(null);onChanged();
    setError("سند اصلاحی ایجاد شد، اما نتیجه ارسال نیازمند بررسی است؛ بدون استعلام، درخواست را تکرار نکنید.");
   }else if(!submissionRequested){
    setError("ذخیره تغییرات انجام نشد و اصلاحیه ارسال نشد. "+(e as Error).message);
   }else{
    setSubmissionLocked(true);onChanged();
    setError("نتیجه ارسال نیازمند بررسی است؛ بدون استعلام، درخواست را تکرار نکنید. "+(e as Error).message);
   }
  }finally{setProgress(null);setBusy(false);}
 };
 const roughTotal=entries.reduce((sum,x,i)=>{
  const rate=draft?.lines[i]?.vatRatePercent??0;
  const net=x.quantity*(x.unitPriceRial-x.discountPerUnitRial);
  return sum+net+Math.round(net*rate/100);
 },0);
 return <div className="invoice-register__overlay" role="presentation">
  <div className="invoice-register__detail-modal invoice-correction-modal" role="dialog" aria-modal="true"
   aria-label="ویرایش صورتحساب اصلاحی" dir="rtl">
   <header className="invoice-register__dialog-header">
    <h2>اصلاح صورتحساب</h2>
    <button type="button" className="identity-secondary invoice-action-button" aria-label="بستن اصلاح صورتحساب" onClick={onClose} disabled={busy}><InvoiceActionIcon name="close"/></button>
   </header>
   <p className="field-hint">نسخه اصلی ثابت می‌ماند؛ سند اصلاحی شناسه مالیاتی جدید خواهد داشت.</p>
   {error&&<p role="alert" className="form-error">{error}</p>}
   {message&&!progress&&<p role="status">{message}</p>}
   {receipt&&<InvoiceCorrectionReceipt receipt={receipt}/>}
   {progress&&<section className="invoice-send-progress" role="status" aria-live="polite" aria-busy="true">
    <span className="argin-loader-spinner" aria-hidden="true"/>
    <div className="invoice-send-progress__body"><strong>{progress==="saving"?"در حال ذخیره تغییرات اصلاحیه…":progress==="sending"?"در حال صدور و ارسال صورتحساب اصلاحی…":"در حال استعلام صورتحساب اصلاحی…"}</strong>
     <p>{progress==="inquiring"?"کد رهگیری دریافت شد؛ نتیجه سازمان در حال بررسی است.":"لطفاً تا پایان عملیات منتظر بمانید و پنجره را باز نگه دارید."}</p>
     <ol className="invoice-send-progress__steps">
      <li aria-current={progress==="saving"?"step":undefined} data-complete={progress!=="saving"}>ذخیره تغییرات</li>
      <li aria-current={progress==="sending"?"step":undefined} data-complete={progress==="inquiring"}>صدور و ارسال</li>
      <li aria-current={progress==="inquiring"?"step":undefined}>استعلام سازمان</li>
     </ol>
    </div>
   </section>}
   {confirmation&&<InvoiceActionToast
    title={confirmation==="send"?"ارسال صورتحساب اصلاحی":"کنار گذاشتن پیش‌نویس اصلاحی"}
    description={confirmation==="send"?"تغییرات فعلی ذخیره و با شناسه مالیاتی جدید به Sandbox ارسال می‌شوند. ارسال را تأیید می‌کنید؟":"تغییرات این پیش‌نویس کنار گذاشته می‌شوند؛ صورتحساب ثبت‌شده حفظ می‌شود."}
    confirmLabel={confirmation==="send"?"تأیید و ارسال":"کنار گذاشتن پیش‌نویس"}
    onConfirm={()=>confirmation==="send"?send():discard()} onCancel={()=>setConfirmation(null)}/> }
   {!draft&&!error&&!message&&!receipt&&!progress&&<p role="status">در حال دریافت پیش‌نویس…</p>}
   {draft&&!progress&&<div className="invoice-correction-editor">
    <div style={{display:"flex",gap:8,flexWrap:"wrap"}}>
     {([{id:"items",text:"اقلام و قیمت‌ها"},{id:"settlement",text:"تسویه"},{id:"review",text:"مرور تغییرات"}] as const)
      .map(t=><button key={t.id} type="button" className="identity-secondary invoice-action-button"
       onClick={()=>setStage(t.id)} aria-pressed={stage===t.id}><InvoiceActionIcon name={t.id==="items"?"items":t.id==="settlement"?"settlement":"review"}/>{t.text}</button>)}
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
    <label><input type="checkbox" disabled={busy} checked={mistake} onChange={e=>setMistake(e.target.checked)}/>
     کاهش مقدار صرفاً برای اصلاح اشتباه ثبت اولیه است (نه برگشت واقعی کالا)
    </label>
    {mistake&&<label>مرجع مستند اصلاح اشتباه اولیه
     <input type="text" maxLength={256} disabled={busy} value={mistakeEvidence}
      onChange={e=>setMistakeEvidence(e.target.value)}
      placeholder="شماره یا کد مستند مربوط به اشتباه اولیه"/>
    </label>}
    </div>}
    {stage==="settlement"&&<div style={{display:"grid",gap:10}}>
     <label>روش تسویه
      <select value={settlement} onChange={e=>setSettlement(Number(e.target.value))} disabled={busy}>
       <option value={1}>نقدی</option><option value={2}>نسیه</option><option value={3}>نقدی و نسیه</option>
      </select>
     </label>
     <label>مبلغ نقدی (ریال)<input type="number" min="0" disabled={busy} value={cash}
      onChange={e=>setCash(Number(e.target.value))}/></label>
     <label>مبلغ نسیه (ریال)<input type="number" min="0" disabled={busy} value={credit}
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
    <div className="invoice-correction-actions">
     <button type="button" className="identity-secondary invoice-action-button" aria-label="ذخیره پیش‌نویس" disabled={busy||submissionLocked} onClick={()=>void save()}><InvoiceActionIcon name="save"/>ذخیره پیش‌نویس</button>
     <button type="button" className="identity-secondary invoice-action-button" aria-label="انصراف از اصلاح" disabled={busy||submissionLocked} onClick={()=>setConfirmation("discard")}><InvoiceActionIcon name="discard"/>انصراف از اصلاح</button>
     <button type="button" className="identity-primary invoice-action-button" aria-label="ذخیره تغییرات و ارسال اصلاحیه" disabled={busy||submissionLocked} onClick={()=>setConfirmation("send")}><InvoiceActionIcon name="send"/>ارسال اصلاحیه</button>
    </div>
   </div>}
  </div>
 </div>;
}
