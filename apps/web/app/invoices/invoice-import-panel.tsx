"use client";
import {useMemo,useRef,useState,type DragEvent} from "react";
import {MAX_IMPORT_BYTES,parseCsv,parseXlsx,previewRows,type ImportMapping} from "./invoice-import-preview.mjs";
export type ImportValues={description:string;quantity:string;unitPrice:string;discount:string;vatRate:string};
const specs=[
 ["description","شرح کالا/خدمت"],["quantity","مقدار"],["unitPrice","قیمت واحد (ریال)"],
 ["discount","تخفیف واحد (ریال)"],["vatRate","نرخ VAT (درصد)"]
] as const;
export default function InvoiceImportPanel({onImport,disabled,onClose}:{
 onImport:(rows:ImportValues[])=>void;disabled:boolean;onClose:()=>void
}){
 const [matrix,setMatrix]=useState<string[][]>([]);
 const [mapping,setMapping]=useState<ImportMapping>({});
 const [filename,setFilename]=useState("");
 const [error,setError]=useState("");
 const [busy,setBusy]=useState(false);
 const [dragging,setDragging]=useState(false);
 const [progress,setProgress]=useState(0);
 const fileInput=useRef<HTMLInputElement>(null);
 const previews=useMemo(()=>{
  if(!matrix.length)return {items:[],error:""};
  try{return {items:previewRows(matrix,mapping),error:""};}
  catch(e){return {items:[],error:(e as Error).message};}
 },[matrix,mapping]);
 const invalid=previews.items.filter(x=>x.errors.length>0).length;
 async function load(file:File|undefined){
  setMatrix([]);setMapping({});setFilename("");setError("");setProgress(0);
  if(!file)return;
  if(file.size>MAX_IMPORT_BYTES){setError("حداکثر حجم مجاز فایل ۲ مگابایت است.");return;}
  setBusy(true);setFilename(file.name);
  try{
   const xlsx=/\.xlsx$/i.test(file.name),csv=/\.csv$/i.test(file.name);
   if(!xlsx&&!csv)throw Error("فرمت فایل باید CSV یا XLSX باشد.");
   const data=xlsx?await parseXlsx(await file.arrayBuffer()):parseCsv(await file.text());
   setProgress(85);
   if(data.length<2)throw Error("فایل فاقد ردیف داده است.");
   const headers=data[0].map(x=>x.trim().toLowerCase());
   const aliases:Record<string,string[]>={description:["description","شرح","کالا","خدمت"],
    quantity:["quantity","مقدار","تعداد"],unitPrice:["unitprice","unit price","قیمت واحد","نرخ"],
    discount:["discount","تخفیف"],vatRate:["vatrate","vat","مالیات","ارزش افزوده"]};
   const values:ImportMapping={};
   for(const [key] of specs){const i=headers.findIndex(h=>aliases[key].includes(h));values[key]=i<0?null:i;}
   setMapping(values);setMatrix(data);setProgress(100);
  }catch(e){setError((e as Error).message);}
  finally{setBusy(false);}
 }
 return <div className="invoice-register__overlay" onMouseDown={e=>{if(e.currentTarget===e.target&&!busy)onClose();}}>
 <div className="invoice-register__detail-modal" role="dialog" aria-modal="true" aria-label="ورود ردیف‌ها از فایل" dir="rtl"
  style={{width:"min(900px,96vw)",maxHeight:"90vh",overflowY:"auto"}}>
 <header className="invoice-register__dialog-header"><h2>ورود ردیف‌ها از فایل</h2><button type="button" className="identity-secondary" disabled={busy} onClick={onClose}>× بستن</button></header>
 <section aria-label="ورود آزمایشی ردیف‌های صورتحساب" className="invoice-register__fields" style={{padding:16}}>
  <h3>ورود ردیف‌ها از CSV / Excel</h3>
  <p className="field-hint">ابتدا فایل را پیش‌نمایش و ستون‌ها را نگاشت کنید. در این گام هیچ اطلاعاتی مستقیماً در دیتابیس ثبت نمی‌شود. فقط با تأیید شما، ردیف‌های معتبر به فرم اضافه می‌شوند.</p>
  <input ref={fileInput} id="invoice-import-file" className="console-file-visually-hidden"
   type="file" accept=".csv,.xlsx" disabled={busy||disabled}
   onChange={e=>{void load(e.currentTarget.files?.[0]);e.currentTarget.value="";}}/>
  <div className={"console-logo-dropzone"+(dragging?" is-dragging":"")+
   (busy||disabled?" is-disabled":"")} role="button"
   tabIndex={busy||disabled?-1:0} aria-disabled={busy||disabled}
   aria-label="انتخاب یا رها کردن فایل اکسل یا CSV صورتحساب"
   onClick={()=>{if(!busy&&!disabled)fileInput.current?.click();}}
   onKeyDown={e=>{if((e.key==="Enter"||e.key===" ")&&!busy&&!disabled){e.preventDefault();fileInput.current?.click();}}}
   onDragOver={e=>{e.preventDefault();if(!busy&&!disabled)setDragging(true);}}
   onDragLeave={e=>{if(!e.currentTarget.contains(e.relatedTarget as Node))setDragging(false);}}
   onDrop={(e:DragEvent<HTMLDivElement>)=>{e.preventDefault();setDragging(false);
     if(!busy&&!disabled)void load(e.dataTransfer.files.item(0)??undefined);}}>
   <span className="console-logo-upload-icon" aria-hidden="true">↑</span>
   <strong>{busy?"در حال پردازش فایل…":"فایل را اینجا رها کنید یا برای انتخاب کلیک کنید"}</strong>
   <small>Excel (.xlsx) یا CSV • حداکثر ۲ مگابایت • حداکثر ۵۰۰ ردیف</small>
   <small>بعد از انتخاب فایل، ستون‌ها را بررسی و نگاشت کنید</small>
  </div>
  {(busy||filename)&&<div className="console-upload-status" role="status" aria-live="polite">
    <div><span>{filename}</span><strong>{progress}٪</strong></div>
    <progress max={100} value={progress} aria-label="پیشرفت خواندن و بررسی فایل"/>
  </div>}
  <a href="/api/invoices/template" download="ArginTaxPayer_Invoice_Import_Template.xlsx"
   className="identity-secondary" style={{display:"inline-flex",alignItems:"center",marginTop:8}}>
   دریافت فایل نمونه Excel
  </a>
  {error&&<p role="alert" className="form-error">{error}</p>}
  {filename&&<p role="status">فایل انتخاب‌شده: {filename}</p>}
  {matrix.length>0&&<>
   <div className="invoice-register__row-tools">
    {specs.map(([key,label])=><label key={key}>{label}
     <select aria-label={"ستون "+label} value={mapping[key]??""}
      onChange={e=>setMapping(old=>({...old,[key]:e.target.value===""?null:Number(e.target.value)}))}>
      <option value="">انتخاب نشده</option>
      {matrix[0].map((name,index)=><option key={index} value={index}>{name||"ستون "+(index+1)}</option>)}
     </select></label>)}
   </div>
   {previews.error&&<p role="alert" className="form-error">{previews.error}</p>}
   {!previews.error&&<><p role="status">{previews.items.length} ردیف · {invalid} نامعتبر · {previews.items.length-invalid} معتبر</p>
   <div className="console-table-scroll" style={{maxHeight:240,overflowY:"auto"}}>
    <table className="console-table"><thead><tr><th>ردیف فایل</th><th>شرح</th><th>مقدار</th><th>قیمت واحد</th><th>وضعیت / علت</th></tr></thead>
    <tbody>{previews.items.map(item=><tr key={item.rowNumber}>
     <td>{item.rowNumber}</td><td>{item.values.description}</td><td>{item.values.quantity}</td><td>{item.values.unitPrice}</td>
     <td>{item.errors.length?item.errors.join("؛ "):"معتبر"}</td>
    </tr>)}</tbody></table>
   </div>
   <button type="button" className="identity-secondary"
    disabled={disabled||busy||invalid>0||previews.items.length===0}
    onClick={()=>{onImport(previews.items.map(x=>x.values));setMatrix([]);setFilename("");onClose();}}>
    افزودن ردیف‌های پیش‌نمایش‌شده به فرم</button>
   </>}
  </>}
 </section></div></div>;
}
