"use client";
import {useCallback,useEffect,useState} from "react";

type Report={environment:"Sandbox"|"Unverified";invoiceState:"Draft"|"Validated"|"Finalized";
 hasImmutableSnapshot:boolean;invoiceRevision:number;sandboxFiscalMemoryId:string|null;
 readyForLiveSend:boolean;submissionEnabled:boolean;
 signedPacketAvailable:boolean;fiscalAccepted:false;blockingCodes:string[]};
type DispatchStatus={status:string;submissionEnabled:boolean;referenceNumber?:string;safeCode?:string;fiscalAccepted:false};
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
 const [dispatch,setDispatch]=useState<DispatchStatus|null>(null);
 const [dispatchError,setDispatchError]=useState("");
 const [dispatchLoading,setDispatchLoading]=useState(false);
 const [dispatchFeedback,setDispatchFeedback]=useState("");
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
   if(typeof data.readyForLiveSend!=="boolean"||typeof data.submissionEnabled!=="boolean"||
      typeof data.signedPacketAvailable!=="boolean"||data.fiscalAccepted!==false||
      !Number.isSafeInteger(data.invoiceRevision)||data.invoiceRevision<1||
      (data.sandboxFiscalMemoryId!==null&&typeof data.sandboxFiscalMemoryId!=="string")||
      !Array.isArray(data.blockingCodes))throw Error("وضعیت ایمنی نامعتبر");
   if(!signal?.aborted)setReport(data);
  }catch(e){if(!signal?.aborted){setReport(null);setError((e as Error).message);}}
  finally{if(!signal?.aborted)setBusy(false);}
 },[invoiceId,taxpayerId,isWorkspace]);
 const loadDispatch=useCallback(async()=>{
  if(isWorkspace)return;
  setDispatchLoading(true);
  setDispatchFeedback("در حال دریافت وضعیت عملیات ارسال از سرور…");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId),{cache:"no-store",credentials:"same-origin"});
   if(!response.ok){
    setDispatch(null);
    setDispatchError("دریافت وضعیت عملیات ارسال ناموفق بود (HTTP "+response.status+").");
    setDispatchFeedback("");
    return;
   }
   const body:unknown=await response.json();
   if(body&&typeof body==="object"&&!Array.isArray(body)){
    const candidate=body as DispatchStatus;
    if(typeof candidate.status==="string"&&typeof candidate.submissionEnabled==="boolean"&&
       candidate.fiscalAccepted===false){
        setDispatch(candidate);setDispatchError("");
        setDispatchFeedback("آخرین وضعیت دریافتی: "+candidate.status+(candidate.submissionEnabled?" — ارسال فعال است.":" — ارسال غیرفعال است."));
       }
    else {setDispatch(null);setDispatchError("پاسخ وضعیت ارسال ساختار معتبر ندارد.");setDispatchFeedback("");}
   }else {setDispatch(null);setDispatchError("پاسخ وضعیت ارسال خالی یا نامعتبر است.");setDispatchFeedback("");}
  }catch{setDispatch(null);setDispatchError("اتصال به سرویس وضعیت ارسال برقرار نشد.");setDispatchFeedback("");}
  finally{setDispatchLoading(false);}
 },[isWorkspace,taxpayerId,invoiceId]);
 const sendSandbox=useCallback(async()=>{
  if(actionBusy||!dispatch||dispatch.status!=="Reserved"||
     !report||report.environment!=="Sandbox"||report.invoiceState!=="Finalized")return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId),{method:"POST",credentials:"same-origin",cache:"no-store"});
   const payload:unknown=await response.json();
   const data=payload&&typeof payload==="object"&&!Array.isArray(payload)
    ?payload as Record<string,unknown>:{};
   const state=typeof data.status==="string"?data.status:"Unavailable";
   if(state==="Acknowledged"&&typeof data.referenceNumber==="string"){
    setNotice("Sandbox دریافت درخواست را با شماره پیگیری "+data.referenceNumber+
     " تأیید کرد؛ نتیجه نهایی مالیاتی هنوز استعلام نشده است.");
   }else if(state==="Indeterminate"){
    setError("نتیجه ارسال نامشخص است. تا روشن‌شدن وضعیت، ارسال را تکرار نکنید. "+String(data.safeCode??""));
   }else{
    setError("ارسال انجام نشد: "+String(data.safeCode??data.error??response.status));
   }
   await loadDispatch();await refresh();
  }catch{
   setError("ارتباط هنگام ارسال قطع شد. احتمال ارسال نامشخص است؛ بدون بررسی وضعیت مجدداً ارسال نکنید.");
   await loadDispatch();
  }finally{setActionBusy(false);}
 },[actionBusy,dispatch,report,taxpayerId,invoiceId,loadDispatch,refresh]);
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
 const prepareLegacy=useCallback(async()=>{
  if(!report||report.invoiceState!=="Finalized"||actionBusy||
     report.blockingCodes.includes("invoice.serial.durable_reservation_required"))return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/prepare-legacy",{
    method:"POST",credentials:"same-origin",cache:"no-store"
   });
   const body:unknown=await response.json();
   const value=body&&typeof body==="object"&&!Array.isArray(body)
    ?body as Record<string,unknown>:{};
   if(!response.ok){
    const errors:Record<string,string>={
     sandbox_legacy_token_disabled:"اتصال Legacy Sandbox فعال یا تنظیم نشده است؛ تنظیمات Endpoint را بررسی کنید.",
     sandbox_legacy_token_unavailable:"دریافت توکن از Sandbox موفق نبود؛ اتصال و لاگ API را بررسی کنید.",
     sandbox_legacy_token_rejected:"سامانه Sandbox درخواست توکن حافظه مالیاتی را رد کرد.",
     sandbox_legacy_key_unavailable:"کلید خصوصی معتبر حافظه Sandbox در دسترس نیست.",
     sandbox_dispatch_fk_migration_required:"Migration اتصال Outbox به حافظه Sandbox باید اعمال شود.",
     fiscal_serial_not_reserved:"رزرو سریال این صورتحساب شناسایی نشد.",
     seller_or_buyer_identity_missing:"شناسه قانونی فروشنده یا خریدار ناقص است.",
     official_goods_mapping_invalid:"نگاشت رسمی کالا یا خدمت قابل تأیید نیست.",
     official_units_unverified:"کد رسمی واحد اندازه‌گیری کالا یا خدمت تأیید نشده است.",
     legacy_wire_mapping_unverified:"اطلاعات این صورتحساب با الگوی فعلی ارسال Legacy سازگار نیست.",
     invoice_number_missing:"شماره داخلی صورتحساب از سوابق سرور پیدا نشد.",
     dispatch_operation_requires_reconciliation:"یک عملیات قبلی وجود دارد که به بررسی Outbox نیاز دارد؛ ارسال تکراری ایجاد نشد.",
     concurrent_preparation_retry_read_status:"آماده‌سازی همزمان انجام شده است؛ ابتدا وضعیت ارسال را تازه‌سازی کنید.",
     preparation_api_unavailable:"سرویس آماده‌سازی Backend در دسترس نیست."
    };
    const code=String(value.error??response.status);
    throw Error(errors[code]??("آماده‌سازی بسته Legacy انجام نشد: "+code));
   }
   setNotice("بسته Legacy امضا و در Outbox آزمایشی ثبت شد. هنوز صورتحساب ارسال نشده است.");
   await refresh();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[report,actionBusy,taxpayerId,invoiceId,refresh]);
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
 useEffect(()=>{const controller=new AbortController();void refresh(controller.signal);void loadReview();void loadDispatch();
  return()=>controller.abort();},[refresh,loadReview,loadDispatch]);
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
  <button type="button" className="identity-secondary" disabled={busy||actionBusy||dispatchLoading}
   onClick={()=>void loadDispatch()}>{dispatchLoading?"در حال دریافت وضعیت…":"به‌روزرسانی وضعیت عملیات ارسال"}</button>
  {dispatchFeedback&&<p role="status" className="field-hint">{dispatchFeedback}</p>}
  {dispatchError&&<p role="alert" className="form-error">{dispatchError}</p>}
  {dispatch?.status==="Reserved"&&!dispatch.submissionEnabled&&
   <p role="alert" className="form-error">عملیات Reserved است، اما تنظیمات فعال‌سازی ارسال Legacy در Backend کامل نیست.</p>}
  {dispatch&&<p role="status" className="field-hint">وضعیت ارسال Sandbox: <strong>{dispatch.status}</strong>
   {dispatch.referenceNumber&&<> — شماره پیگیری: <b dir="ltr">{dispatch.referenceNumber}</b></>}</p>}
  {report?.environment==="Sandbox"&&report.invoiceState==="Finalized"&&
   dispatch?.status==="Reserved"&&dispatch.submissionEnabled&&
   <button type="button" className="identity-secondary" disabled={actionBusy||busy}
    onClick={()=>void sendSandbox()}>{actionBusy?"در حال ارسال…":"ارسال واقعی صورتحساب به Sandbox"}</button>}
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
   !report.blockingCodes.includes("invoice.serial.durable_reservation_required")&&
   report.blockingCodes.includes("invoice.signed_packet.not_prepared")&&
   <button type="button" className="identity-secondary" disabled={actionBusy||busy}
    onClick={()=>void prepareLegacy()}>
    {actionBusy?"در حال آماده‌سازی…":"امضا و آماده‌سازی بسته Legacy برای Sandbox"}
   </button>}
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
    <p className="invoice-validation__warning">{report.readyForLiveSend?"وضعیت: آماده ارسال آزمایشی به Sandbox.":"وضعیت: هنوز آماده ارسال آزمایشی نیست."}</p>
    <ul>{report.blockingCodes.map(code=><li key={code}>{labels[code]??"مانع ناشناخته؛ بررسی لازم است"}</li>)}</ul>
   </div>}
 </section>;
}
