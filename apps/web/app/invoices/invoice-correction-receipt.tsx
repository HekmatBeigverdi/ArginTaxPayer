import {InvoiceActionIcon} from "./invoice-action-toast";
export type CorrectionReceiptValue={invoiceId?:string|null;status?:string|null;referenceNumber?:string|null;inquiryCode?:string|null;authorityStatus?:string|null};
const labels:Record<string,string>={SUCCESS:"تأیید شده توسط سازمان",FAILED:"رد شده توسط سازمان",PENDING:"در انتظار بررسی سازمان",UNKNOWN:"نتیجه استعلام هنوز مشخص نیست"};
export default function InvoiceCorrectionReceipt({receipt}:{receipt:CorrectionReceiptValue}){
 const accepted=receipt.authorityStatus==="SUCCESS";
 const rejected=receipt.authorityStatus==="FAILED";
 return <section className="correction-receipt" data-outcome={accepted?"success":rejected?"failed":"pending"} aria-label="نتیجه ارسال اصلاحیه" role="status" aria-live="polite">
  <header><span className="correction-receipt__icon"><InvoiceActionIcon name="check"/></span><div>
   <h3>{receipt.referenceNumber?"اصلاحیه ارسال شد و کد رهگیری دریافت شد":"سند اصلاحی ایجاد شد؛ وضعیت ارسال را بررسی کنید"}</h3>
   <p>{accepted?"نتیجه موفق استعلام برای صورتحساب اصلاحی ثبت شد.":rejected?"برای بررسی خطاها، گزارش سازمان را از بخش استعلام باز کنید.":"دریافت رهگیری به معنی تأیید نهایی سازمان نیست؛ ارسال را تکرار نکنید."}</p>
  </div></header>
  <dl><div><dt>وضعیت ارسال</dt><dd>{receipt.status==="Acknowledged"?"رسید ارسال دریافت شد":receipt.status==="Indeterminate"?"نیازمند بررسی":receipt.status==="Sending"?"در حال ارسال":"در انتظار بررسی"}</dd></div>
   <div><dt>نتیجه استعلام</dt><dd>{labels[receipt.authorityStatus??""]??"در انتظار استعلام سازمان"}</dd></div>
   <div className="correction-receipt__reference"><dt>شماره رهگیری اصلاحیه</dt><dd><bdi dir="ltr">{receipt.referenceNumber??"هنوز دریافت نشده"}</bdi></dd></div>
  </dl>
  {receipt.invoiceId&&<details><summary>مشخصات فنی سند اصلاحی</summary><span>شناسه داخلی سند</span><code dir="ltr">{receipt.invoiceId}</code></details>}
 </section>;
}
