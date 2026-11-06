"use client";
import {useEffect,useState} from "react";
import InvoiceFormalPreview from "./invoice-formal-preview";

type Node={invoiceId:string;subject:string;fiscalTaxId:string|null;
 issuedAtUtc:string;dispatchState:string;authorityReference:string|null;
 authorityStatus:string|null;sentAtUtc:string|null};
type Graph={hasDispatchAttempt:boolean;hasChanges:boolean;outcomeUnknown:boolean;nodes:Node[]};
type AuthorityIssue={code:string;message:string};
type AuthorityReport={status:"FAILED"|"SUCCESS"|"PENDING"|"UNKNOWN";
 errors:AuthorityIssue[];warnings:AuthorityIssue[];observedAtUtc:string};
const label:Record<string,string>={Original:"اصلی",Correction:"اصلاحی",Cancellation:"ابطالی",SalesReturn:"برگشت از فروش"};
const authorityStatusLabel:Record<string,string>={
 FAILED:"رد شده توسط سازمان",SUCCESS:"نتیجه موفق سازمان",PENDING:"در انتظار بررسی سازمان",
 UNKNOWN:"نتیجه سازمان نامشخص",ACKNOWLEDGED:"کد رهگیری دریافت شد",INDETERMINATE:"نتیجه نامشخص"};
const date=(value:string|null)=>value?new Date(value).toLocaleString("fa-IR"):"—";
export default function InvoiceLineageModal({taxpayerId,invoiceId,onClose}:{
 taxpayerId:string;invoiceId:string;onClose:()=>void}){
 const [graph,setGraph]=useState<Graph|null>(null);
 const [error,setError]=useState("");
 const [reload,setReload]=useState(0);
 const [inquiryBusy,setInquiryBusy]=useState<string|null>(null);
 const [inquiryMessage,setInquiryMessage]=useState("");
 const [inquiryReport,setInquiryReport]=useState<{invoiceId:string;report:AuthorityReport}|null>(null);
 const [loading,setLoading]=useState(true);
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
   {graph?.hasDispatchAttempt&&<div style={{display:"grid",gap:8,padding:10,maxHeight:"65dvh",overflow:"auto"}}>
    {graph.nodes.map((node,i)=><div key={node.invoiceId}>
      {i>0&&<div style={{textAlign:"center",fontSize:12,color:"#607981",padding:6}}>
       ↓ ارجاع به شناسه مالیاتی سند مرجع
      </div>}
      <article style={{border:"1px solid #bedad5",borderRadius:9,padding:12,background:"#f8fbfa"}}>
       <strong>{label[node.subject]??node.subject}</strong>
       <div style={{fontSize:12,display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(190px,1fr))",gap:6,marginTop:8}}>
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
          })
          .catch(e=>setInquiryMessage("استعلام ناموفق بود: "+(e as Error).message))
          .finally(()=>setInquiryBusy(null));
        }}>{inquiryBusy===node.invoiceId?<><span className="argin-loader-spinner" aria-hidden="true"/>در حال استعلام…</>:"استعلام این سند از سازمان"}</button>}
       <details className="fiscal-section" style={{marginTop:10}}>
        <summary>مشاهده صورتحساب در همین مرحله</summary>
        <div className="fiscal-section-content">
         <InvoiceFormalPreview taxpayerId={taxpayerId} invoiceId={node.invoiceId} reference="" authorityStatus={node.authorityStatus??undefined} readOnly/>
        </div>
       </details>
      </article>
     </div>)}
    {!graph.hasChanges&&<p className="field-hint">سند ارجاعی تأییدشده‌ای در زنجیره ثبت نشده است. ارتباط اصلاح، ابطال و برگشت از فروش در ادامه گام ۲۵ تکمیل می‌شود.</p>}
   </div>}
  </section>
 </div>;
}
