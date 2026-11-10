"use client";

import {useCallback,useEffect,useState} from "react";
import InvoiceFormalPreview from "./invoice-formal-preview";

export default function InvoicePrintAction({taxpayerId,invoiceId,reference,subject}: {
 taxpayerId:string;invoiceId:string;reference:string;subject:string
}){
 const [requested,setRequested]=useState(false);
 const [ready,setReady]=useState(false);
 const [error,setError]=useState("");
 const onReady=useCallback(()=>setReady(true),[]);
 const onError=useCallback((message:string)=>{
  setError(message);setReady(false);setRequested(false);
 },[]);
 useEffect(()=>{
  if(!requested||!ready)return;
  const sheet=document.querySelector<HTMLElement>(".invoice-table-print-source .invoice-paper-print-sheet");
  if(!sheet){setError("نمای چاپ صورتحساب آماده نشد.");setRequested(false);setReady(false);return;}
  const root=document.createElement("div");
  root.id="invoice-lineage-print-root";
  root.dir="rtl";
  root.append(sheet.cloneNode(true));
  document.body.append(root);
  document.body.classList.add("invoice-lineage-printing");
  const afterPrint=()=>{
   document.body.classList.remove("invoice-lineage-printing");root.remove();
   setRequested(false);setReady(false);
  };
  window.addEventListener("afterprint",afterPrint,{once:true});
  const imageReady=Promise.all(Array.from(root.querySelectorAll<HTMLImageElement>("img"),image=>
   image.decode().catch(()=>undefined)));
  const timer=window.setTimeout(()=>{void imageReady.then(()=>window.print());},100);
  return()=>{
   window.clearTimeout(timer);window.removeEventListener("afterprint",afterPrint);
   document.body.classList.remove("invoice-lineage-printing");root.remove();
  };
 },[requested,ready]);
 const busy=requested&&!ready;
 return <>
  <button type="button" className="identity-secondary invoice-lineage-print-button invoice-table-print-button"
   aria-label={busy?"در حال آماده‌سازی چاپ":error?"تلاش دوباره برای چاپ صورتحساب":"چاپ صورتحساب"}
   title={busy?"در حال آماده‌سازی چاپ":error?"چاپ انجام نشد؛ برای تلاش دوباره کلیک کنید":"چاپ صورتحساب"}
   aria-busy={busy} disabled={busy}
   onClick={()=>{setError("");setReady(false);setRequested(true);}}>
   {busy?<span className="invoice-lineage-print-spinner" aria-hidden="true"/>:
    <svg className="invoice-lineage-print-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
     <path d="M7 8V3h10v5M7 17H5a2 2 0 0 1-2-2v-4a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3v4a2 2 0 0 1-2 2h-2M7 14h10v7H7z"/>
     <path d="M18 11h.01"/>
    </svg>}
  </button>
  {requested&&<div className="invoice-table-print-source" aria-hidden="true">
   <InvoiceFormalPreview taxpayerId={taxpayerId} invoiceId={invoiceId} reference={reference}
    subject={subject} invoiceSubject={subject} printLayout readOnly onPreviewReady={onReady} onPreviewError={onError}/>
  </div>}
 </>;
}

