"use client";
import {useEffect,useState} from "react";
type Line={description:string;officialIdentifier:string|null;quantity:number;unitPriceRial:number;discountRial:number;netRial:number;vatRatePercent:number;vatRial:number;totalRial:number};
type Invoice={issueDateUtc:string;revision:number;seller:string|null;buyer:string|null;description:string|null;contractNumber:string|null;settlementMethod:string|null;lines:Line[];grossRial:number;discountRial:number;netRial:number;vatRial:number;totalRial:number};
const money=(value:number)=>value.toLocaleString("fa-IR",{maximumFractionDigits:2});
export default function InvoiceFormalPreview({taxpayerId,invoiceId,reference}:{taxpayerId:string;invoiceId:string;reference:string}){
 const [invoice,setInvoice]=useState<Invoice|null>(null);
 const [error,setError]=useState("");
 useEffect(()=>{
  const controller=new AbortController();
  setInvoice(null);setError("");
  void (async()=>{
   try{
    const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/print-preview",{cache:"no-store",credentials:"same-origin",signal:controller.signal});
    if(!response.ok)throw Error(response.status===401?"نشست ورود منقضی شده است.":"نمای صورتحساب در دسترس نیست.");
    const data=await response.json() as Invoice;
    if(!controller.signal.aborted)setInvoice(data);
   }catch(e){if(!controller.signal.aborted)setError((e as Error).message);}
  })();return()=>controller.abort();
 },[taxpayerId,invoiceId]);
 return <section dir="rtl" aria-label="نمای رسمی صورتحساب" style={{border:"1px solid var(--border,#d2d9df)",borderRadius:12,padding:12,marginBlock:12}}>
  <h3 style={{textAlign:"center",margin:"4px 0 16px"}}>صورتحساب فروش کالا و خدمات</h3>
  {error&&<p role="alert" className="form-error">{error}</p>}
  {!invoice&&!error&&<p role="status">در حال دریافت اطلاعات صورتحساب…</p>}
  {invoice&&<>
   <h4>سربرگ صورتحساب</h4>
   <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(180px,1fr))",gap:8,marginBottom:14}}>
    <span>فروشنده: <strong>{invoice.seller??"—"}</strong></span>
    <span>خریدار: <strong>{invoice.buyer??"—"}</strong></span>
    <span>تاریخ: <strong>{new Date(invoice.issueDateUtc).toLocaleDateString("fa-IR-u-ca-persian")}</strong></span>
    <span>نسخه: <strong>{invoice.revision}</strong></span>
    {reference&&<span>شماره صورتحساب: <strong dir="ltr">{reference}</strong></span>}
    {invoice.contractNumber&&<span>شماره قرارداد: <strong dir="ltr">{invoice.contractNumber}</strong></span>}
   </div>
   <h4>بدنه صورتحساب</h4>
   <div className="table-scroll" style={{maxHeight:340,overflow:"auto",marginBottom:14}}>
    <table className="console-table" style={{minWidth:900}}>
     <thead><tr><th>ردیف</th><th>شرح کالا/خدمت</th><th>شناسه رسمی</th><th>تعداد</th><th>فی (ریال)</th><th>تخفیف</th><th>مالیات</th><th>مبلغ نهایی (ریال)</th></tr></thead>
     <tbody>{invoice.lines.map((line,i)=><tr key={i}><td>{i+1}</td>
      <td>{line.description}</td><td><bdi dir="ltr">{line.officialIdentifier??"—"}</bdi></td>
      <td>{money(line.quantity)}</td><td>{money(line.unitPriceRial)}</td>
      <td>{money(line.discountRial)}</td><td>{money(line.vatRial)}</td>
      <td>{money(line.totalRial)}</td></tr>)}</tbody>
    </table>
   </div>
   <h4>پاورقی و جمع مبالغ</h4>
   <div style={{display:"grid",gridTemplateColumns:"repeat(auto-fit,minmax(180px,1fr))",gap:8}}>
    <span>جمع ناخالص: <strong>{money(invoice.grossRial)} ریال</strong></span>
    <span>جمع تخفیف: <strong>{money(invoice.discountRial)} ریال</strong></span>
    <span>جمع پس از تخفیف: <strong>{money(invoice.netRial)} ریال</strong></span>
    <span>مالیات ارزش افزوده: <strong>{money(invoice.vatRial)} ریال</strong></span>
    <span>مبلغ قابل پرداخت: <strong>{money(invoice.totalRial)} ریال</strong></span>
    {invoice.settlementMethod&&<span>روش تسویه: <strong>{invoice.settlementMethod}</strong></span>}
   </div>
   {invoice.description&&<p>توضیحات: {invoice.description}</p>}
  </>}
 </section>;
}
