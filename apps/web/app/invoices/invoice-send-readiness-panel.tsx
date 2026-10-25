"use client";
import {useCallback,useEffect,useState} from "react";

type Report={environment:"Sandbox"|"Unverified";invoiceState:"Draft"|"Validated"|"Finalized";
 hasImmutableSnapshot:boolean;invoiceRevision:number;sandboxFiscalMemoryId:string|null;
 readyForLiveSend:false;submissionEnabled:false;
 signedPacketAvailable:boolean;fiscalAccepted:false;blockingCodes:string[]};
type Review={state:"Draft"|"InReview"|"ChangesRequested"|"Approved";revision:number};
const labels:Record<string,string>={
 "invoice.snapshot.invalid":"ساختار نسخه ذخیره‌شده قابل تأیید نیست",
 "invoice.snapshot.not_internal_promoted":"نسخه ثابت داخلی هنوز ایجاد یا شناسایی نشده است",
 "invoice.sandbox.memory_not_bound":"حافظه Sandbox معتبر به این نسخه متصل نیست",
 "invoice.lifecycle.validation_required":"اعتبارسنجی داخلی صورتحساب هنوز تکمیل نشده است",
 "invoice.review.approval_required":"تأیید بازبینی صورتحساب هنوز ثبت نشده است",
 "invoice.finalization.not_finalized":"فرایند قطعی‌سازی قانونی صورتحساب کامل نشده است",
 "invoice.rules.not_submission_verified":"قواعد قانونی نسخه ارسال تأیید نهایی نشده‌اند",
 "invoice.fiscal_identifier.not_issued":"شناسه مالیاتی صورتحساب هنوز تولید نشده است",
 "invoice.serial.durable_reservation_required":"سریال یکتای صورتحساب باید به‌صورت تراکنشی رزرو شود",
 "invoice.sandbox.certificate_chain_not_verified":"زنجیره گواهی دیجیتال مربوط به کلید Sandbox تأیید نشده است",
 "invoice.v2.protocol_not_verified_for_sandbox":"پروتکل و Endpoint نسخه جدید SDK برای این محیط تأیید نشده است",
 "invoice.legacy.transport_not_registered":"مسیر ارسال قدیمی Sandbox هنوز در Backend ثبت نشده است",
 "invoice.signed_packet.not_prepared":"بسته امضاشده قابل ارسال وجود ندارد",
 "invoice.sandbox_outbox.not_connected":"Outbox به مسیر ارسال Sandbox متصل نیست",
 "invoice.transport.disabled":"ارسال واقعی در Backend غیرفعال است",
 "invoice.owner_live_send_approval.required":"مجوز جداگانه ارسال واقعی دریافت نشده است"
};
export default function InvoiceSendReadinessPanel({invoiceId,taxpayerId,isWorkspace}:
 {invoiceId:string;taxpayerId:string;isWorkspace:boolean}){
 const [report,setReport]=useState<Report|null>(null);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState("");
 const [actionBusy,setActionBusy]=useState(false);
 const [notice,setNotice]=useState("");
 const [review,setReview]=useState<Review|null>(null);
 const refresh=useCallback(async(signal?:AbortSignal)=>{
  if(isWorkspace)return;
  setBusy(true);setError("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/readiness",
    {cache:"no-store",credentials:"same-origin",signal});
   if(!response.ok)throw Error(response.status===403||response.status===404?
    "دسترسی به وضعیت این سند مجاز نیست.":"بررسی آمادگی ارسال در دسترس نیست.");
   const value:unknown=await response.json();
   if(!value||typeof value!=="object"||Array.isArray(value))throw Error("پاسخ نامعتبر");
   const data=value as Report;
   if(data.readyForLiveSend!==false||data.submissionEnabled!==false||
      typeof data.signedPacketAvailable!=="boolean"||data.fiscalAccepted!==false||
      !Number.isSafeInteger(data.invoiceRevision)||data.invoiceRevision<1||
      (data.sandboxFiscalMemoryId!==null&&typeof data.sandboxFiscalMemoryId!=="string")||
      !Array.isArray(data.blockingCodes))throw Error("وضعیت ایمنی نامعتبر");
   if(!signal?.aborted)setReport(data);
  }catch(e){if(!signal?.aborted){setReport(null);setError((e as Error).message);}}
  finally{if(!signal?.aborted)setBusy(false);}
 },[invoiceId,taxpayerId,isWorkspace]);
 const validate=useCallback(async()=>{
  if(!report||report.invoiceState!=="Draft"||actionBusy)return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/validate-internal",{
    method:"POST",credentials:"same-origin",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({expectedRevision:report.invoiceRevision})
   });
   const body:unknown=await response.json();
   const data=body&&typeof body==="object"&&!Array.isArray(body)
    ?body as Record<string,unknown>:{};
   if(!response.ok){
    const messages:Record<string,string>={
     unauthorized:"نشست ورود معتبر نیست؛ دوباره وارد حساب شوید.",
     forbidden:"مجوز اعتبارسنجی این صورتحساب برای حساب فعلی وجود ندارد.",
     not_found:"صورتحساب در محدوده دسترسی حساب فعلی پیدا نشد.",
     origin_not_allowed:"مبدأ درخواست مورد تأیید نیست؛ صفحه را از نشانی محلی برنامه باز کنید.",
     invalid_upstream_response:"پاسخ سرویس اعتبارسنجی نامعتبر است؛ وضعیت API بررسی شود.",
     upstream_server_error:"سرویس Backend خطای داخلی دارد؛ Migration و لاگ API بررسی شود.",
     api_unavailable:"اتصال به API برقرار نیست.",
     sandbox_snapshot_validation_failed:"نسخه ثابت صورتحساب شرایط اعتبارسنجی داخلی را ندارد."
    };
    const code=typeof data.error==="string"?data.error:"unknown_error";
    throw Error(messages[code]??("اعتبارسنجی انجام نشد: "+code));
   }
   setNotice("اعتبارسنجی داخلی انجام شد؛ تأیید بازبینی و نهایی‌سازی هنوز لازم است.");
   await refresh();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[report,actionBusy,taxpayerId,invoiceId,refresh]);
 const loadReview=useCallback(async()=>{
  if(isWorkspace)return;
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/review-status",{
    cache:"no-store",credentials:"same-origin"});
   if(!response.ok){setReview(null);return;}
   const value:unknown=await response.json();
   if(value&&typeof value==="object"&&!Array.isArray(value)){
    const candidate=value as Review;
    if(["Draft","InReview","ChangesRequested","Approved"].includes(candidate.state)&&
       Number.isSafeInteger(candidate.revision)&&candidate.revision>=1)
       setReview(candidate);
   }
  }catch{setReview(null);}
 },[isWorkspace,taxpayerId,invoiceId]);
 const advanceReview=useCallback(async(action:"submit"|"approve")=>{
  if(!review||actionBusy)return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/review-action",{
    method:"POST",credentials:"same-origin",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({action,expectedReviewRevision:review.revision,
       idempotencyKey:crypto.randomUUID()})
   });
   const payload:unknown=await response.json();
   const result=payload&&typeof payload==="object"&&!Array.isArray(payload)
    ?payload as Record<string,unknown>:{};
   if(!response.ok)throw Error(response.status===403?
    "مجوز بازبینی این صورتحساب تأیید نشد؛ نشست ورود و مالکیت مؤدی را بررسی کنید.":
    "عملیات بازبینی انجام نشد: "+String(result.error??response.status));
   setNotice(action==="submit"?"صورتحساب برای بازبینی ثبت شد.":"بازبینی داخلی تأیید شد.");
   await loadReview();await refresh();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[review,actionBusy,invoiceId,taxpayerId,loadReview,refresh]);
 const finalize=useCallback(async()=>{
  if(!report||review?.state!=="Approved"||report.invoiceState!=="Validated"||actionBusy)return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/finalize-internal",{
    method:"POST",credentials:"same-origin",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({expectedRevision:report.invoiceRevision})
   });
   const payload:unknown=await response.json();
   const result=payload&&typeof payload==="object"&&!Array.isArray(payload)
    ?payload as Record<string,unknown>:{};
   if(!response.ok)throw Error("نهایی‌سازی انجام نشد: "+String(result.error??response.status));
   setNotice("صورتحساب به‌صورت داخلی نهایی شد؛ این وضعیت به معنی تأیید سازمان نیست.");
   await refresh();await loadReview();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[report,review,actionBusy,invoiceId,taxpayerId,refresh,loadReview]);
 const reserveSerial=useCallback(async()=>{
  if(!report||report.invoiceState!=="Finalized"||actionBusy)return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/serial-reservation",{
    method:"POST",credentials:"same-origin",cache:"no-store",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({expectedRevision:report.invoiceRevision})
   });
   const body:unknown=await response.json();
   const value=body&&typeof body==="object"&&!Array.isArray(body)
    ?body as Record<string,unknown>:{};
   if(!response.ok)throw Error("رزرو سریال انجام نشد: "+String(value.error??response.status));
   setNotice("سریال حافظه Sandbox رزرو شد. شناسه مالیاتی: "+String(value.fiscalTaxId??"نامشخص"));
   await refresh();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[report,actionBusy,taxpayerId,invoiceId,refresh]);
 useEffect(()=>{const controller=new AbortController();void refresh(controller.signal);void loadReview();
  return()=>controller.abort();},[refresh,loadReview]);
 if(isWorkspace)return <p className="field-hint">ابتدا نسخه ثابت داخلی سند را ثبت کنید.</p>;
 return <section dir="rtl" className="invoice-validation" aria-label="پیش‌نیازهای ارسال مالیاتی">
  <div className="invoice-validation__heading">
   <div><h3>آمادگی ارسال به سامانه مؤدیان</h3>
    <p>بررسی فقط‌خواندنی؛ هیچ فاکتوری ارسال نمی‌شود.</p></div>
   <button type="button" className="identity-secondary" disabled={busy}
    onClick={()=>void refresh()}>{busy?"در حال بررسی…":"بازبینی آمادگی"}</button>
  </div>
  {error&&<p role="alert" className="form-error">{error}</p>}
  {notice&&<p role="status" className="field-hint">{notice}</p>}
  {report?.invoiceState==="Validated"&&review&&
   <div className="invoice-validation__actions">
    <p>وضعیت بازبینی داخلی: <strong>{review.state}</strong></p>
    {(review.state==="Draft"||review.state==="ChangesRequested")&&
     <button type="button" className="identity-secondary" disabled={actionBusy||busy}
      onClick={()=>void advanceReview("submit")}>ارسال برای بازبینی</button>}
    {review.state==="InReview"&&
     <button type="button" className="identity-secondary" disabled={actionBusy||busy}
      onClick={()=>void advanceReview("approve")}>تأیید بازبینی صورتحساب</button>}
    {review.state==="Approved"&&
     <button type="button" className="identity-secondary" disabled={actionBusy||busy}
      onClick={()=>void finalize()}>نهایی‌سازی داخلی صورتحساب</button>}
   </div>}
  {report?.invoiceState==="Finalized"&&report.environment==="Sandbox"&&
   report.blockingCodes.includes("invoice.serial.durable_reservation_required")&&
   <button type="button" className="identity-secondary" disabled={actionBusy||busy}
    onClick={()=>void reserveSerial()}>{actionBusy?"در حال رزرو…":"رزرو سریال و تولید شناسه مالیاتی Sandbox"}</button>}
  {report?.invoiceState==="Draft"&&report.hasImmutableSnapshot&&report.environment==="Sandbox"&&
   <button type="button" className="identity-secondary" disabled={actionBusy||busy}
    onClick={()=>void validate()}>{actionBusy?"در حال اعتبارسنجی…":"اعتبارسنجی داخلی صورتحساب"}</button>}
  {report&&<div>
    <p>محیط شناسایی‌شده: <strong>{report.environment==="Sandbox"?"Sandbox":"تأییدنشده"}</strong></p>
    <p>وضعیت سند: <strong>{report.invoiceState}</strong></p>
   <p>نسخه صورتحساب: <strong>{report.invoiceRevision}</strong></p>
   <p>حافظه Sandbox: <strong dir="ltr">{report.sandboxFiscalMemoryId??"متصل نشده"}</strong></p>
    <p className="invoice-validation__warning">وضعیت: غیرمجاز برای ارسال واقعی.</p>
    <ul>{report.blockingCodes.map(code=><li key={code}>{labels[code]??"مانع ناشناخته؛ بررسی لازم است"}</li>)}</ul>
   </div>}
 </section>;
}
