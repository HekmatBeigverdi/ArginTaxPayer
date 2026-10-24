"use client";

import {useCallback,useEffect,useState} from "react";

type Attempt={number:number;startedAtUtc:string;endedAtUtc:string|null;result:string};
type Receipt={kind:"InitialReceipt"|"InquiryEvidence";observedAtUtc:string;hasAuthorityReference:boolean};
type History={
  environment:"Mock";submissionEnabled:false;fiscalAccepted:false;
  signedPreviewAvailable:false;invoiceRevision:number|null;
  state:string;lastUpdatedAtUtc:string|null;attempts:Attempt[];receipts:Receipt[];
};
const safeResult:Record<string,string>={
  success:"ثبت نتیجه اولیه",acknowledged:"رسید اولیه",failed:"خطای ثبت‌شده",
  timeout:"پایان مهلت",indeterminate:"نتیجه نامشخص",Pending:"در حال بررسی",
  Recorded:"ثبت عملیات"
};
function date(value:string|null){
  if(!value)return "ثبت نشده";
  const d=new Date(value);
  return Number.isNaN(d.getTime())?"نامشخص":d.toLocaleString("fa-IR");
}

export default function InvoiceDispatchHistoryPanel({
  taxpayerId,invoiceId,isWorkspace
}:{taxpayerId:string;invoiceId:string;isWorkspace:boolean}){
  const [data,setData]=useState<History|null>(null);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState("");
  const [consent,setConsent]=useState(false);
  const [previewOpen,setPreviewOpen]=useState(false);
  const [confirmationOpen,setConfirmationOpen]=useState(false);
  const refresh=useCallback(async(signal?:AbortSignal)=>{
    if(isWorkspace)return;
    setBusy(true);setError("");
    try{
      const response=await fetch("/api/invoice-dispatch/"+
        encodeURIComponent(taxpayerId)+"/"+encodeURIComponent(invoiceId)+"/history",{
        method:"GET",credentials:"same-origin",cache:"no-store",signal
      });
      if(!response.ok)throw new Error(response.status===401?
        "نشست معتبر نیست. مجدداً وارد شوید.":response.status===403||response.status===404?
        "این سند برای شما قابل مشاهده نیست.":"تاریخچه ارسال در دسترس نیست.");
      const value:unknown=await response.json();
      if(!value||typeof value!=="object"||Array.isArray(value))
        throw Error("پاسخ تاریخچه نامعتبر است.");
      const row=value as Record<string,unknown>;
      if(row.environment!=="Mock"||row.submissionEnabled!==false||
        row.fiscalAccepted!==false||row.signedPreviewAvailable!==false||
        !Array.isArray(row.attempts)||!Array.isArray(row.receipts)||
        typeof row.state!=="string")
        throw Error("قرارداد امنیتی نمایش تاریخچه معتبر نیست.");
      if(!signal?.aborted)setData(row as History);
    }catch(e){
      if(!signal?.aborted){setData(null);setError((e as Error).message);}
    }finally{if(!signal?.aborted)setBusy(false);}
  },[taxpayerId,invoiceId,isWorkspace]);
  useEffect(()=>{
    const controller=new AbortController();
    void refresh(controller.signal);
    return ()=>controller.abort();
  },[refresh]);

  return <section className="invoice-dispatch-history" dir="rtl"
    aria-label="تاریخچه عملیات ارسال صورتحساب">
    <div className="invoice-dispatch-history__head">
      <div>
        <h3>پیش‌نمایش و تاریخچه عملیات</h3>
        <p>اطلاعات خواندنی از Outbox؛ هیچ ارسال یا استعلام زنده‌ای انجام نمی‌شود.</p>
      </div>
      <button type="button" className="identity-secondary"
        disabled={isWorkspace||busy} onClick={()=>void refresh()}>
        {busy?"در حال بارگذاری…":"بروزرسانی"}
      </button>
    </div>
    {isWorkspace?<p className="field-hint">ابتدا صورتحساب را از حالت پیش‌نویس تکمیل کنید.</p>:
      busy?<div role="status" aria-busy="true" className="invoice-dispatch-history__loading">
        <span className="argin-loader-spinner" aria-hidden="true"/>
        <span>در حال دریافت تاریخچه و رسیدها…</span>
      </div>:error?<p className="form-error" role="alert">{error}</p>:data&&<>
        <div className="invoice-dispatch-history__facts">
          <div><span>محیط عملیات</span><strong>Mock (شبیه‌سازی)</strong></div>
          <div><span>نسخه صورتحساب</span><strong>{data.invoiceRevision??"هنوز آماده نشده"}</strong></div>
          <div><span>وضعیت ثبت‌شده</span><strong>{data.state}</strong></div>
          <div><span>آخرین تغییر</span><strong>{date(data.lastUpdatedAtUtc)}</strong></div>
        </div>
        <button type="button" className="identity-secondary"
          aria-expanded={previewOpen} onClick={()=>setPreviewOpen(v=>!v)}>
          {previewOpen?"بستن پیش‌نمایش مشخصات":"پیش‌نمایش مشخصات عملیات"}
        </button>
        {previewOpen&&<div className="invoice-dispatch-history__preview">
          <p>شناسه سند: <bdi dir="ltr">{invoiceId}</bdi></p>
          <p>نسخه: {data.invoiceRevision??"موجود نیست"}</p>
          <p>وضعیت: {data.state}</p>
          <p>بسته امضاشده برای پیش‌نمایش موجود نیست. نمایش این مشخصات به معنای امضای نهایی یا مجوز ارسال نیست.</p>
        </div>}
        <h4>تلاش‌های ثبت‌شده</h4>
        {data.attempts.length===0?<p className="field-hint">هیچ تلاش ارسالی ثبت نشده است.</p>:
          <div className="console-table-scroll">
            <table className="console-table">
              <thead><tr><th>تلاش</th><th>شروع</th><th>پایان</th><th>نتیجه</th></tr></thead>
              <tbody>{data.attempts.map(a=><tr key={a.number}>
                <td>{a.number}</td><td>{date(a.startedAtUtc)}</td>
                <td>{date(a.endedAtUtc)}</td><td>{safeResult[a.result]??"ثبت عملیات"}</td>
              </tr>)}</tbody>
            </table>
          </div>}
        <h4>رسیدها و شواهد</h4>
        {data.receipts.length===0?<p className="field-hint">رسیدی برای این عملیات ثبت نشده است.</p>:
          <div className="invoice-dispatch-history__receipts">
            {data.receipts.map((r,i)=><div key={r.kind+"-"+r.observedAtUtc+"-"+i}>
              <strong>{r.kind==="InitialReceipt"?"رسید اولیه":"شاهد استعلام"}</strong>
              <span>{date(r.observedAtUtc)}</span>
              <span>{r.hasAuthorityReference?"دارای شناسه مرجع":"بدون شناسه مرجع"}</span>
            </div>)}
          </div>}
        <div className="invoice-dispatch-history__confirm">
          <button type="button" className="identity-secondary"
            onClick={()=>setConfirmationOpen(v=>!v)} aria-expanded={confirmationOpen}>
            {confirmationOpen?"بستن شرایط ارسال":"مشاهده شرایط و تأیید ارسال"}
          </button>
          {confirmationOpen&&<div>
            <label className="sandbox-memory-panel__consent">
              <input type="checkbox" checked={consent} onChange={e=>setConsent(e.target.checked)}/>
              می‌دانم ارسال صورتحساب ممکن است اثر قانونی داشته باشد و ابتدا باید محیط، حافظه و اطلاعات سند بررسی شوند.
            </label>
            <button type="button" className="identity-primary" disabled
              title="ارسال تا آماده‌سازی سند امضاشده، مجوز سمت سرور و Outbox عملیاتی مجاز نیست">
              تأیید و ارسال صورتحساب
            </button>
            <p className="field-hint">تأیید این گزینه صرفاً نمایشی است؛ هیچ درخواست POST یا رزرو عملیات ایجاد نمی‌کند.</p>
          </div>}
        </div>
        <p className="invoice-dispatch-history__notice">
          رسید اولیه و وضعیت تأیید داخلی به معنی پذیرش مالیاتی نیست.
          پذیرش سازمان در این صفحه تأیید نشده است.
        </p>
      </>}
  </section>;
}
