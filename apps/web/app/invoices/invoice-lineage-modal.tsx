"use client";
import {useEffect,useState} from "react";

type Node={invoiceId:string;subject:string;fiscalTaxId:string|null;
 issuedAtUtc:string;dispatchState:string;authorityReference:string|null;
 authorityStatus:string|null;sentAtUtc:string|null};
type Graph={hasDispatchAttempt:boolean;hasChanges:boolean;outcomeUnknown:boolean;nodes:Node[]};
const label:Record<string,string>={Original:"اصلی",Correction:"اصلاحی",Cancellation:"ابطالی",SalesReturn:"برگشت از فروش"};
const date=(value:string|null)=>value?new Date(value).toLocaleString("fa-IR"):"—";
export default function InvoiceLineageModal({taxpayerId,invoiceId,onClose}:{
 taxpayerId:string;invoiceId:string;onClose:()=>void}){
 const [graph,setGraph]=useState<Graph|null>(null);
 const [error,setError]=useState("");
 useEffect(()=>{
  const controller=new AbortController();
  void (async()=>{
   try{
    const res=await fetch("/api/invoice-dispatch/"+
     encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/lineage",
     {credentials:"same-origin",cache:"no-store",signal:controller.signal});
    if(!res.ok)throw Error("دریافت زنجیره صورتحساب ناموفق بود.");
    const data=await res.json() as Graph;
    if(!controller.signal.aborted)setGraph(data);
   }catch(e){if(!controller.signal.aborted)setError((e as Error).message);}
  })();
  return()=>controller.abort();
 },[taxpayerId,invoiceId]);
 return <div className="invoice-register__overlay" onMouseDown={e=>{if(e.target===e.currentTarget)onClose();}}>
  <section className="invoice-register__detail-modal" dir="rtl" role="dialog" aria-modal="true" aria-label="زنجیره صورتحساب">
   <header className="invoice-register__dialog-header"><h2>زنجیره صورتحساب</h2>
    <button type="button" className="identity-secondary" onClick={onClose} aria-label="بستن">×</button>
   </header>
   {!graph&&!error&&<p role="status">در حال دریافت زنجیره…</p>}
   {error&&<p role="alert" className="form-error">{error}</p>}
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
        <span>نتیجه سازمان: {node.authorityStatus??"ثبت نشده"}</span>
        <span>شماره رهگیری: <bdi dir="ltr">{node.authorityReference??"—"}</bdi></span>
       </div>
      </article>
     </div>)}
    {!graph.hasChanges&&<p className="field-hint">سند ارجاعی تأییدشده‌ای در زنجیره ثبت نشده است. ارتباط اصلاح، ابطال و برگشت از فروش در ادامه گام ۲۵ تکمیل می‌شود.</p>}
   </div>}
  </section>
 </div>;
}
