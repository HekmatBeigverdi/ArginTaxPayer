"use client";

import {useCallback,useEffect,useState} from "react";

type DispatchState = "NotPrepared" | "Prepared" | "Signed" | "Reserved" |
  "Sending" | "Acknowledged" | "Indeterminate" | "RetryEligible" |
  "FailedTerminal" | "Unavailable" | "Unrecognized";
type DispatchView = {
  environment: "Mock";
  submissionEnabled: false;
  inquiryEnabled: false;
  fiscalAccepted: false;
  status: DispatchState;
};
const labels: Record<DispatchState,string> = {
  NotPrepared:"هنوز آماده ارسال نشده",
  Prepared:"آماده‌سازی اولیه",
  Signed:"امضای داخلی (نیازمند تأیید)",
  Reserved:"رزرو شده؛ ارسال اثبات نشده",
  Sending:"در حال پردازش ارسال",
  Acknowledged:"رسید اولیه ثبت شده؛ پذیرش مالیاتی نیست",
  Indeterminate:"نتیجه نامشخص؛ نیازمند استعلام",
  RetryEligible:"نیازمند بررسی برای تلاش مجدد",
  FailedTerminal:"رد قطعی ثبت‌شده",
  Unavailable:"وضعیت در دسترس نیست",
  Unrecognized:"وضعیت ناشناخته"
};

export default function InvoiceDispatchPanel({
  invoiceId, taxpayerId, isWorkspace
}: {invoiceId:string; taxpayerId:string; isWorkspace:boolean}) {
  const [data,setData] = useState<DispatchView|null>(null);
  const [loading,setLoading] = useState(false);
  const [error,setError] = useState("");
  const refresh = useCallback(async (signal?:AbortSignal) => {
    if (isWorkspace) return;
    setLoading(true);setError("");
    try {
      const response=await fetch("/api/invoice-dispatch/"+encodeURIComponent(taxpayerId)+
        "/"+encodeURIComponent(invoiceId),{
          method:"GET",credentials:"same-origin",cache:"no-store",signal
        });
      if (!response.ok) throw new Error(
        response.status===401?"نشست معتبر نیست؛ دوباره وارد شوید.":
        response.status===403||response.status===404?"دسترسی به وضعیت این صورتحساب مجاز نیست یا سند نهایی ایجاد نشده است.":
        "دریافت وضعیت ارسال موقتاً در دسترس نیست.");
      const json:unknown=await response.json();
      if (!json || typeof json!=="object" || Array.isArray(json))
        throw new Error("پاسخ وضعیت معتبر نیست.");
      const value=json as Record<string,unknown>;
      if (value.environment!=="Mock" || value.submissionEnabled!==false ||
          value.inquiryEnabled!==false || value.fiscalAccepted!==false ||
          typeof value.status!=="string" || !(value.status in labels))
        throw new Error("تنظیمات محیط ارسال تأیید نشده است.");
      if (!signal?.aborted) setData(value as DispatchView);
    } catch (e) {
      if (!signal?.aborted) {setData(null);setError((e as Error).message);}
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  },[invoiceId,taxpayerId,isWorkspace]);

  useEffect(()=>{
    const controller=new AbortController();
    void refresh(controller.signal);
    return ()=>controller.abort();
  },[refresh]);

  return <section className="invoice-dispatch-ui" aria-label="وضعیت ارسال به سامانه مؤدیان" dir="rtl">
    <div className="invoice-dispatch-ui__head">
      <div>
        <h3>ارسال به سامانه مؤدیان</h3>
        <p>کنترل وضعیت اتصال و ارسال صورتحساب</p>
      </div>
      <span className="invoice-dispatch-ui__env" title="محیط شبیه‌سازی؛ ارسال قانونی نیست">
        محیط Mock
      </span>
    </div>
    <div className="invoice-dispatch-ui__grid">
      <div><span>محیط فعلی</span><strong>Mock (آزمایشی داخلی)</strong></div>
      <div><span>اتصال Sandbox</span><strong>فعال نشده</strong></div>
      <div><span>ارسال صورتحساب</span><strong>غیرفعال</strong></div>
      <div><span>استعلام سازمان</span><strong>غیرفعال</strong></div>
    </div>
    <div className="invoice-dispatch-ui__state" aria-live="polite">
      <span>وضعیت عملیات</span>
      <strong>{isWorkspace?"پیش‌نویس در حال تکمیل":loading?"در حال دریافت…":
        error?"نامشخص":data?labels[data.status]:"در انتظار دریافت اطلاعات"}</strong>
    </div>
    {error&&<p role="alert" className="form-error">{error}</p>}
    <div className="invoice-dispatch-ui__actions">
      <button type="button" className="identity-secondary"
        disabled={isWorkspace||loading} onClick={()=>void refresh()}>
        {loading?"در حال بررسی…":"بروزرسانی وضعیت"}
      </button>
      <button type="button" className="identity-primary" disabled
        title="ارسال تا تأیید اتصال Sandbox، امضای معتبر و Outbox عملیاتی غیرفعال است">
        ارسال صورتحساب
      </button>
      <button type="button" className="identity-secondary" disabled
        title="سرویس استعلام واقعی هنوز فعال نشده است">
        استعلام از سازمان
      </button>
    </div>
    <p className="invoice-dispatch-ui__hint">
      این صفحه در حالت شبیه‌سازی است. تأیید داخلی، رسید اولیه یا وضعیت آماده‌سازی
      به معنی پذیرش صورتحساب توسط سازمان امور مالیاتی نیست.
      ارسال آزمایشی Sandbox فقط پس از احراز دسترسی و فعال‌سازی امن سرور فراهم می‌شود.
    </p>
  </section>;
}
