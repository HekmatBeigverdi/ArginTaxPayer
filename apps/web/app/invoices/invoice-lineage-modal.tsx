"use client";
import {useEffect,useRef,useState} from "react";
import InvoiceFormalPreview from "./invoice-formal-preview";

type Node={invoiceId:string;subject:string;fiscalTaxId:string|null;
 issuedAtUtc:string;dispatchState:string;authorityReference:string|null;
 authorityStatus:string|null;sentAtUtc:string|null;canResumeSubmission:boolean;
 systemCode?:string|null;referenceFiscalTaxId?:string|null};
type Graph={hasDispatchAttempt:boolean;hasChanges:boolean;outcomeUnknown:boolean;nodes:Node[]};
type AuthorityIssue={code:string;message:string};
type AuthorityReport={status:"FAILED"|"SUCCESS"|"PENDING"|"IN_PROGRESS"|"UNKNOWN";
 errors:AuthorityIssue[];warnings:AuthorityIssue[];observedAtUtc:string};
const label:Record<string,string>={Original:"اصلی",Correction:"اصلاحی",Cancellation:"ابطالی",SalesReturn:"برگشت از فروش"};
const authorityStatusLabel:Record<string,string>={
 FAILED:"رد شده توسط سازمان",SUCCESS:"نتیجه موفق سازمان",PENDING:"در انتظار بررسی سازمان",
 IN_PROGRESS:"در صف ارسال به سازمان",
 UNKNOWN:"نتیجه سازمان نامشخص",ACKNOWLEDGED:"کد رهگیری دریافت شد",INDETERMINATE:"نتیجه نامشخص"};
const date=(value:string|null)=>value?new Date(value).toLocaleString("fa-IR"):"—";
export default function InvoiceLineageModal({taxpayerId,invoiceId,onClose,onChanged}:{
 taxpayerId:string;invoiceId:string;onClose:()=>void;onChanged?:()=>void}){
 const [graph,setGraph]=useState<Graph|null>(null);
 const [error,setError]=useState("");
 const [reload,setReload]=useState(0);
 const [inquiryBusy,setInquiryBusy]=useState<string|null>(null);
 const [inquiryMessage,setInquiryMessage]=useState("");
 const [inquiryReport,setInquiryReport]=useState<{invoiceId:string;report:AuthorityReport}|null>(null);
 const [printTargetId,setPrintTargetId]=useState<string|null>(null);
 const [printBusyId,setPrintBusyId]=useState<string|null>(null);
 const [printReady,setPrintReady]=useState<Record<string,boolean>>({});
 const previewDetails=useRef<Record<string,HTMLDetailsElement|null>>({});
 const [loading,setLoading]=useState(true);
 const resumeCorrection=async(node:Node)=>{
  setInquiryBusy(node.invoiceId);setInquiryMessage("");setInquiryReport(null);
  try{
   const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
    encodeURIComponent(invoiceId)+"/correction-send-and-inquire",{
    method:"POST",credentials:"same-origin",cache:"no-store",signal:AbortSignal.timeout(115000)});
   const result=await response.json() as {code?:string;status?:string;invoiceId?:string|null;
    referenceNumber?:string|null;error?:string};
   if(!response.ok)throw Error(String(result.code??result.error??response.status));
   setInquiryMessage("نتیجه ادامه ارسال: "+String(result.code??"نامشخص")+
    (result.status?" — وضعیت "+result.status:""));
   if(result.invoiceId&&result.referenceNumber){
    const inquiry=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
     encodeURIComponent(result.invoiceId)+"/inquiry-by-reference",{
     method:"POST",credentials:"same-origin",cache:"no-store",signal:AbortSignal.timeout(40000)});
    const report=await inquiry.json() as {authorityResult?:AuthorityReport|null;safeCode?:string;error?:string};
    if(!inquiry.ok)throw Error(String(report.error??inquiry.status));
    if(report.authorityResult)setInquiryReport({invoiceId:result.invoiceId,report:report.authorityResult});
    setInquiryMessage("ارسال: "+String(result.code??"نامشخص")+
     "؛ استعلام: "+(report.authorityResult
      ?authorityStatusLabel[report.authorityResult.status]??report.authorityResult.status
      :String(report.safeCode??"در انتظار بررسی")));
   }
  }catch(e){setInquiryMessage("ادامه ارسال یا استعلام ناموفق بود: "+(e as Error).message);}
  finally{setInquiryBusy(null);setReload(value=>value+1);onChanged?.();}
 };
 useEffect(()=>{
  const controller=new AbortController();
  setLoading(true);setError("");
  void (async()=>{
   try{
    const res=await fetch("/api/invoice-dispatch/"+
     encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/lineage",
     {credentials:"same-origin",cache:"no-store",signal:controller.signal});
    if(!res.ok)throw Error("دریافت زنجیره صورتحساب ناموفق بود.");
    const data=await res.json() as Graph;
    if(!controller.signal.aborted)setGraph(data);
   }catch(e){if(!controller.signal.aborted)setError((e as Error).message);}
   finally{if(!controller.signal.aborted)setLoading(false);}
  })();
  return()=>controller.abort();
 },[taxpayerId,invoiceId,reload]);
 useEffect(()=>{
  if(!printTargetId||!printReady[printTargetId])return;
  const sheet=previewDetails.current[printTargetId]?.querySelector<HTMLElement>(".invoice-paper-print-sheet");
  if(!sheet){setPrintBusyId(null);setPrintTargetId(null);return;}
  const printRoot=document.createElement("div");
  printRoot.id="invoice-lineage-print-root";
  printRoot.dir="rtl";
  printRoot.append(sheet.cloneNode(true));
  document.body.append(printRoot);
  document.body.classList.add("invoice-lineage-printing");
  const afterPrint=()=>{
   if(printTargetId)previewDetails.current[printTargetId]?.removeAttribute("open");
   document.body.classList.remove("invoice-lineage-printing");
   printRoot.remove();
   setPrintBusyId(null);
   setPrintTargetId(null);
  };
  window.addEventListener("afterprint",afterPrint,{once:true});
  const imageReady=Promise.all(Array.from(printRoot.querySelectorAll<HTMLImageElement>("img"),image=>
   image.decode().catch(()=>undefined)));
  const timer=window.setTimeout(()=>{void imageReady.then(()=>window.print());},100);
  return()=>{window.clearTimeout(timer);window.removeEventListener("afterprint",afterPrint);
   document.body.classList.remove("invoice-lineage-printing");printRoot.remove();};
 },[printTargetId,printReady]);
 return <div className="invoice-register__overlay" onMouseDown={e=>{if(e.target===e.currentTarget)onClose();}}>
  <section className="invoice-register__detail-modal" dir="rtl" role="dialog" aria-modal="true" aria-label="زنجیره صورتحساب">
   <header className="invoice-register__dialog-header"><h2>زنجیره صورتحساب</h2>
    <button type="button" className="identity-secondary" onClick={onClose} aria-label="بستن">×</button>
   </header>
   {loading&&!graph&&<div className="invoice-lineage-loading" role="status" aria-live="polite" aria-busy="true">
    <div className="argin-loader"><span className="argin-loader-spinner" aria-hidden="true"/>
     <strong>در حال دریافت زنجیره صورتحساب…</strong>
     <span className="argin-loader-subtitle">سوابق ارسال و ارتباط اسناد در حال دریافت است؛ لطفاً منتظر بمانید.</span>
    </div>
    <div className="invoice-lineage-skeleton" aria-hidden="true">
     {[0,1,2].map(index=><div className="invoice-lineage-skeleton__node" key={index}>
      <span className="invoice-lineage-skeleton__dot"/>
      <div className="invoice-lineage-skeleton__card"><span/><span/><span/></div>
     </div>)}
    </div>
   </div>}
   {loading&&graph&&<p className="invoice-lineage-refresh" role="status" aria-live="polite" aria-busy="true">
    <span className="argin-loader-spinner" aria-hidden="true"/>در حال به‌روزرسانی زنجیره…
   </p>}
   {error&&<div role="alert" className="form-error invoice-lineage-error"><span>{error}</span>
    <button type="button" className="identity-secondary invoice-action-button" disabled={loading} onClick={()=>setReload(value=>value+1)}>تلاش دوباره</button>
   </div>}
   {inquiryMessage&&<p role="status">{inquiryMessage}</p>}
   {inquiryReport&&<section className="invoice-authority-report" aria-label="گزارش استعلام سازمان">
    <h3>گزارش استعلام سازمان: {authorityStatusLabel[inquiryReport.report.status]??inquiryReport.report.status}</h3>
    <p>زمان استعلام: {date(inquiryReport.report.observedAtUtc)}</p>
    <h4>خطاهای سازمان</h4>
    {inquiryReport.report.errors.length?<ul>{inquiryReport.report.errors.map((item,index)=><li key={item.code+":"+index}><bdi dir="ltr">{item.code}</bdi> — {item.message}</li>)}</ul>:<p>خطایی گزارش نشده است.</p>}
    <h4>هشدارهای سازمان</h4>
    {inquiryReport.report.warnings.length?<ul>{inquiryReport.report.warnings.map((item,index)=><li key={item.code+":"+index}><bdi dir="ltr">{item.code}</bdi> — {item.message}</li>)}</ul>:<p>هشداری گزارش نشده است.</p>}
    <p>این گزارش نتیجه استعلام است و به‌تنهایی پذیرش قطعی مالیاتی را ثبت نمی‌کند.</p>
   </section>}
   {graph&&!graph.hasDispatchAttempt&&<p>برای این صورتحساب هنوز سابقه ارسال ثبت نشده است.</p>}
    {graph?.hasDispatchAttempt&&<div className="invoice-lineage-nodes" style={{display:"grid",gap:8,padding:10,maxHeight:"65dvh",overflow:"auto"}}>
    {graph.nodes.map((node,i)=><div className="invoice-lineage-node" key={node.invoiceId}>
      {i>0&&<div style={{textAlign:"center",fontSize:12,color:"#607981",padding:6}}>
       ↓ ارجاع به شناسه مالیاتی سند مرجع
      </div>}
      <article className="invoice-lineage-node__card" style={{border:"1px solid #bedad5",borderRadius:9,padding:12,background:"#f8fbfa"}}>
       <strong>{label[node.subject]??node.subject}</strong>
       <button type="button" className="identity-secondary invoice-lineage-print-button"
        aria-label={printBusyId===node.invoiceId||!printReady[node.invoiceId]?"در حال آماده‌سازی چاپ":"چاپ صورتحساب"}
        title={printBusyId===node.invoiceId||!printReady[node.invoiceId]?"در حال آماده‌سازی چاپ":"چاپ صورتحساب"}
        aria-busy={printBusyId===node.invoiceId||!printReady[node.invoiceId]}
        disabled={!printReady[node.invoiceId]||printBusyId!==null} onClick={()=>{
         if(printReady[node.invoiceId]){
          previewDetails.current[node.invoiceId]?.setAttribute("open","");
          setPrintBusyId(node.invoiceId);
          setPrintTargetId(node.invoiceId);
         }
        }}>
        {printBusyId===node.invoiceId||!printReady[node.invoiceId]
         ?<span className="invoice-lineage-print-spinner" aria-hidden="true"/>
         :<svg className="invoice-lineage-print-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
          <path d="M7 8V3h10v5M7 17H5a2 2 0 0 1-2-2v-4a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3v4a2 2 0 0 1-2 2h-2M7 14h10v7H7z"/>
          <path d="M18 11h.01"/>
         </svg>}
       </button>
       <div style={{fontSize:12,display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(190px,1fr))",gap:6,marginTop:8}}>
        <span>شماره صورتحساب: <bdi dir="ltr">{node.systemCode??"—"}</bdi></span>
        <span>شناسه مالیاتی: <bdi dir="ltr">{node.fiscalTaxId??"تخصیص نیافته"}</bdi></span>
        <span>صدور: {date(node.issuedAtUtc)}</span>
        <span>ارسال: {date(node.sentAtUtc)}</span>
        <span>وضعیت ارسال: {node.dispatchState}</span>
        <span>نتیجه سازمان: {authorityStatusLabel[
         inquiryReport?.invoiceId===node.invoiceId?inquiryReport.report.status:(node.authorityStatus??"")
        ]??node.authorityStatus??"ثبت نشده"}</span>
        <span>شماره رهگیری: <bdi dir="ltr">{node.authorityReference??"—"}</bdi></span>
       </div>
       {node.authorityReference&&<button type="button" className="identity-secondary invoice-lineage-inquiry" aria-busy={inquiryBusy===node.invoiceId}
        disabled={inquiryBusy!==null} onClick={()=>{
         setInquiryBusy(node.invoiceId);setInquiryMessage("");setInquiryReport(null);
         void fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+
          encodeURIComponent(node.invoiceId)+"/inquiry-by-reference",
          {method:"POST",credentials:"same-origin",cache:"no-store"})
          .then(async resp=>{const result=await resp.json() as {
           safeCode?:string;authorityResult?:AuthorityReport|null;error?:string};
           if(!resp.ok)throw Error(String(result.error??resp.status));
           const report=result.authorityResult;
           if(report)setInquiryReport({invoiceId:node.invoiceId,report});
           setInquiryMessage("نتیجه استعلام: "+(report
            ?authorityStatusLabel[report.status]??report.status
            :String(result.safeCode??"در انتظار بررسی")));
           setReload(v=>v+1);
           onChanged?.();
          })
          .catch(e=>setInquiryMessage("استعلام ناموفق بود: "+(e as Error).message))
          .finally(()=>setInquiryBusy(null));
        }}>{inquiryBusy===node.invoiceId?<><span className="argin-loader-spinner" aria-hidden="true"/>در حال استعلام…</>:"استعلام این سند از سازمان"}</button>}
       {node.canResumeSubmission&&<button type="button" className="identity-secondary invoice-lineage-inquiry"
        disabled={inquiryBusy!==null} aria-busy={inquiryBusy===node.invoiceId}
        onClick={()=>void resumeCorrection(node)}>
        {inquiryBusy===node.invoiceId?<><span className="argin-loader-spinner" aria-hidden="true"/>در حال ادامه ارسال همین اصلاحیه…</>:"ادامه ارسال همین اصلاحیه"}
       </button>}
       <details className="fiscal-section" style={{marginTop:10}}
        ref={element=>{previewDetails.current[node.invoiceId]=element;}}>
        <summary>مشاهده صورتحساب در همین مرحله</summary>
        <div className="fiscal-section-content invoice-lineage-print-sheet"
         data-printing={printTargetId===node.invoiceId?"true":"false"}>
         <div className="invoice-lineage-preview-scroll">
         <InvoiceFormalPreview taxpayerId={taxpayerId} invoiceId={node.invoiceId}
          reference={node.systemCode??""} authorityReference={node.authorityReference}
          referenceFiscalTaxId={node.referenceFiscalTaxId}
          authorityStatus={node.authorityStatus??undefined} subject={node.subject}
          invoiceSubject={node.subject} printLayout readOnly
          onPreviewReady={()=>setPrintReady(current=>current[node.invoiceId]?current:
           {...current,[node.invoiceId]:true})}/>
         </div>
        </div>
       </details>
      </article>
     </div>)}
    {!graph.hasChanges&&<p className="field-hint">سند ارجاعی تأییدشده‌ای در زنجیره ثبت نشده است. ارتباط اصلاح، ابطال و برگشت از فروش در ادامه گام ۲۵ تکمیل می‌شود.</p>}
   </div>}
  </section>
 </div>;
}
