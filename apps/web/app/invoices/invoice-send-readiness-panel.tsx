"use client";
import {useCallback,useEffect,useRef,useState} from "react";
import InvoiceDispatchHistory from "./invoice-dispatch-history";

type Report={environment:"Sandbox"|"Unverified";invoiceState:"Draft"|"Validated"|"Finalized";
 hasImmutableSnapshot:boolean;invoiceRevision:number;sandboxFiscalMemoryId:string|null;
 readyForLiveSend:boolean;submissionEnabled:boolean;
 signedPacketAvailable:boolean;fiscalAccepted:false;blockingCodes:string[]};
type DispatchStatus={status:string;submissionEnabled:boolean;referenceNumber?:string;safeCode?:string;fiscalAccepted:false};
type CalculationFinding={code:"0204501"|"0304401"|"0200201"|"00010"|"00026";
 field:string;message:string;expected:string|null;actual:string|null};
type CalculationAudit={invoiceRevision:number;canResend:boolean;findings:CalculationFinding[]};
type WireField={path:string;value:string};
function flattenWire(value:unknown,path=""):WireField[]{
 if(value!==null&&typeof value==="object"){
  if(Array.isArray(value))return value.flatMap((item,index)=>flattenWire(item,`${path}[${index+1}]`));
  return Object.entries(value as Record<string,unknown>).flatMap(([key,item])=>
   flattenWire(item,path?`${path}.${key}`:key));
 }
 return [{path,value:JSON.stringify(value)}];
}
type AuthorityResult={status:"FAILED"|"SUCCESS"|"PENDING"|"UNKNOWN";
 errors:{code:string;message:string}[];warnings:{code:string;message:string}[];
 canPrepareCorrection:boolean;observedAtUtc:string};
const authorityLabels:Record<AuthorityResult["status"],string>={
 FAILED:"رد شده توسط سازمان",SUCCESS:"نتیجه موفق در Sandbox",
 PENDING:"در انتظار نتیجه سازمان",UNKNOWN:"نتیجه سازمان نامشخص است"};
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
export default function InvoiceSendReadinessPanel({invoiceId,taxpayerId,isWorkspace,hideHistory=false}:
 {invoiceId:string;taxpayerId:string;isWorkspace:boolean;hideHistory?:boolean}){
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
 const [authority,setAuthority]=useState<{scope:string;result:AuthorityResult|null;referenceNumber:string|null}|null>(null);
 const [authorityError,setAuthorityError]=useState("");
 const [correctionUrl,setCorrectionUrl]=useState("");
 const [calculationAudit,setCalculationAudit]=useState<CalculationAudit|null>(null);
 const [calculationAuditError,setCalculationAuditError]=useState("");
 const [wirePreviewJson,setWirePreviewJson]=useState("");
 const [reviewedWirePreview,setReviewedWirePreview]=useState(false);
 const authorityScope=taxpayerId+"/"+invoiceId;
 const authorityResult=authority?.scope===authorityScope?authority.result:null;
 const authorityController=useRef<AbortController|null>(null);
 const authoritySequence=useRef(0);
 const loadAuthorityReport=useCallback(async(signal:AbortSignal)=>{
  if(isWorkspace)return;
  const sequence=++authoritySequence.current;
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId)+"/inquiry-by-reference",
    {method:"GET",cache:"no-store",credentials:"same-origin",signal});
   if(!response.ok)throw Error("دریافت گزارش ذخیره‌شده سازمان ناموفق بود.");
   const data=await response.json();
   if(!signal.aborted&&sequence===authoritySequence.current){
    setAuthority({scope:taxpayerId+"/"+invoiceId,result:data.authorityResult??null,
     referenceNumber:data.referenceNumber??null});setAuthorityError("");
   }
  }catch(e){if(!signal.aborted&&sequence===authoritySequence.current)setAuthorityError((e as Error).message);}
 },[isWorkspace,taxpayerId,invoiceId]);
 const loadCalculationAudit=useCallback(async()=>{
  if(isWorkspace)return;
  setCalculationAuditError("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId)+"/calculation-audit",{cache:"no-store",credentials:"same-origin"});
   if(!response.ok)throw Error("بررسی محاسبات صورتحساب در دسترس نیست.");
   const value:unknown=await response.json();
   if(!value||typeof value!=="object"||Array.isArray(value))throw Error("گزارش محاسبات نامعتبر است.");
   const data=value as Record<string,unknown>;
   if(!Number.isSafeInteger(data.invoiceRevision)||Number(data.invoiceRevision)<1||
      typeof data.canResend!=="boolean"||!Array.isArray(data.findings))
     throw Error("گزارش محاسبات نامعتبر است.");
   const findings=data.findings.filter((item):item is CalculationFinding=>{
    if(!item||typeof item!=="object"||Array.isArray(item))return false;
    const row=item as Record<string,unknown>;
    return typeof row.code==="string"&&typeof row.field==="string"&&typeof row.message==="string"&&
      (row.expected===null||typeof row.expected==="string")&&
      (row.actual===null||typeof row.actual==="string");
   });
   if(findings.length!==data.findings.length)throw Error("گزارش محاسبات نامعتبر است.");
   setCalculationAudit({invoiceRevision:Number(data.invoiceRevision),canResend:data.canResend,findings});
  }catch(e){setCalculationAudit(null);setCalculationAuditError((e as Error).message);}
 },[isWorkspace,taxpayerId,invoiceId]);
 useEffect(()=>{
  const controller=new AbortController();authorityController.current=controller;
  setAuthority(null);setAuthorityError("");setCorrectionUrl("");setActionBusy(false);setNotice("");
  setWirePreviewJson("");setReviewedWirePreview(false);
  // The authority inquiry report is fetched only when the user opens its section.
  return()=>controller.abort();
 },[loadAuthorityReport]);
 const prepareCorrection=useCallback(async()=>{
  if(actionBusy||authorityResult?.status!=="FAILED"||!authorityResult.canPrepareCorrection)return;
  const signal=authorityController.current?.signal;
  setActionBusy(true);setError("");setCorrectionUrl("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId)+"/correction-draft",
    {method:"POST",cache:"no-store",credentials:"same-origin",signal});
   const data=await response.json().catch(()=>({}));
   if(signal?.aborted)return;
   if(!response.ok){
    const code=typeof data.error==="string"?data.error:"";
    const messages:Record<string,string>={
     authority_rejection_required:"گزارش رد معتبر از استعلام سازمان در دسترس نیست؛ استعلام را تازه کنید و دوباره تلاش کنید.",
     correction_draft_creation_failed:"گزارش سازمان دریافت شد، اما ذخیرهٔ پیش‌نویس انجام نشد. دوباره تلاش کنید.",
     correction_draft_invalid:"داده‌های پیش‌نویس اصلاح معتبر نیستند؛ شناسهٔ ۱۳ رقمی کالا و تاریخ واقعی صدور را بررسی کنید.",
     correction_draft_empty:"ذخیره انجام شد اما شناسهٔ پیش‌نویس از سرویس دریافت نشد؛ صفحه را تازه کنید.",
     correction_link_conflict:"پیش‌نویس اصلاح قبلاً ایجاد شده، اما پیوند آن کامل نشده است. صفحه را تازه‌سازی کنید.",
     source_workspace_missing:"پیش‌نویس اولیهٔ این فاکتور پیدا نشد.",
     source_workspace_revision_mismatch:"پیش‌نویس اولیه پس از صدور تغییر کرده است؛ وضعیت فاکتور را تازه کنید.",
     sandbox_snapshot_required:"این فاکتور برای اصلاح در محیط Sandbox آماده نیست.",
     correction_api_unavailable:"سرویس ایجاد پیش‌نویس موقتاً در دسترس نیست؛ دوباره تلاش کنید.",
     correction_access_denied:"دسترسی به پیش‌نویس اصلاح مجاز نیست.",
     correction_rejected:"درخواست ایجاد پیش‌نویس از سوی سرویس رد شد.",
    };
    throw Error(messages[code]||`ایجاد پیش‌نویس انجام نشد (HTTP ${response.status}${code?`, ${code}`:""}).`);
   }
   if(data.authoritySubmitted!==false||typeof data.workspaceId!=="string"||
    !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(data.workspaceId))
    throw Error("پاسخ ایجاد پیش‌نویس نامعتبر است.");
   const dateAdjusted=data.issueDateAdjusted===true;
   const url="/invoices?workspaceId="+encodeURIComponent(data.workspaceId)+
    (dateAdjusted?"&issueDateAdjusted=1":"");
   setCorrectionUrl(url);
   setNotice("پیش‌نویس اصلاح ایجاد شد؛ هیچ ارسال جدیدی انجام نشده است.");
   window.location.assign(url);
  }catch(e){if(!signal?.aborted)setError((e as Error).message);}
  finally{if(!signal?.aborted)setActionBusy(false);}
 },[actionBusy,authorityResult,taxpayerId,invoiceId]);
 const refresh=useCallback(async(signal?:AbortSignal)=>{
  if(isWorkspace)return;
  setBusy(true);setError("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/readiness",
    {cache:"no-store",credentials:"same-origin",signal});
   if(!response.ok)throw Error(response.status===401?
    "نشست ورود منقضی شده است؛ دوباره وارد حساب شوید.":
    response.status===403||response.status===404?
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
    setDispatchError(response.status===401?
     "نشست ورود منقضی شده است. یک‌بار از حساب خارج شوید و دوباره وارد شوید، سپس این صفحه را باز کنید.":
     "دریافت وضعیت عملیات ارسال ناموفق بود (HTTP "+response.status+").");
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
 const armDispatch=useCallback(async()=>{
  if(actionBusy||dispatch?.status!=="Prepared"||report?.invoiceState!=="Finalized")return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
     encodeURIComponent(invoiceId)+"/arm",{
     method:"POST",credentials:"same-origin",cache:"no-store"});
   const body:unknown=await response.json();
   const data=body&&typeof body==="object"&&!Array.isArray(body)
    ?body as Record<string,unknown>:{};
   if(!response.ok)throw Error("انتقال به صف ارسال انجام نشد: "+String(data.error??response.status));
   setNotice("بسته موجود برای ارسال Sandbox رزرو شد؛ هنوز درخواستی به سازمان ارسال نشده است.");
   await loadDispatch();await refresh();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[actionBusy,dispatch,report,taxpayerId,invoiceId,loadDispatch,refresh]);
 const oneClickSend=useCallback(async()=>{
  if(actionBusy||isWorkspace||dispatchLoading||dispatch?.status==="Indeterminate"||
    dispatch?.status==="Sending"||dispatch?.status==="Acknowledged")return;
  setActionBusy(true);setError("");setNotice("در حال اعتبارسنجی و ارسال…");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId)+"/send-and-inquire",{method:"POST",cache:"no-store",
    credentials:"same-origin"});
   const value:unknown=await response.json();
   const result=value&&typeof value==="object"&&!Array.isArray(value)?value as Record<string,unknown>:{};
   if(!response.ok)throw Error("فرآیند متوقف شد: "+String(result.error??response.status));
   if(result.status==="Acknowledged")
    setNotice("صورتحساب با شماره رهگیری "+String(result.referenceNumber??"—")+
      " ارسال شد. نتیجه استعلام: "+String(result.safeCode??"در انتظار بررسی"));
   else setError("نتیجه ارسال مشخص نیست؛ ارسال را تکرار نکنید. "+String(result.safeCode??""));
   await loadDispatch();await refresh();await loadReview();
  }catch(e){setError((e as Error).message+"؛ پیش از تلاش دیگر وضعیت را تازه کنید.");await loadDispatch();}
  finally{setActionBusy(false);}
 },[actionBusy,isWorkspace,dispatchLoading,dispatch,taxpayerId,invoiceId,loadDispatch,refresh,loadReview]);
 const readyExisting=useCallback(async()=>{
  if(actionBusy||!report||calculationAudit?.invoiceRevision!==report.invoiceRevision||!calculationAudit.canResend||report.invoiceState!=="Finalized"||
     report.environment!=="Sandbox"||dispatchLoading||dispatch?.status==="Indeterminate"||
     dispatch?.status==="Acknowledged"||dispatch?.status==="Sending")return;
  setActionBusy(true);setError("");setNotice("");
  const base="/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
   encodeURIComponent(invoiceId);
  const invoke=async(path:string,body?:unknown)=>{
   const result=await fetch(base+path,{method:"POST",credentials:"same-origin",
    cache:"no-store",...(body===undefined?{}:{headers:{"Content-Type":"application/json"},
    body:JSON.stringify(body)})});
   const data:unknown=await result.json();
   const value=data&&typeof data==="object"&&!Array.isArray(data)
    ?data as Record<string,unknown>:{};
   if(!result.ok){
    const code=String(value.error??value.safeCode??result.status);
    throw Error(code==="seller_economic_identifier_invalid"
     ?"شماره اقتصادی فروشنده باید ۱۱ یا ۱۴ رقم باشد؛ شناسه فعلی برای ارسال معتبر نیست.":code);
   }
   return value;
  };
  try{
   let state=dispatch?.status??"NotPrepared";
   if(state==="NotPrepared"){
    if(report.blockingCodes.includes("invoice.serial.durable_reservation_required")){
     await invoke("/serial-reservation",{expectedRevision:report.invoiceRevision});
    }
    const prepared=await invoke("/prepare-legacy");
    if(typeof prepared.wirePreviewJson!=="string")throw Error("پیش‌نمایش دقیق فیلدهای ارسالی دریافت نشد.");
    setWirePreviewJson(prepared.wirePreviewJson);setReviewedWirePreview(false);
    state="Prepared";
   }else if(state==="Prepared"){
    const prepared=await invoke("/prepare-legacy");
    if(typeof prepared.wirePreviewJson!=="string")throw Error("پیش‌نمایش دقیق فیلدهای ارسالی دریافت نشد.");
    setWirePreviewJson(prepared.wirePreviewJson);setReviewedWirePreview(false);
   }
   if(state==="Prepared"){
    await invoke("/arm");
    state="Reserved";
   }
   setNotice(state==="Reserved"?
    "صورتحساب برای ارسال آماده است. برای ارسال واقعی، دکمه «ارسال واقعی صورتحساب به Sandbox» را بزنید.":
    "وضعیت عملیات به‌روزرسانی شد.");
  }catch(e){setError("آماده‌سازی ارسال متوقف شد: "+(e as Error).message);}
  finally{
   await loadDispatch();await refresh();setActionBusy(false);
  }
 },[actionBusy,report,calculationAudit,dispatch,dispatchLoading,taxpayerId,invoiceId,loadDispatch,refresh]);
 const loadWirePreview=useCallback(async()=>{
  if(actionBusy||!dispatch||!["Prepared","Reserved"].includes(dispatch.status))return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId)+"/prepare-legacy",{method:"POST",credentials:"same-origin",cache:"no-store"});
   const data:unknown=await response.json();
   const value=data&&typeof data==="object"&&!Array.isArray(data)?data as Record<string,unknown>:{};
   if(!response.ok||typeof value.wirePreviewJson!=="string")
    throw Error("پیش‌نمایش فیلدهای نهایی ارسالی دریافت نشد: "+String(value.error??response.status));
   setWirePreviewJson(value.wirePreviewJson);setReviewedWirePreview(false);
   setNotice("پیش‌نمایش از نسخه ثابت فاکتور ساخته شد؛ هنوز چیزی به سازمان ارسال نشده است.");
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[actionBusy,dispatch,taxpayerId,invoiceId]);
 const inquireByReference=useCallback(async()=>{
  if(actionBusy||dispatch?.status!=="Acknowledged")return;
  const signal=authorityController.current?.signal;
  ++authoritySequence.current;setAuthority(null);setAuthorityError("");
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/inquiry-by-reference",{
    method:"POST",credentials:"same-origin",cache:"no-store",signal});
   const body:unknown=await response.json();
   const data=body&&typeof body==="object"&&!Array.isArray(body)
    ?body as Record<string,unknown>:{};
   if(signal?.aborted)return;
   if(!response.ok)throw Error("استعلام رهگیری انجام نشد: "+String(data.error??response.status));
   setAuthority({scope:taxpayerId+"/"+invoiceId,result:(data.authorityResult??null) as AuthorityResult|null,
    referenceNumber:typeof data.referenceNumber==="string"?data.referenceNumber:null});
   setNotice("نتیجه استعلام شماره رهگیری: "+String(data.safeCode??"نامشخص")+
    (typeof data.referenceNumber==="string"?" — شماره مرجع: "+data.referenceNumber:"")+
    " — وضعیت پذیرش قطعی مالیاتی هنوز تأیید نشده است.");
  }catch(e){if(!signal?.aborted)setError((e as Error).message);}
  finally{if(!signal?.aborted)setActionBusy(false);}
 },[actionBusy,dispatch,taxpayerId,invoiceId]);
 const inquireByUid=useCallback(async()=>{
  if(actionBusy||dispatch?.status!=="Indeterminate")return;
  setActionBusy(true);setError("");setNotice("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/inquiry-by-uid",{
    method:"POST",credentials:"same-origin",cache:"no-store"});
   const body:unknown=await response.json();
   const data=body&&typeof body==="object"&&!Array.isArray(body)
    ?body as Record<string,unknown>:{};
   if(!response.ok)throw Error("استعلام انجام نشد: "+String(data.error??response.status));
   const code=String(data.safeCode??"inquiry_inconclusive");
   setNotice(data.referenceNumber?
    "استعلام UID دارای نتیجه مرتبط است؛ شماره مرجع: "+String(data.referenceNumber)+
    " — پذیرش نهایی مالیاتی هنوز تأیید نشده است.":
    "استعلام UID انجام شد: "+code+" — نتیجه قطعی مشخص نیست؛ ارسال مجدد نکنید.");
   await loadDispatch();
  }catch(e){setError((e as Error).message);}
  finally{setActionBusy(false);}
 },[actionBusy,dispatch,taxpayerId,invoiceId,loadDispatch]);
 const sendSandbox=useCallback(async()=>{
  if(actionBusy||!dispatch||dispatch.status!=="Reserved"||!wirePreviewJson||!reviewedWirePreview||
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
 },[actionBusy,dispatch,report,wirePreviewJson,reviewedWirePreview,taxpayerId,invoiceId,loadDispatch,refresh]);
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
     seller_economic_identifier_invalid:"شماره اقتصادی فروشنده باید ۱۱ یا ۱۴ رقم باشد؛ شناسه فعلی برای ارسال معتبر نیست.",
     invoice_calculation_audit_failed:"محاسبات یا فیلدهای اجباری صورتحساب معتبر نیست؛ خطاهای محاسباتی را اصلاح و دوباره بررسی کنید.",
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
 useEffect(()=>{const controller=new AbortController();void refresh(controller.signal);void loadReview();void loadDispatch();void loadCalculationAudit();
  return()=>controller.abort();},[refresh,loadReview,loadDispatch,loadCalculationAudit]);
 if(isWorkspace)return <p className="field-hint">ابتدا نسخه ثابت داخلی سند را ثبت کنید.</p>;
 return <section dir="rtl" className="invoice-validation" aria-label="پیش‌نیازهای ارسال مالیاتی">
  {dispatch?.status!=="Acknowledged"&&<div className="invoice-validation__heading">
   <div><h3>آمادگی ارسال به سامانه مؤدیان</h3>
    <p>بررسی فقط‌خواندنی؛ هیچ فاکتوری ارسال نمی‌شود.</p></div>
   <button type="button" className="identity-secondary" disabled={busy}
    onClick={()=>void refresh()}>{busy?"در حال بررسی…":"بازبینی آمادگی"}</button>
  </div>
  }
  {error&&<p role="alert" className="form-error">{error}</p>}
  {notice&&<p role="status" className="field-hint">{notice}</p>}
  {dispatch?.status!=="Acknowledged"&&<button type="button" className="identity-secondary" disabled={busy||actionBusy||dispatchLoading}
   onClick={()=>void loadDispatch()}>{dispatchLoading?"در حال دریافت وضعیت…":"به‌روزرسانی وضعیت عملیات ارسال"}</button>}
  {dispatchFeedback&&<p role="status" className="field-hint">{dispatchFeedback}</p>}
  {dispatchError&&<p role="alert" className="form-error">{dispatchError}</p>}
  {calculationAuditError&&<p role="alert" className="form-error">{calculationAuditError}</p>}
  {calculationAudit?.findings?.length ? <section className="invoice-calculation-audit" aria-label="خطاهای محاسباتی صورتحساب">
   <h4>خطاهای محاسباتی و فیلدهای اجباری</h4>
   <ul>{calculationAudit.findings.map((item,index)=><li key={item.code+":"+index}>
    <bdi dir="ltr">{item.code}</bdi> — {item.message}
    {item.expected!==null&&<>؛ مقدار مورد انتظار: <bdi dir="ltr">{item.expected}</bdi></>}
    {item.actual!==null&&<>؛ مقدار فعلی: <bdi dir="ltr">{item.actual}</bdi></>}
   </li>)}</ul>
  </section>:null}
  <details aria-label="استعلام و گزارش سازمان" onToggle={event=>{
    if(event.currentTarget.open&&dispatch?.status==="Acknowledged"){
     const controller=authorityController.current;
     if(controller&&!controller.signal.aborted)void loadAuthorityReport(controller.signal);
    }
   }}>
   <summary>استعلام و گزارش نتیجه سازمان (باز کردن در صورت نیاز)</summary>
  {report?.environment==="Sandbox"&&dispatch?.status==="Acknowledged"&&
   <button type="button" className="identity-secondary" disabled={actionBusy||busy}
    onClick={()=>void inquireByReference()}>{actionBusy?"در حال استعلام…":"استعلام شماره رهگیری از Sandbox"}</button>}
  {report?.environment==="Sandbox"&&dispatch?.status==="Indeterminate"&&
   <button type="button" className="identity-secondary" disabled={actionBusy||busy}
    onClick={()=>void inquireByUid()}>{actionBusy?"در حال استعلام…":"استعلام نتیجه ارسال قبلی از Sandbox با UID"}</button>}

     {authorityError&&<p role="alert" className="form-error">{authorityError}</p>}
  {authorityResult&&<div aria-label="گزارش نتیجه سازمان">
   <h4>نتیجه استعلام سازمان: {authorityLabels[authorityResult.status]}</h4>
   <p className="field-hint">شماره رهگیری: رسید درخواست، نه تأیید نهایی.</p>
   {authority?.referenceNumber&&<p>شماره مرجع: <b dir="ltr">{authority.referenceNumber}</b></p>}
   <p>زمان مشاهده: <time dir="ltr">{authorityResult.observedAtUtc}</time></p>
   <h5>خطاهای سازمان</h5>
   <ul>{authorityResult.errors.map((item,index)=><li key={index}><b dir="ltr">{item.code}</b> — {item.message}</li>)}</ul>
   <h5>هشدارهای سازمان</h5>
   <ul>{authorityResult.warnings.map((item,index)=><li key={index}><b dir="ltr">{item.code}</b> — {item.message}</li>)}</ul>
   {(authorityResult.status==="PENDING"||authorityResult.status==="UNKNOWN")&&
    <p>نتیجه قطعی مشخص نیست؛ ارسال را تکرار نکنید.</p>}
   {authorityResult.status==="SUCCESS"&&<p>این نتیجه مربوط به Sandbox است؛ پذیرش قطعی مالیاتی ثبت نشده است.</p>}
   {authorityResult.status==="FAILED"&&authorityResult.canPrepareCorrection&&
    <button type="button" className="identity-secondary" disabled={actionBusy||!!correctionUrl}
     onClick={()=>void prepareCorrection()}>بازگشت به پیش‌نویس اصلاحی</button>}
  </div>}

  </details>
  {correctionUrl&&<a href={correctionUrl}>باز کردن پیش‌نویس اصلاح برای ویرایش</a>}
  {dispatch?.status==="Reserved"&&!dispatch.submissionEnabled&&
   <p role="alert" className="form-error">عملیات Reserved است، اما تنظیمات فعال‌سازی ارسال Legacy در Backend کامل نیست.</p>}
  {dispatch?.status==="Acknowledged"&&<p role="status" className="field-hint">وضعیت ارسال Sandbox: <strong>{dispatch.status}</strong>
   {dispatch.referenceNumber&&<> — شماره پیگیری: <b dir="ltr">{dispatch.referenceNumber}</b></>}</p>}
  {report?.environment==="Sandbox"&&
   dispatch?.status!=="Acknowledged"&&dispatch?.status!=="Indeterminate"&&
   dispatch?.status!=="Sending"&&
   <button type="button" className="identity-primary" disabled={actionBusy||busy||dispatchLoading}
    onClick={()=>void oneClickSend()}>{actionBusy?"در حال پردازش…":"ارسال به سامانه مؤدیان و استعلام"}</button>}
  {dispatch?.status==="Indeterminate"&&<p role="alert" className="form-error">
    نتیجه ارسال قبلی مشخص نیست؛ ارسال مجدد خودکار انجام نمی‌شود.
   </p>}
  {!hideHistory&&<InvoiceDispatchHistory taxpayerId={taxpayerId} invoiceId={invoiceId}/>}
 </section>;
}
