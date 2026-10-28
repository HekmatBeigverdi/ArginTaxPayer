"use client";
import {useCallback,useEffect,useState} from "react";

type Attempt={number:number;startedAtUtc:string;endedAtUtc:string|null;result:string};
type Receipt={kind:"InitialReceipt"|"InquiryEvidence";observedAtUtc:string;hasAuthorityReference:boolean};
type History={environment:"Sandbox";state:string;invoiceRevision:number|null;
 lastUpdatedAtUtc:string|null;attempts:Attempt[];receipts:Receipt[];
 recoveryAction:string;recoveryCode:string;mayRetry:false;mayInquire:false};
const statusNames:Record<string,string>={
 NotPrepared:"آماده‌سازی نشده",Prepared:"آماده‌شده",Reserved:"آماده ارسال",
 Sending:"در حال ارسال",Acknowledged:"شماره رهگیری دریافت شده",
 Indeterminate:"نتیجه نامشخص",RetryEligible:"نیازمند بررسی ارسال مجدد",
 FailedTerminal:"خطای نهایی"};
const date=(value:string|null)=>value?new Date(value).toLocaleString("fa-IR"):"—";
export default function InvoiceDispatchHistory({taxpayerId,invoiceId}:{
 taxpayerId:string;invoiceId:string}){
 const [history,setHistory]=useState<History|null>(null);
 const [pending,setPending]=useState(false);
 const [error,setError]=useState("");
 const load=useCallback(async(signal?:AbortSignal)=>{
  setPending(true);setError("");
  try{
   const response=await fetch("/api/invoice-dispatch/"+
    encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/history",{
     cache:"no-store",credentials:"same-origin",signal});
   if(!response.ok)throw Error(response.status===401?
     "نشست ورود منقضی شده است؛ دوباره وارد شوید.":
     "دریافت تاریخچه ارسال ناموفق بود (HTTP "+response.status+").");
   const body:unknown=await response.json();
   if(!body||typeof body!=="object"||Array.isArray(body)||
      (body as History).environment!=="Sandbox")
    throw Error("ساختار پاسخ تاریخچه معتبر نیست.");
   if(!signal?.aborted)setHistory(body as History);
  }catch(e){if(!signal?.aborted){setHistory(null);setError((e as Error).message);}}
  finally{if(!signal?.aborted)setPending(false);}
 },[invoiceId,taxpayerId]);
 useEffect(()=>{
  const controller=new AbortController();
  void load(controller.signal);
  return()=>controller.abort();
 },[load]);
 return <details dir="rtl" className="invoice-calculation-audit">
  <summary>تاریخچه ارسال و استعلام Sandbox</summary>
  <button type="button" className="identity-secondary" disabled={pending}
   onClick={()=>void load()}>{pending?"در حال دریافت…":"تازه‌سازی تاریخچه"}</button>
  {error&&<p role="alert" className="form-error">{error}</p>}
  {history&&<>
   <p>وضعیت: <strong>{statusNames[history.state]??"نیازمند بررسی"}</strong>
    {" — "}نسخه: {history.invoiceRevision??"—"}
    {" — "}آخرین تغییر: <time>{date(history.lastUpdatedAtUtc)}</time></p>
   {history.state==="Indeterminate"&&
    <p role="alert">نتیجه درخواست قبلی مشخص نیست. بدون دریافت شواهد معتبر، ارسال مجدد مجاز نیست.</p>}
   <h4>تلاش‌های ارسال</h4>
   {history.attempts.length===0?<p>تلاشی ثبت نشده است.</p>:
    <div className="table-scroll"><table><thead><tr><th>شماره</th><th>شروع</th><th>پایان</th><th>نتیجه</th></tr></thead>
    <tbody>{history.attempts.map((item,index)=><tr key={item.number+":"+index}>
     <td>{item.number}</td><td>{date(item.startedAtUtc)}</td>
     <td>{date(item.endedAtUtc)}</td><td><bdi dir="ltr">{item.result}</bdi></td>
    </tr>)}</tbody></table></div>}
   <h4>شواهد و پاسخ‌ها</h4>
   {history.receipts.length===0?<p>رسیدی ثبت نشده است.</p>:
    <div className="table-scroll"><table><thead><tr><th>نوع</th><th>زمان</th><th>مرجع سازمان</th></tr></thead>
    <tbody>{history.receipts.map((item,index)=><tr key={item.observedAtUtc+":"+index}>
     <td>{item.kind==="InitialReceipt"?"پاسخ اولیه ارسال":"شاهد استعلام"}</td>
     <td>{date(item.observedAtUtc)}</td>
     <td>{item.hasAuthorityReference?"ثبت شده":"ثبت نشده"}</td>
    </tr>)}</tbody></table></div>}
   <p className="field-hint">این تاریخچه صرفاً داده‌های ثبت‌شده را نمایش می‌دهد و به معنی پذیرش قطعی مالیاتی نیست.</p>
  </>}
 </details>;
}
