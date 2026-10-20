"use client";
import {useMemo,useRef,useState,type DragEvent} from "react";
import {parseCsv,parseXlsx,MAX_IMPORT_BYTES} from "./invoice-import-preview.mjs";
import {previewBulkInvoices,type BulkInvoicePreview} from "./bulk-import-preview.mjs";
type Outcome={invoiceKey:string;status:string;draftId:string|null;error:string|null};
export default function BulkInvoiceImportPanel({onClose,onChanged}:{
 onClose:()=>void;onChanged:()=>void
}){
 const fileRef=useRef<HTMLInputElement>(null);
 const [dragging,setDragging]=useState(false);
 const [busy,setBusy]=useState(false);
 const [filename,setFilename]=useState("");
 const [batchKey,setBatchKey]=useState("");
 const [items,setItems]=useState<BulkInvoicePreview[]>([]);
 const [outcomes,setOutcomes]=useState<Outcome[]|null>(null);
 const [error,setError]=useState("");
 const invalid=useMemo(()=>items.filter(i=>i.errors.length>0).length,[items]);
 const formatter=new Intl.NumberFormat("fa-IR",{maximumFractionDigits:2});
 async function load(file:File|undefined){
  setItems([]);setOutcomes(null);setError("");setBatchKey("");setFilename("");
  if(!file)return;
  if(file.size>MAX_IMPORT_BYTES||file.size===0){setError("حجم فایل باید بین ۱ بایت تا ۲ مگابایت باشد.");return;}
  if(!/\.(csv|xlsx)$/i.test(file.name)){setError("فرمت فایل باید CSV یا XLSX باشد.");return;}
  setBusy(true);
  try{
   const buffer=await file.arrayBuffer();
   const digest=await crypto.subtle.digest("SHA-256",buffer);
   const hash=[...new Uint8Array(digest)].map(v=>v.toString(16).padStart(2,"0")).join("");
   const matrix=/\.xlsx$/i.test(file.name)?
    await parseXlsx(buffer):parseCsv(new TextDecoder("utf-8",{fatal:true}).decode(buffer));
   const preview=previewBulkInvoices(matrix);
   setFilename(file.name);setBatchKey(hash);setItems(preview);
  }catch(e){setError((e as Error).message);}
  finally{setBusy(false);}
 }
 async function submit(){
  if(!batchKey||busy||items.every(i=>i.draft===null)||outcomes)return;
  setBusy(true);setError("");
  try{
   const response=await fetch("/api/invoices/workspaces/bulk",{
    method:"POST",credentials:"same-origin",
    headers:{"Content-Type":"application/json"},
    body:JSON.stringify({batchKey,invoices:items.filter(i=>i.draft!==null).map(i=>({invoiceKey:i.invoiceKey,draft:i.draft}))})
   });
   if(!response.ok)throw Error(response.status===401?"نشست کاربری معتبر نیست.":response.status===503?
    "ارتباط با API برقرار نشد؛ با همان فایل دوباره تلاش کنید.":
    "درخواست ثبت گروهی پذیرفته نشد (HTTP "+response.status+").");
   const result=await response.json() as {results:Outcome[]};
   setOutcomes([...items.filter(i=>i.draft===null).map(i=>({
    invoiceKey:i.invoiceKey,status:"invalid",draftId:null,error:i.errors.join("؛ ")
   })),...result.results]);
   onChanged();
  }catch(e){setError((e as Error).message);}
  finally{setBusy(false);}
 }
 return <div className="invoice-register__overlay" dir="rtl" onMouseDown={e=>{
  if(e.currentTarget===e.target&&!busy)onClose();
 }}>
  <div className="invoice-register__detail-modal" role="dialog" aria-modal="true"
   aria-label="ورود گروهی صورتحساب‌ها" style={{width:"min(1000px,96vw)",maxHeight:"91vh",overflowY:"auto"}}>
   <header className="invoice-register__dialog-header">
    <h2>ورود گروهی صورتحساب‌ها</h2>
    <button type="button" className="identity-secondary" disabled={busy} onClick={onClose}>× بستن</button>
   </header>
   <section className="invoice-register__fields" style={{padding:16}}>
    <p className="field-hint">هر سطر فایل یک قلم صورتحساب است. سطرهای دارای invoiceKey یکسان در یک پیش‌نویس قرار می‌گیرند. این عملیات فقط پیش‌نویس ایجاد می‌کند و چیزی به سامانه مؤدیان ارسال نمی‌کند.</p>
    <a href="/api/invoices/bulk-template" download="ArginTaxPayer_Bulk_Invoice_Template.xlsx"
     className="identity-secondary" style={{display:"inline-flex",marginBottom:12}}>دریافت قالب استاندارد Excel</a>
    <p className="field-hint">قبل از ورود، شناسه‌های نمونه مؤدی و خریدار را با UUID واقعی مجاز در پنل خود جایگزین کنید. تاریخ صدور در فایل با قالب میلادی UTC ثبت می‌شود.</p>
    <input ref={fileRef} type="file" className="console-file-visually-hidden" accept=".xlsx,.csv"
     disabled={busy} aria-label="انتخاب فایل گروهی"
     onChange={e=>{void load(e.currentTarget.files?.[0]);e.currentTarget.value="";}}/>
    <div className={"console-logo-dropzone"+(dragging?" is-dragging":"")+(busy?" is-disabled":"")}
     role="button" tabIndex={busy?-1:0} aria-disabled={busy}
     onClick={()=>{if(!busy)fileRef.current?.click();}}
     onKeyDown={e=>{if(!busy&&(e.key==="Enter"||e.key===" ")){e.preventDefault();fileRef.current?.click();}}}
     onDragOver={e=>{e.preventDefault();if(!busy)setDragging(true);}}
     onDragLeave={e=>{if(!e.currentTarget.contains(e.relatedTarget as Node))setDragging(false);}}
     onDrop={(e:DragEvent<HTMLDivElement>)=>{e.preventDefault();setDragging(false);
      if(!busy)void load(e.dataTransfer.files.item(0)??undefined);}}>
     <span className="console-logo-upload-icon" aria-hidden="true">↑</span>
     <strong>{busy?"در حال پردازش…":"فایل Excel یا CSV را رها کنید یا کلیک کنید"}</strong>
     <small>حداکثر ۲ مگابایت، ۵۰۰ قلم و ۵۰ صورتحساب در هر Batch</small>
    </div>
    {filename&&<p role="status">{filename} · {items.length} صورتحساب · {invalid} نامعتبر</p>}
    {error&&<p role="alert" className="form-error">{error}</p>}
    {items.length>0&&<div className="console-table-scroll" style={{maxHeight:300,overflowY:"auto"}}>
     <table className="console-table"><thead><tr>
      <th>شناسه فایل</th><th>اقلام</th><th>جمع (ریال)</th><th>پیش‌بررسی</th><th>نتیجه ثبت</th>
     </tr></thead><tbody>{items.map(item=>{
       const outcome=outcomes?.find(o=>o.invoiceKey===item.invoiceKey);
       const state=outcome?.status==="created"?"ثبت‌شده":outcome?.status==="duplicate"?"قبلاً ثبت‌شده":
        outcome?.status==="conflict"?"تعارض محتوا":outcome?.status==="invalid"?"نامعتبر":
        outcome?.status==="forbidden"?"عدم دسترسی":outcome?.status==="not_found"?"مؤدی نامعتبر":
        outcome?"ناموفق":"در انتظار تأیید";
       return <tr key={item.invoiceKey}>
        <td dir="ltr">{item.invoiceKey}</td><td>{item.lineCount}</td><td>{formatter.format(item.total)}</td>
        <td>{item.errors.length?item.errors.join("؛ "):"معتبر"}</td>
        <td>{state}{outcome?.draftId&&<small style={{display:"block"}} dir="ltr">{outcome.draftId}</small>}</td>
       </tr>;
     })}</tbody></table>
    </div>}
    <div className="invoice-register__actions" style={{marginTop:12}}>
     <button type="button" className="identity-primary" disabled={busy||items.every(i=>i.draft===null)||!!outcomes}
      onClick={()=>void submit()}>{busy?"در حال ثبت…":"تأیید و ثبت پیش‌نویس‌های معتبر"}</button>
     {outcomes&&<button type="button" className="identity-secondary" onClick={()=>{setOutcomes(null);setError("");}}>
       بررسی دوباره نتایج یا Retry همان فایل</button>}
    </div>
   </section>
  </div>
 </div>;
}
