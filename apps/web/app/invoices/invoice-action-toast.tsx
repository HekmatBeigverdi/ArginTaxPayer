"use client";
import {useEffect,useRef} from "react";

export function InvoiceActionIcon({name}:{name:"save"|"send"|"discard"|"close"|"check"|"items"|"settlement"|"review"}){
 const paths={items:"M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01",settlement:"M3 5h18v15H3V5Zm0 0V3h15v2M21 10h-6v5h6M17 12.5h.01",review:"M9 3h6v4H9V3ZM9 5H5v16h14V5h-4M8 14l3 3 5-6",save:"M5 3h12l4 4v14H3V3h2Zm2 0v6h10V3M7 21v-8h10v8",send:"m22 2-7 20-4-9-9-4L22 2ZM22 2 11 13",discard:"M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7",close:"m6 6 12 12M18 6 6 18",check:"m5 12 4 4L19 6"};
 return <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]}/></svg>;
}

export default function InvoiceActionToast({title,description,confirmLabel,onConfirm,onCancel}:{
 title:string;description:string;confirmLabel:string;onConfirm:()=>void;onCancel:()=>void
}){
 const cancel=useRef<HTMLButtonElement>(null);
 useEffect(()=>{
  const previous=document.activeElement as HTMLElement|null;
  cancel.current?.focus();
  const escape=(event:KeyboardEvent)=>{if(event.key==="Escape"){event.stopPropagation();onCancel();}};
  document.addEventListener("keydown",escape);
  return()=>{document.removeEventListener("keydown",escape);previous?.focus();};
 },[onCancel]);
 return <aside className="invoice-action-toast" dir="rtl" role="alertdialog" aria-label={title}>
  <div className="invoice-action-toast__icon"><InvoiceActionIcon name="send"/></div>
  <div className="invoice-action-toast__body"><strong>{title}</strong><p>{description}</p>
   <div className="invoice-action-toast__actions">
    <button type="button" className="identity-primary invoice-action-button" onClick={onConfirm}><InvoiceActionIcon name="check"/>{confirmLabel}</button>
    <button ref={cancel} type="button" className="identity-secondary invoice-action-button" onClick={onCancel}>بازگشت</button>
   </div>
  </div>
 </aside>;
}
