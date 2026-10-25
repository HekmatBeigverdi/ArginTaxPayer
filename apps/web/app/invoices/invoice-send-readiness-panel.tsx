"use client";
import {useCallback,useEffect,useState} from "react";

type Report={environment:"Sandbox"|"Unverified";invoiceState:"Draft"|"Validated"|"Finalized";
 hasImmutableSnapshot:boolean;invoiceRevision:number;sandboxFiscalMemoryId:string|null;
 readyForLiveSend:false;submissionEnabled:false;
 signedPacketAvailable:false;fiscalAccepted:false;blockingCodes:string[]};
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
      data.signedPacketAvailable!==false||data.fiscalAccepted!==false||
      !Number.isSafeInteger(data.invoiceRevision)||data.invoiceRevision<1||
      (data.sandboxFiscalMemoryId!==null&&typeof data.sandboxFiscalMemoryId!=="string")||
      !Array.isArray(data.blockingCodes))throw Error("وضعیت ایمنی نامعتبر");
   if(!signal?.aborted)setReport(data);
  }catch(e){if(!signal?.aborted){setReport(null);setError((e as Error).message);}}
  finally{if(!signal?.aborted)setBusy(false);}
 },[invoiceId,taxpayerId,isWorkspace]);
 useEffect(()=>{const controller=new AbortController();void refresh(controller.signal);
  return()=>controller.abort();},[refresh]);
 if(isWorkspace)return <p className="field-hint">ابتدا نسخه ثابت داخلی سند را ثبت کنید.</p>;
 return <section dir="rtl" className="invoice-validation" aria-label="پیش‌نیازهای ارسال مالیاتی">
  <div className="invoice-validation__heading">
   <div><h3>آمادگی ارسال به سامانه مؤدیان</h3>
    <p>بررسی فقط‌خواندنی؛ هیچ فاکتوری ارسال نمی‌شود.</p></div>
   <button type="button" className="identity-secondary" disabled={busy}
    onClick={()=>void refresh()}>{busy?"در حال بررسی…":"بازبینی آمادگی"}</button>
  </div>
  {error&&<p role="alert" className="form-error">{error}</p>}
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
